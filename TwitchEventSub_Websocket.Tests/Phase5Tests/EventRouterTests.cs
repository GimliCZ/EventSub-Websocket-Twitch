using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase5Tests;

public class EventRouterTests
{
    [Fact]
    public void RegisterUser_ThenMessage_RoutesToCorrectCallback()
    {
        var router = new EventRouter();
        bool dispatched = false;
        router.RegisterUser("broadcaster-123", _ => dispatched = true);

        router.OnMessageReceived(BuildNotification("broadcaster-123"));

        Assert.True(dispatched);
    }

    [Fact]
    public void UnregisteredUser_MessageDropped_NoException()
    {
        var router = new EventRouter();
        var ex = Record.Exception((Action)(() => router.OnMessageReceived(BuildNotification("unknown-user"))));
        Assert.Null(ex);
    }

    [Fact]
    public void DuplicateMessageId_DeduplicatedExactlyOnce()
    {
        var rp = new ReplayProtection(100);
        var router = new EventRouter(rp);
        int dispatchCount = 0;
        router.RegisterUser("broadcaster-123", _ => Interlocked.Increment(ref dispatchCount));

        var message = BuildNotification("broadcaster-123", messageId: "msg-dup-001");
        router.OnMessageReceived(message);
        router.OnMessageReceived(message);  // duplicate

        Assert.Equal(1, dispatchCount);
    }

    [Fact]
    public void UnregisterUser_SubsequentMessages_NotRouted()
    {
        var router = new EventRouter();
        bool dispatched = false;
        router.RegisterUser("broadcaster-123", _ => dispatched = true);
        router.UnregisterUser("broadcaster-123");

        router.OnMessageReceived(BuildNotification("broadcaster-123"));
        Assert.False(dispatched);
    }

    [Fact]
    public void CategoryB_UserId_RoutesToCorrectCallback()
    {
        // Category B: user_id routing (whispers, user.update)
        var router = new EventRouter();
        bool dispatched = false;
        router.RegisterUser("user-456", _ => dispatched = true);

        router.OnMessageReceived(BuildNotificationByUserId("user-456"));
        Assert.True(dispatched);
    }

    private static WebSocketNotificationMessage BuildNotification(string broadcasterId, string messageId = "msg-001")
    {
        return new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata
            {
                MessageId = messageId,
                MessageType = "notification",
                MessageTimestamp = DateTime.UtcNow.ToString("O")
            },
            Payload = new WebSocketNotificationPayload
            {
                Subscription = new WebSocketSubscription
                {
                    Id = "sub-1",
                    Type = "channel.follow",
                    Condition = new Condition { BroadcasterUserId = broadcasterId }
                }
            }
        };
    }

    private static WebSocketNotificationMessage BuildNotificationByUserId(string userId, string messageId = "msg-uid-001")
    {
        return new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata
            {
                MessageId = messageId,
                MessageType = "notification",
                MessageTimestamp = DateTime.UtcNow.ToString("O")
            },
            Payload = new WebSocketNotificationPayload
            {
                Subscription = new WebSocketSubscription
                {
                    Id = "sub-2",
                    Type = "user.whisper.message",
                    Condition = new Condition { UserId = userId }
                }
            }
        };
    }
}
