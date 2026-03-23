using System.Reactive.Linq;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Internal implementation of IShardBinding created by ShardManager per user.
/// UserMessages filters the shard's message stream to only messages for this user:
///   Category A: Payload.Subscription.Condition.BroadcasterUserId == userId
///   Category B: Payload.Subscription.Condition.UserId == userId
/// </summary>
internal class ShardBinding : IShardBinding
{
    private readonly ShardSequencer _sequencer;
    private readonly string _userId;
    private readonly ShardManager _manager;
    // Named handler fields for unsubscription
    private readonly EventHandler<ShardCloseArgs> _closedHandler;
    private readonly EventHandler<SessionIdUpdatedArgs> _sessionHandler;

    public string ShardId => _sequencer.ShardId;
    public string SessionId => _sequencer.SessionId ?? string.Empty;

    public IObservable<WebSocketMessage> UserMessages => _sequencer.Messages
        .Where(msg => IsForUser(msg, _userId));

    public event EventHandler? OnShardLost;
    public event EventHandler<string>? OnSessionIdChanged;

    public ShardBinding(ShardSequencer sequencer, string userId, ShardManager manager)
    {
        _sequencer = sequencer;
        _userId = userId;
        _manager = manager;

        _closedHandler = (_, _) => OnShardLost?.Invoke(this, EventArgs.Empty);
        _sessionHandler = (_, args) =>
        {
            if (args.ShardId == _sequencer.ShardId && args.NewSessionId != null)
                OnSessionIdChanged?.Invoke(this, args.NewSessionId);
        };

        _sequencer.OnClosed += _closedHandler;
        _manager.OnSessionIdUpdated += _sessionHandler;
    }

    public void Dispose()
    {
        _sequencer.OnClosed -= _closedHandler;
        _manager.OnSessionIdUpdated -= _sessionHandler;
    }

    private static bool IsForUser(WebSocketMessage msg, string userId)
    {
        if (msg is not WebSocketNotificationMessage notification) return false;
        var condition = notification.Payload?.Subscription?.Condition;
        if (condition == null) return false;
        // Category A: broadcaster_user_id matches
        if (condition.BroadcasterUserId == userId) return true;
        // Category B: user_id matches (whispers, user.update, etc.)
        if (condition.UserId == userId) return true;
        return false;
    }
}
