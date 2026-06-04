using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twitch.EventSub.API.Extensions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.APIConduit;

namespace Twitch.EventSub.API;

public class ConduitOrchestrator : IConduitOrchestrator
{
    private readonly ITwitchConduitApi _api;
    private readonly TwitchApi _twitchApi;
    private readonly ILogger<ConduitOrchestrator> _logger;
    private readonly EventSubClientOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly List<ConduitReplica> _replicas = new();

    public IReadOnlyList<string> ConduitIds => _replicas.Select(r => r.ConduitId).ToList();
    public string ConduitIdAt(int replicaIndex) => _replicas[replicaIndex].ConduitId;

    /// <summary>
    /// Supplied by the composition root: opens a fresh WebSocket shard session on the given replica
    /// and returns its session id. Used to reactivate a disabled shard. Null = recovery disabled.
    /// </summary>
    public Func<int, CancellationToken, Task<string>>? OpenReplacementSessionAsync { get; set; }

    /// <summary>
    /// Preferred recovery seam: opens a fresh shard and returns BOTH its session id and the internal
    /// shard id tracking it. When set, this is used in preference to <see cref="OpenReplacementSessionAsync"/>
    /// so the reactivated slot can be remapped to the new shard (enabling future reconnect Updates of
    /// that same slot rather than a new Add).
    /// </summary>
    public Func<int, CancellationToken, Task<(string Session, string InternalShardId)>>? OpenReplacementShardDetailedAsync { get; set; }

    public ConduitOrchestrator(ITwitchConduitApi api, IOptions<EventSubClientOptions> options, ILogger<ConduitOrchestrator> logger, TwitchApi twitchApi)
    {
        _api = api;
        _twitchApi = twitchApi;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        int n = _options.RedundancyFactor < 1 ? 1 : _options.RedundancyFactor;
        var existing = await _api.GetConduitIdsAsync(_options.AppAccessToken, _options.ClientId, ct) ?? new();

        if (existing.Count > _options.MaxConduits)
        {
            throw new InvalidOperationException(
                $"Cannot create a new conduit: client already has {existing.Count} conduits " +
                $"(Twitch limit: {EventSubClientOptions.TwitchMaxConduits}, configured MaxConduits: {_options.MaxConduits}).");
        }

        _replicas.Clear();
        for (int i = 0; i < n; i++)
        {
            if (i < existing.Count)
            {
                var id = existing[i];
                _logger.LogInformation("ConduitOrchestrator reusing existing conduit {ConduitId} for replica {Replica}", id, i);
                var replica = new ConduitReplica(i, id);
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var shards = await _api.GetAllConduitGetShardsAsync(_options.ClientId, _options.AppAccessToken, id, cts, _logger);
                    int shardCount = shards?.Count ?? 0;
                    replica.TwitchShardCount = shardCount < 1 ? 1 : shardCount;
                    if (shardCount > 0)
                        replica.SetReconciledSlotBase(shardCount);
                }
                _logger.LogInformation("ConduitOrchestrator reusing conduit {ConduitId} with {Count} existing shard slots", id, replica.TwitchShardCount);
                _replicas.Add(replica);
            }
            else
            {
                var id = await _api.CreateConduitAsync(_options.AppAccessToken, _options.ClientId, ct)
                    ?? throw new InvalidOperationException("Failed to create conduit");
                _logger.LogInformation("ConduitOrchestrator created new conduit {ConduitId} for replica {Replica}", id, i);
                _replicas.Add(new ConduitReplica(i, id) { TwitchShardCount = 1 });
            }
        }

        // Register conduit.shard.disabled subscription on each replica for health monitoring.
        foreach (var replica in _replicas)
        {
            var req = new CreateSubscriptionRequest
            {
                Condition = new Condition(),
                Transport = new Transport { Method = "conduit", ConduitId = replica.ConduitId }
            }.SetConduitShardDisabled(_options.ClientId, replica.ConduitId);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                await _twitchApi.SubscribeAsync(_options.ClientId, _options.AppAccessToken, req, cts, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed creating conduit.shard.disabled subscription on {ConduitId}", replica.ConduitId);
            }
        }
    }

    public async Task AddShardAsync(int replicaIndex, string shardId, string sessionId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var r = _replicas[replicaIndex];
            var slot = r.ReserveShardSlot(shardId);
            if (slot.NeedsExpand)
            {
                _logger.LogInformation("ConduitOrchestrator expanding shard_count {Old} → {New} on conduit {ConduitId} (replica {Replica})", r.TwitchShardCount, slot.NewShardCount, r.ConduitId, replicaIndex);
                await _api.UpdateConduitShardCountAsync(r.ConduitId, slot.NewShardCount, _options.AppAccessToken, _options.ClientId, ct);
                r.TwitchShardCount = slot.NewShardCount;
            }

            _logger.LogInformation("ConduitOrchestrator assigning shard {ShardId} → Twitch index {TwitchIndex}, session={SessionId}, conduit={ConduitId} (replica {Replica})", shardId, slot.TwitchIndex, sessionId, r.ConduitId, replicaIndex);
            await _api.UpdateConduitShardSessionAsync(r.ConduitId, slot.TwitchIndex, sessionId, _options.AppAccessToken, _options.ClientId, ct);
            r.CommitShard(shardId, slot.TwitchIndex, sessionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateShardAsync(int replicaIndex, string shardId, string oldSessionId, string newSessionId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var r = _replicas[replicaIndex];
            if (!r.TryGetShard(shardId, out var entry))
            {
                _logger.LogWarning("ConduitOrchestrator UpdateShardAsync: unknown shardId {ShardId} (replica {Replica})", shardId, replicaIndex);
                return;
            }

            _logger.LogInformation("ConduitOrchestrator updating shard {ShardId} (Twitch index {TwitchIndex}) session {Old} → {New} on conduit {ConduitId} (replica {Replica})", shardId, entry.TwitchIndex, oldSessionId, newSessionId, r.ConduitId, replicaIndex);
            await _api.UpdateConduitShardSessionAsync(r.ConduitId, entry.TwitchIndex, newSessionId, _options.AppAccessToken, _options.ClientId, ct);
            r.UpdateShardSession(shardId, newSessionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveShardAsync(int replicaIndex, string shardId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var r = _replicas[replicaIndex];
            if (!r.TryGetShard(shardId, out _))
            {
                _logger.LogWarning("ConduitOrchestrator RemoveShardAsync: unknown shardId {ShardId} (replica {Replica})", shardId, replicaIndex);
                return;
            }

            int originalCount = r.TwitchShardCount;
            var plan = r.PlanRemoval(shardId);

            if (!plan.TargetIsLast && plan.LastShardId != null)
            {
                _logger.LogInformation("ConduitOrchestrator swapping last shard {LastShardId} into freed slot {FreedIndex} on conduit {ConduitId} (replica {Replica})", plan.LastShardId, plan.FreedIndex, r.ConduitId, replicaIndex);
                await _api.UpdateConduitShardSessionAsync(r.ConduitId, plan.FreedIndex, plan.LastSessionId!, _options.AppAccessToken, _options.ClientId, ct);
            }

            r.ApplyRemoval(shardId, plan);

            // Reduce shard_count only when there was more than one slot to begin with.
            // Removing the sole shard clamps count at >=1 with no API call (mirrors prior behavior).
            if (originalCount > 1)
            {
                _logger.LogInformation("ConduitOrchestrator reducing shard_count {Old} → {New} on conduit {ConduitId} (replica {Replica})", originalCount, r.TwitchShardCount, r.ConduitId, replicaIndex);
                await _api.UpdateConduitShardCountAsync(r.ConduitId, r.TwitchShardCount, _options.AppAccessToken, _options.ClientId, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task TeardownAsync(CancellationToken ct)
    {
        foreach (var r in _replicas)
        {
            _logger.LogInformation("ConduitOrchestrator teardown: deleting conduit {ConduitId} (replica {Replica}; Twitch auto-removes subscriptions)", r.ConduitId, r.Index);
            await _api.DeleteConduitAsync(r.ConduitId, _options.AppAccessToken, _options.ClientId, ct);
        }
        _replicas.Clear();
    }

    public async Task HandleShardDisabledAsync(string conduitId, string shardId, CancellationToken ct)
    {
        var replica = _replicas.FirstOrDefault(r => r.ConduitId == conduitId);
        if (replica == null) { _logger.LogWarning("HandleShardDisabled: unknown conduit {ConduitId}", conduitId); return; }

        // Open a fresh shard. Prefer the detailed opener (gives us the internal shard id so we can remap
        // the reactivated slot), falling back to the session-only seam for back-compat / tests.
        string newSession;
        string? internalShardId = null;
        if (OpenReplacementShardDetailedAsync != null)
        {
            (newSession, internalShardId) = await OpenReplacementShardDetailedAsync(replica.Index, ct);
        }
        else if (OpenReplacementSessionAsync != null)
        {
            newSession = await OpenReplacementSessionAsync(replica.Index, ct);
        }
        else
        {
            _logger.LogWarning("HandleShardDisabled: no recovery opener set; cannot reactivate shard {ShardId} on {ConduitId}", shardId, conduitId);
            return;
        }

        if (string.IsNullOrEmpty(newSession)) { _logger.LogWarning("HandleShardDisabled: replacement session empty for {ConduitId}/{ShardId}", conduitId, shardId); return; }

        await _lock.WaitAsync(ct);
        try
        {
            // SOLE conduit writer for recovery: reuse (PATCH) the existing disabled slot. No Add, no shard_count change.
            await _api.UpdateConduitShardSessionAsync(conduitId, shardId, newSession, _options.AppAccessToken, _options.ClientId, ct);

            // Rebind the slot to the new shard in the replica map so future reconnects of the replacement
            // shard issue an Update of THIS slot (never a new Add). shardId here is the Twitch slot index.
            if (internalShardId != null)
                replica.RebindSlot(shardId, internalShardId, newSession);

            _logger.LogInformation("HandleShardDisabled: reactivated shard {ShardId} on {ConduitId} with fresh session", shardId, conduitId);
        }
        finally { _lock.Release(); }
    }
}
