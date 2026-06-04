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
    /// <summary>Ordered stream of all frames from this shard's active connection.</summary>
    IObservable<ShardInbound> ShardStream { get; }
    /// <summary>Keepalive seconds negotiated via the shard's Welcome message, if known.</summary>
    int? NegotiatedKeepaliveSeconds { get; }
    /// <summary>Fired when the shard WebSocket goes down unexpectedly.</summary>
    event EventHandler OnShardLost;
    /// <summary>Fired when a reconnect completes and a new session_id is available.</summary>
    event EventHandler<string> OnSessionIdChanged;
}
