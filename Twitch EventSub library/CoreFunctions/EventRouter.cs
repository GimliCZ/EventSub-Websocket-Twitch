using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Routes incoming WebSocket messages to the correct per-user callback.
/// Category A: routes by Subscription.Condition.BroadcasterUserId
/// Category B: routes by Subscription.Condition.UserId
/// Uses ReplayProtection for cross-shard deduplication.
/// </summary>
public class EventRouter : IEventRouter
{
    private readonly ConcurrentDictionary<string, Action<WebSocketMessage>> _byBroadcaster = new();
    private readonly ConcurrentDictionary<string, Action<WebSocketMessage>> _byUserId = new();
    private readonly ReplayProtection _replayProtection;
    private readonly ILogger<EventRouter>? _logger;

    public EventRouter(ReplayProtection? replayProtection = null, ILogger<EventRouter>? logger = null)
    {
        _replayProtection = replayProtection ?? new ReplayProtection(100);
        _logger = logger;
    }

    public void RegisterUser(string userId, Action<WebSocketMessage> handler)
    {
        _byBroadcaster[userId] = handler;
        _byUserId[userId] = handler;
    }

    public void UnregisterUser(string userId)
    {
        _byBroadcaster.TryRemove(userId, out _);
        _byUserId.TryRemove(userId, out _);
    }

    public void OnMessageReceived(WebSocketMessage message)
    {
        if (message is not WebSocketNotificationMessage notification) return;

        var messageId = notification.Metadata?.MessageId;
        var timestamp = notification.Metadata?.MessageTimestamp;

        if (messageId == null || timestamp == null) return;
        if (!_replayProtection.IsUpToDate(timestamp))
        {
            _logger?.LogWarning("EventRouter dropped stale message {MessageId}", messageId);
            return;
        }
        if (_replayProtection.IsDuplicate(messageId))
        {
            _logger?.LogDebug("EventRouter dropped duplicate message {MessageId}", messageId);
            return;
        }

        var condition = notification.Payload?.Subscription?.Condition;
        var broadcasterId = condition?.BroadcasterUserId;
        var userId = condition?.UserId;

        Action<WebSocketMessage>? handler = null;
        if (broadcasterId != null) _byBroadcaster.TryGetValue(broadcasterId, out handler);
        if (handler == null && userId != null) _byUserId.TryGetValue(userId, out handler);

        if (handler == null)
        {
            _logger?.LogDebug("EventRouter: no handler for broadcasterId={BId} userId={UId}", broadcasterId, userId);
            return;
        }

        try
        {
            handler(message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "EventRouter handler threw for message {MessageId}", messageId);
        }
    }
}
