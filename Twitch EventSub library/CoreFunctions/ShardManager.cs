using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<string, ShardContext> _shards = new();
    private readonly ConcurrentDictionary<string, string> _userToShard = new();  // userId → shardId
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _shardCounter;

    public event EventHandler<SessionIdUpdatedArgs>? OnSessionIdUpdated;

    public ShardManager(IOptions<EventSubClientOptions> options, ILogger<ShardManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<(string ShardId, string? SessionId)> ActiveSessionIds =>
        _shards.Select(kv => (kv.Key, kv.Value.Sequencer.SessionId)).ToList();

    /// <summary>Current number of active shards. Exposed for tests and monitoring.</summary>
    public int ShardCount => _shards.Count;

    public async Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Find a shard that still has room for one more user.
            var available = _shards.Values.FirstOrDefault(s => s.UserIds.Count < _options.MaxUsersPerShard);

            ShardContext ctx;
            if (available != null)
            {
                ctx = available;
            }
            else if (_shards.Count < _options.MaxShardsPerConduit)
            {
                var shardId = $"shard-{Interlocked.Increment(ref _shardCounter)}";
                var sequencer = new ShardSequencer(shardId, _logger);
                ctx = new ShardContext(sequencer);
                _shards[shardId] = ctx;
                _logger.LogInformation("ShardManager created new shard {ShardId} (total={Count})", shardId, _shards.Count);
            }
            else
            {
                // All shards full and at the shard cap — spill into least-loaded shard.
                ctx = _shards.Values.OrderBy(s => s.UserIds.Count).First();
                _logger.LogWarning(
                    "ShardManager at capacity (MaxShardsPerConduit={MaxShards}, MaxUsersPerShard={MaxUsers}); " +
                    "user {UserId} assigned to least-loaded shard",
                    _options.MaxShardsPerConduit, _options.MaxUsersPerShard, userId);
            }

            ctx.UserIds.Add(userId);
            _userToShard[userId] = ctx.Sequencer.ShardId;

            return new ShardBinding(ctx.Sequencer, userId, this);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReleaseUserFromShardAsync(string userId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_userToShard.TryRemove(userId, out var shardId)) return;
            if (!_shards.TryGetValue(shardId, out var ctx)) return;

            ctx.UserIds.Remove(userId);
            if (ctx.UserIds.Count == 0)
            {
                _shards.TryRemove(shardId, out _);
                _logger.LogInformation("ShardManager disposed empty shard {ShardId}", shardId);
                OnSessionIdUpdated?.Invoke(this, new SessionIdUpdatedArgs
                {
                    ShardId = shardId,
                    OldSessionId = ctx.Sequencer.SessionId,
                    NewSessionId = null
                });
                await ctx.Sequencer.DisposeAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    internal void NotifySessionIdUpdated(string shardId, string? oldSession, string? newSession)
    {
        OnSessionIdUpdated?.Invoke(this, new SessionIdUpdatedArgs
        {
            ShardId = shardId,
            OldSessionId = oldSession,
            NewSessionId = newSession
        });
    }

    // Test helper
    internal void SimulateSessionIdUpdatedForTest(string userId, string sessionId)
    {
        if (_userToShard.TryGetValue(userId, out var shardId))
        {
            NotifySessionIdUpdated(shardId, null, sessionId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var ctx in _shards.Values)
            await ctx.Sequencer.DisposeAsync();
        _shards.Clear();
        _lock.Dispose();
    }
}
