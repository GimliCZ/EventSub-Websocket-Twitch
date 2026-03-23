namespace Twitch.EventSub.APIConduit;

/// <summary>
/// Clean CancellationToken-based abstraction over TwitchApiConduit.
/// ConduitOrchestrator depends on this interface, not the concrete class.
/// </summary>
public interface ITwitchConduitApi
{
    /// <summary>Returns all conduit IDs registered for this application.</summary>
    Task<List<string>> GetConduitIdsAsync(string appAccessToken, string clientId, CancellationToken ct);

    /// <summary>Creates a new conduit with shard_count=1. Returns the conduit ID.</summary>
    Task<string> CreateConduitAsync(string appAccessToken, string clientId, CancellationToken ct);

    /// <summary>
    /// Updates the conduit's shard_count via PATCH /helix/eventsub/conduits.
    /// Increasing adds new (unassigned) shard slots; decreasing removes the highest-indexed slots.
    /// </summary>
    Task UpdateConduitShardCountAsync(string conduitId, int shardCount, string appAccessToken, string clientId, CancellationToken ct);

    /// <summary>
    /// Assigns a WebSocket session to a shard slot via PATCH /helix/eventsub/conduits/shards.
    /// The shard slot (identified by twitchShardIndex "0", "1", ...) must already exist in the conduit.
    /// </summary>
    Task UpdateConduitShardSessionAsync(string conduitId, string twitchShardIndex, string sessionId, string appAccessToken, string clientId, CancellationToken ct);

    /// <summary>Deletes the conduit. Twitch auto-removes all associated subscriptions.</summary>
    Task DeleteConduitAsync(string conduitId, string appAccessToken, string clientId, CancellationToken ct);
}
