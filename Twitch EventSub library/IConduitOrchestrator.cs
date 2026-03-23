namespace Twitch.EventSub;

public interface IConduitOrchestrator
{
    Task InitializeAsync(CancellationToken ct);
    /// <summary>Register a new shard (stable shardId + its current sessionId) with the conduit.</summary>
    Task AddShardAsync(string shardId, string sessionId, CancellationToken ct);
    /// <summary>Update an existing shard's session (old → new sessionId).</summary>
    Task UpdateShardAsync(string shardId, string oldSessionId, string newSessionId, CancellationToken ct);
    /// <summary>Remove/disable a shard from the conduit.</summary>
    Task RemoveShardAsync(string shardId, CancellationToken ct);
    Task TeardownAsync(CancellationToken ct);
    string ConduitId { get; }
}
