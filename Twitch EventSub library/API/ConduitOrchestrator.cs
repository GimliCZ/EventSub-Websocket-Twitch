using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twitch.EventSub.APIConduit;

namespace Twitch.EventSub.API;

public class ConduitOrchestrator : IConduitOrchestrator
{
    private readonly ITwitchConduitApi _api;
    private readonly ILogger<ConduitOrchestrator> _logger;
    private readonly EventSubClientOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Maps internal shardId → (Twitch integer index, current sessionId)
    private readonly Dictionary<string, (string TwitchIndex, string SessionId)> _shardMap = new();
    private int _twitchShardCount;

    public string ConduitId { get; private set; } = string.Empty;

    public ConduitOrchestrator(ITwitchConduitApi api, IOptions<EventSubClientOptions> options, ILogger<ConduitOrchestrator> logger)
    {
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var existing = await _api.GetConduitIdsAsync(_options.AppAccessToken, _options.ClientId, ct);
        if (existing?.Count > 0)
        {
            if (existing.Count >= _options.MaxConduits)
            {
                throw new InvalidOperationException(
                    $"Cannot create a new conduit: client already has {existing.Count} conduits " +
                    $"(Twitch limit: {EventSubClientOptions.TwitchMaxConduits}, configured MaxConduits: {_options.MaxConduits}).");
            }

            ConduitId = existing[0];
            _logger.LogInformation("ConduitOrchestrator reusing existing conduit {ConduitId}", ConduitId);
            // Reset to shard_count=1 to clean up any stale slots from prior run
            await _api.UpdateConduitShardCountAsync(ConduitId, 1, _options.AppAccessToken, _options.ClientId, ct);
        }
        else
        {
            ConduitId = await _api.CreateConduitAsync(_options.AppAccessToken, _options.ClientId, ct)
                ?? throw new InvalidOperationException("Failed to create conduit");
            _logger.LogInformation("ConduitOrchestrator created new conduit {ConduitId}", ConduitId);
        }

        _shardMap.Clear();
        _twitchShardCount = 1;
    }

    public async Task AddShardAsync(string shardId, string sessionId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Next Twitch index = current number of tracked shards (0-based)
            int nextIndex = _shardMap.Count;
            string twitchIndex = nextIndex.ToString();

            if (nextIndex >= _options.MaxShardsPerConduit)
            {
                throw new InvalidOperationException(
                    $"Cannot add shard: conduit {ConduitId} already has {nextIndex} shards " +
                    $"(Twitch limit: {EventSubClientOptions.TwitchMaxShardsPerConduit}, configured MaxShardsPerConduit: {_options.MaxShardsPerConduit}).");
            }

            // Expand conduit's shard_count if the new slot doesn't exist yet
            if (nextIndex >= _twitchShardCount)
            {
                int newCount = nextIndex + 1;
                _logger.LogInformation("ConduitOrchestrator expanding shard_count {Old} → {New} on conduit {ConduitId}", _twitchShardCount, newCount, ConduitId);
                await _api.UpdateConduitShardCountAsync(ConduitId, newCount, _options.AppAccessToken, _options.ClientId, ct);
                _twitchShardCount = newCount;
            }

            _logger.LogInformation("ConduitOrchestrator assigning shard {ShardId} → Twitch index {TwitchIndex}, session={SessionId}, conduit={ConduitId}", shardId, twitchIndex, sessionId, ConduitId);
            await _api.UpdateConduitShardSessionAsync(ConduitId, twitchIndex, sessionId, _options.AppAccessToken, _options.ClientId, ct);
            _shardMap[shardId] = (twitchIndex, sessionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateShardAsync(string shardId, string oldSessionId, string newSessionId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_shardMap.TryGetValue(shardId, out var entry))
            {
                _logger.LogWarning("ConduitOrchestrator UpdateShardAsync: unknown shardId {ShardId}", shardId);
                return;
            }

            _logger.LogInformation("ConduitOrchestrator updating shard {ShardId} (Twitch index {TwitchIndex}) session {Old} → {New} on conduit {ConduitId}", shardId, entry.TwitchIndex, oldSessionId, newSessionId, ConduitId);
            await _api.UpdateConduitShardSessionAsync(ConduitId, entry.TwitchIndex, newSessionId, _options.AppAccessToken, _options.ClientId, ct);
            _shardMap[shardId] = (entry.TwitchIndex, newSessionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveShardAsync(string shardId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_shardMap.TryGetValue(shardId, out var target))
            {
                _logger.LogWarning("ConduitOrchestrator RemoveShardAsync: unknown shardId {ShardId}", shardId);
                return;
            }

            // Find the shard occupying the last Twitch slot (index = _twitchShardCount - 1)
            int lastIndex = _twitchShardCount - 1;
            string lastIndexStr = lastIndex.ToString();
            var lastEntry = _shardMap.FirstOrDefault(kv => kv.Value.TwitchIndex == lastIndexStr);

            bool targetIsLast = target.TwitchIndex == lastIndexStr;

            if (!targetIsLast && lastEntry.Key != null)
            {
                // Swap: copy last slot's session into the freed slot, then update mapping
                _logger.LogInformation("ConduitOrchestrator swapping last shard {LastShardId} (Twitch index {LastIndex}) into freed slot {FreedIndex} on conduit {ConduitId}", lastEntry.Key, lastIndexStr, target.TwitchIndex, ConduitId);
                await _api.UpdateConduitShardSessionAsync(ConduitId, target.TwitchIndex, lastEntry.Value.SessionId, _options.AppAccessToken, _options.ClientId, ct);
                _shardMap[lastEntry.Key] = (target.TwitchIndex, lastEntry.Value.SessionId);
            }

            _shardMap.Remove(shardId);

            // Reduce shard_count by 1 to drop the now-vacated last slot
            int newCount = _twitchShardCount - 1;
            if (newCount >= 1)
            {
                _logger.LogInformation("ConduitOrchestrator reducing shard_count {Old} → {New} on conduit {ConduitId}", _twitchShardCount, newCount, ConduitId);
                await _api.UpdateConduitShardCountAsync(ConduitId, newCount, _options.AppAccessToken, _options.ClientId, ct);
                _twitchShardCount = newCount;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task TeardownAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ConduitId)) return;
        _logger.LogInformation("ConduitOrchestrator teardown: deleting conduit {ConduitId} (Twitch auto-removes subscriptions)", ConduitId);
        await _api.DeleteConduitAsync(ConduitId, _options.AppAccessToken, _options.ClientId, ct);
        _shardMap.Clear();
        _twitchShardCount = 0;
        ConduitId = string.Empty;
    }
}
