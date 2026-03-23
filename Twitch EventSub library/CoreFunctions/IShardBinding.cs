using Twitch.EventSub.Messages;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Decouples UserSequencer from WebSocket ownership.
/// Created by ShardManager; held by UserSequencer.
/// UserMessages provides a pre-filtered stream of WebSocketMessages for this user's
/// broadcaster_user_id (category A) and user_id (category B).
/// </summary>
public interface IShardBinding : IDisposable
{
    string ShardId { get; }
    string SessionId { get; }
    /// <summary>Pre-filtered message stream for this user's broadcaster_user_id / user_id.</summary>
    IObservable<WebSocketMessage> UserMessages { get; }
    /// <summary>Fired when the shard WebSocket goes down unexpectedly.</summary>
    event EventHandler OnShardLost;
    /// <summary>Fired when a reconnect completes and a new session_id is available.</summary>
    event EventHandler<string> OnSessionIdChanged;
}
