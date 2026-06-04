using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Twitch.EventSub.User;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Allocates users across WebSocket shards. All user-assignment operations
/// are serialized by a SemaphoreSlim(1,1) to prevent concurrent capacity violations.
/// Capacity limit: MaxShardsPerConduit controls the number of shards (not users per shard).
/// </summary>
public class ShardManager : IShardManager
{
    private readonly EventSubClientOptions _options;
    private readonly ILogger<ShardManager> _logger;
    private readonly IMessagePipeline _messagePipeline;
    private readonly ConcurrentDictionary<string, ShardContext> _shards = new();
    private readonly ConcurrentDictionary<(string UserId, int ReplicaIndex), string> _userToShard = new();  // (userId, replica) → shardId
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _shardCounter;

    public event EventHandler<SessionIdUpdatedArgs>? OnSessionIdUpdated;

    public ShardManager(IOptions<EventSubClientOptions> options, ILogger<ShardManager> logger, IMessagePipeline messagePipeline)
    {
        _options = options.Value;
        _logger = logger;
        _messagePipeline = messagePipeline;
    }

    public IReadOnlyList<(string ShardId, string? SessionId)> ActiveSessionIds =>
        _shards.Select(kv => (kv.Key, kv.Value.Sequencer.SessionId)).ToList();

    /// <summary>Current number of active shards. Exposed for tests and monitoring.</summary>
    public int ShardCount => _shards.Count;

    public async Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, int replicaIndex, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Find a shard for THIS replica that still has room for one more user.
            var available = _shards.Values
                .FirstOrDefault(s => s.ReplicaIndex == replicaIndex && s.UserIds.Count < _options.MaxUsersPerShard);

            ShardContext ctx;
            if (available != null)
            {
                ctx = available;
            }
            else if (_shards.Count < _options.MaxShardsPerConduit
                     || !_shards.Values.Any(s => s.ReplicaIndex == replicaIndex))
            {
                // Create a new shard when there is global capacity, OR when this replica has no
                // shard yet (never strand a replica with zero shards even at the global cap).
                var shardId = $"shard-{Interlocked.Increment(ref _shardCounter)}";
                var sequencer = CreateShard(shardId);
                ctx = new ShardContext(sequencer) { ReplicaIndex = replicaIndex };
                ctx.PipelineAttachment = _messagePipeline.Attach(sequencer.Messages);
                _shards[shardId] = ctx;
                var created = ctx;
                // Forward each session assignment to the conduit: null→new is an Add, old→new is an Update.
                sequencer.OnSessionAssigned += (_, newSession) =>
                {
                    var old = created.LastSession;
                    created.LastSession = newSession;
                    NotifySessionIdUpdated(shardId, replicaIndex, old, newSession);
                };
                _logger.LogInformation("ShardManager created new shard {ShardId} for replica {Replica} (total={Count})", shardId, replicaIndex, _shards.Count);
                await ConnectShardAsync(sequencer, ct);
            }
            else
            {
                // All shards for this replica are full and we are at the global shard cap —
                // spill into the least-loaded shard OF THIS REPLICA.
                ctx = _shards.Values
                    .Where(s => s.ReplicaIndex == replicaIndex)
                    .OrderBy(s => s.UserIds.Count)
                    .First();
                _logger.LogWarning(
                    "ShardManager at capacity (MaxShardsPerConduit={MaxShards}, MaxUsersPerShard={MaxUsers}); " +
                    "user {UserId} assigned to least-loaded shard of replica {Replica}",
                    _options.MaxShardsPerConduit, _options.MaxUsersPerShard, userId, replicaIndex);
            }

            ctx.UserIds.Add(userId);
            _userToShard[(userId, replicaIndex)] = ctx.Sequencer.ShardId;

            return new ShardBinding(ctx.Sequencer, userId, this);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReleaseUserFromShardAsync(string userId, int replicaIndex, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_userToShard.TryRemove((userId, replicaIndex), out var shardId)) return;
            if (!_shards.TryGetValue(shardId, out var ctx)) return;

            ctx.UserIds.Remove(userId);
            if (ctx.UserIds.Count == 0)
            {
                _shards.TryRemove(shardId, out _);
                _logger.LogInformation("ShardManager disposed empty shard {ShardId}", shardId);
                OnSessionIdUpdated?.Invoke(this, new SessionIdUpdatedArgs
                {
                    ShardId = shardId,
                    ReplicaIndex = ctx.ReplicaIndex,
                    OldSessionId = ctx.Sequencer.SessionId,
                    NewSessionId = null
                });
                ctx.PipelineAttachment?.Dispose();
                await ctx.Sequencer.DisposeAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> OpenReplacementShardAsync(int replicaIndex, CancellationToken ct)
    {
        var (session, _) = await OpenReplacementShardDetailedAsync(replicaIndex, ct);
        return session;
    }

    /// <summary>
    /// Opens a fresh WebSocket shard to reactivate a disabled conduit slot and returns its session id
    /// together with the internal shard id that tracks it.
    /// <para>
    /// Critically, this opener does NOT trigger a conduit Add on the first welcome: the orchestrator's
    /// <c>HandleShardDisabledAsync</c> is the sole conduit writer for recovery and PATCHes the existing
    /// disabled slot. Only the FIRST welcome is suppressed; SUBSEQUENT reconnects of this replacement
    /// shard forward as an Update (old → new) so the same slot is refreshed in place — never a new slot.
    /// </para>
    /// The new shard is tracked in <c>_shards</c> so it is disposed at <see cref="DisposeAsync"/>.
    /// </summary>
    public async Task<(string Session, string InternalShardId)> OpenReplacementShardDetailedAsync(int replicaIndex, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        ShardContext ctx;
        ShardSequencer sequencer;
        string shardId;
        try
        {
            shardId = $"shard-{Interlocked.Increment(ref _shardCounter)}";
            sequencer = CreateShard(shardId);
            ctx = new ShardContext(sequencer) { ReplicaIndex = replicaIndex, IsRecovery = true };
            ctx.PipelineAttachment = _messagePipeline.Attach(sequencer.Messages);
            _shards[shardId] = ctx;
            var created = ctx;
            // Recovery wiring: the FIRST welcome must NOT raise an Add (OldSessionId==null), because the
            // orchestrator PATCHes the existing disabled slot. We only record the first session locally.
            // Any later welcome (a genuine reconnect) forwards as an Update (old != null) of the same slot.
            sequencer.OnSessionAssigned += (_, newSession) =>
            {
                var old = created.LastSession;
                created.LastSession = newSession;
                if (old == null)
                    return; // first welcome → orchestrator owns the initial slot PATCH; do not Add.
                NotifySessionIdUpdated(shardId, replicaIndex, old, newSession);
            };
            _logger.LogInformation("ShardManager opening replacement shard {ShardId} for replica {Replica} (total={Count})", shardId, replicaIndex, _shards.Count);
            await ConnectShardAsync(sequencer, ct);
        }
        finally
        {
            _lock.Release();
        }

        // The welcome (and thus the session id) arrives asynchronously after Connect. Wait briefly for it.
        for (int i = 0; i < 50 && string.IsNullOrEmpty(sequencer.SessionId); i++)
        {
            if (ct.IsCancellationRequested) break;
            await Task.Delay(20, ct);
        }
        return (sequencer.SessionId ?? string.Empty, shardId);
    }

    /// <summary>Creates a shard sequencer. Overridable seam so tests can avoid real socket creation.</summary>
    internal virtual ShardSequencer CreateShard(string shardId) => new(shardId, _logger);

    /// <summary>Opens the shard's WebSocket. Overridable seam so tests can avoid touching the network.</summary>
    internal virtual Task ConnectShardAsync(ShardSequencer shard, CancellationToken ct) =>
        shard.ConnectAsync(BuildWebSocketUri(), ct);

    private Uri BuildWebSocketUri() =>
        new($"{UserBase.DefaultWebSocketUrl}?keepalive_timeout_seconds={_options.KeepaliveTimeoutSeconds}");

    internal void NotifySessionIdUpdated(string shardId, int replicaIndex, string? oldSession, string? newSession)
    {
        OnSessionIdUpdated?.Invoke(this, new SessionIdUpdatedArgs
        {
            ShardId = shardId,
            ReplicaIndex = replicaIndex,
            OldSessionId = oldSession,
            NewSessionId = newSession
        });
    }

    // Test helper
    internal void SimulateSessionIdUpdatedForTest(string userId, int replicaIndex, string sessionId)
    {
        if (_userToShard.TryGetValue((userId, replicaIndex), out var shardId))
        {
            NotifySessionIdUpdated(shardId, replicaIndex, null, sessionId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var ctx in _shards.Values)
        {
            ctx.PipelineAttachment?.Dispose();
            await ctx.Sequencer.DisposeAsync();
        }
        _shards.Clear();
        _lock.Dispose();
    }
}
