namespace Twitch.EventSub;

public interface IConduitOrchestrator
{
    Task InitializeAsync(CancellationToken ct);
    /// <summary>Register a new shard (stable shardId + its current sessionId) with the named replica's conduit.</summary>
    Task AddShardAsync(int replicaIndex, string shardId, string sessionId, CancellationToken ct);
    /// <summary>Update an existing shard's session (old → new sessionId) on the named replica's conduit.</summary>
    Task UpdateShardAsync(int replicaIndex, string shardId, string oldSessionId, string newSessionId, CancellationToken ct);
    /// <summary>Remove/disable a shard from the named replica's conduit.</summary>
    Task RemoveShardAsync(int replicaIndex, string shardId, CancellationToken ct);
    Task TeardownAsync(CancellationToken ct);
    /// <summary>Handle a conduit.shard.disabled notification for the given conduit/shard.</summary>
    Task HandleShardDisabledAsync(string conduitId, string shardId, CancellationToken ct);
    /// <summary>
    /// Supplied by the composition root: opens a fresh WebSocket shard session on the given replica
    /// and returns its session id. Used to reactivate a disabled shard. Null = recovery disabled.
    /// </summary>
    Func<int, CancellationToken, Task<string>>? OpenReplacementSessionAsync { get; set; }
    /// <summary>
    /// Preferred recovery seam: opens a fresh shard and returns both its session id and the internal
    /// shard id tracking it, so the reactivated slot can be remapped (enabling future reconnect Updates
    /// of that slot instead of a new Add). When set, takes precedence over <see cref="OpenReplacementSessionAsync"/>.
    /// </summary>
    Func<int, CancellationToken, Task<(string Session, string InternalShardId)>>? OpenReplacementShardDetailedAsync { get; set; }
    IReadOnlyList<string> ConduitIds { get; }
    string ConduitIdAt(int replicaIndex);
}
