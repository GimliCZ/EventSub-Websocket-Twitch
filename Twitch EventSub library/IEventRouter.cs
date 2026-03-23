using Twitch.EventSub.Messages;

namespace Twitch.EventSub;

/// <summary>
/// Routes incoming WebSocket messages to per-user callbacks.
/// Routing categories:
///   A — broadcaster_user_id (most events)
///   B — user_id (user-scoped events: user.whisper.message, user.update)
///   C — conduit/platform events (conduit.shard.disabled) — handled upstream, not here
/// </summary>
public interface IEventRouter
{
    void RegisterUser(string userId, Action<WebSocketMessage> handler);
    void UnregisterUser(string userId);
    void OnMessageReceived(WebSocketMessage message);
}
