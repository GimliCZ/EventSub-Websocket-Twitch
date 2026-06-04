using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class EventKeyTests
{
    private static WebSocketNotificationMessage Msg(string broadcaster, System.DateTime startedAt, string conduitId, string messageId) => new()
    {
        Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = messageId, MessageTimestamp = "2026-01-01T00:00:00Z", SubscriptionType = "stream.online", SubscriptionVersion = "1" },
        Payload = new WebSocketNotificationPayload
        {
            Subscription = new WebSocketSubscription { Type = "stream.online", Version = "1", Condition = new Condition { BroadcasterUserId = broadcaster },
                Transport = new WebSocketTransport { Method = "conduit", ConduitId = conduitId } },
            Event = new StreamOnlineEvent { BroadcasterUserId = broadcaster, Type = "live", StartedAt = startedAt }
        }
    };

    [Fact]
    public void SameEvent_DifferentConduitAndMessageId_ProducesSameKey()
    {
        var t = new System.DateTime(2026, 1, 1, 10, 0, 0, System.DateTimeKind.Utc);
        var a = EventKey.Compute(Msg("1", t, "conduit-A", "msg-A"));
        var b = EventKey.Compute(Msg("1", t, "conduit-B", "msg-B"));
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentEvent_ProducesDifferentKey()
    {
        var a = EventKey.Compute(Msg("1", new System.DateTime(2026,1,1,10,0,0,System.DateTimeKind.Utc), "conduit-A", "m1"));
        var b = EventKey.Compute(Msg("1", new System.DateTime(2026,1,1,11,0,0,System.DateTimeKind.Utc), "conduit-A", "m2"));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DifferentBroadcaster_ProducesDifferentKey()
    {
        var t = new System.DateTime(2026,1,1,10,0,0,System.DateTimeKind.Utc);
        var a = EventKey.Compute(Msg("1", t, "conduit-A", "m1"));
        var b = EventKey.Compute(Msg("2", t, "conduit-A", "m2"));
        Assert.NotEqual(a, b);
    }
}
