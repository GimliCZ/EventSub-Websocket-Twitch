using Twitch.EventSub.CoreFunctions;

namespace Twitch.EventSub;

public interface IShardManager : IAsyncDisposable
{
    Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, int replicaIndex, CancellationToken ct);
    Task ReleaseUserFromShardAsync(string userId, int replicaIndex, CancellationToken ct);
    /// <summary>
    /// Opens a fresh WebSocket shard on the given replica and returns its session id once the
    /// welcome assigns one. Used to reactivate a shard the platform reported as disabled.
    /// </summary>
    Task<string> OpenReplacementShardAsync(int replicaIndex, CancellationToken ct);
    /// <summary>
    /// Like <see cref="OpenReplacementShardAsync"/> but also returns the internal shard id tracking the
    /// new connection, so the conduit orchestrator can map the reactivated slot to it for future
    /// reconnect Updates. Recovery shards never trigger a conduit Add on their first welcome.
    /// </summary>
    Task<(string Session, string InternalShardId)> OpenReplacementShardDetailedAsync(int replicaIndex, CancellationToken ct);
    IReadOnlyList<(string ShardId, string? SessionId)> ActiveSessionIds { get; }
    event EventHandler<SessionIdUpdatedArgs> OnSessionIdUpdated;
}
