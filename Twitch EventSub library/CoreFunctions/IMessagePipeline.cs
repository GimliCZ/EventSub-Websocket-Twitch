namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Routes ordered shard frames to the owning user. Notifications go to the user whose id matches
/// the subscription condition; connection-level control messages broadcast to all registered users.
/// </summary>
public interface IMessagePipeline
{
    void RegisterUser(string userId, Func<ShardInbound, Task> handler);
    void UnregisterUser(string userId);
    /// <summary>
    /// Register the platform-level handler that receives conduit/orchestrator control notifications
    /// (e.g. conduit.shard.disabled) instead of routing them to an individual user.
    /// </summary>
    void RegisterPlatformHandler(Func<Twitch.EventSub.Messages.NotificationMessage.WebSocketNotificationMessage, Task> handler);
    IDisposable Attach(IObservable<ShardInbound> shardStream);
}
