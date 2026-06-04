using System.Collections.Concurrent;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

public sealed class MessagePipeline : IMessagePipeline
{
    private readonly ConcurrentDictionary<string, Func<ShardInbound, Task>> _users = new();
    private readonly ILogger<MessagePipeline>? _logger;
    private Func<WebSocketNotificationMessage, Task>? _platform;

    public MessagePipeline(ILogger<MessagePipeline>? logger = null) => _logger = logger;

    public void RegisterUser(string userId, Func<ShardInbound, Task> handler) => _users[userId] = handler;
    public void UnregisterUser(string userId) => _users.TryRemove(userId, out _);
    public void RegisterPlatformHandler(Func<WebSocketNotificationMessage, Task> handler) => _platform = handler;

    public IDisposable Attach(IObservable<ShardInbound> shardStream) =>
        shardStream.Select(i => Observable.FromAsync(() => HandleAsync(i))).Concat().Subscribe();

    private async Task HandleAsync(ShardInbound inbound)
    {
        try
        {
            if (inbound.Parsed is WebSocketNotificationMessage notification)
            {
                if (notification.Payload?.Subscription?.Type == "conduit.shard.disabled")
                {
                    if (_platform != null) await _platform(notification);
                    return;
                }

                var condition = notification.Payload?.Subscription?.Condition;
                var ownerId = condition?.BroadcasterUserId ?? condition?.UserId;
                if (ownerId != null && _users.TryGetValue(ownerId, out var handler))
                {
                    await handler(inbound);
                }
                else
                {
                    _logger?.LogDebug("MessagePipeline: no user for condition broadcaster={B} user={U}",
                        condition?.BroadcasterUserId, condition?.UserId);
                }
                return;
            }

            foreach (var handler in _users.Values)
                await handler(inbound);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MessagePipeline handler threw for message {Id}", inbound.Parsed.Metadata?.MessageId);
        }
    }
}
