using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancySyntheticTests
{
    [Fact]
    public async Task SameEventViaTwoReplicas_DeliveredOnce()
    {
        var rp = new ReplayProtection(100);
        int delivered = 0;
        Task Handler(ShardInbound i)
        {
            if (i.Parsed is WebSocketNotificationMessage n)
            {
                var key = EventKey.Compute(n);
                if (rp.IsDuplicateEvent(key)) return Task.CompletedTask;
                Interlocked.Increment(ref delivered);
            }
            return Task.CompletedTask;
        }

        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        pipeline.RegisterUser("1", Handler);
        var replicaA = new Subject<ShardInbound>();
        var replicaB = new Subject<ShardInbound>();
        pipeline.Attach(replicaA);
        pipeline.Attach(replicaB);

        ShardInbound Copy(string conduit, string msgId) => new("{}", new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = msgId, MessageTimestamp = System.DateTime.UtcNow.ToString("o"), SubscriptionType = "stream.online", SubscriptionVersion = "1" },
            Payload = new WebSocketNotificationPayload
            {
                Subscription = new WebSocketSubscription { Type = "stream.online", Version = "1", Condition = new Condition { BroadcasterUserId = "1" }, Transport = new WebSocketTransport { Method = "conduit", ConduitId = conduit } },
                Event = new StreamOnlineEvent { BroadcasterUserId = "1", Type = "live", StartedAt = new System.DateTime(2026,1,1,10,0,0,System.DateTimeKind.Utc) }
            }
        });

        replicaA.OnNext(Copy("conduit-A", "msg-A"));
        replicaB.OnNext(Copy("conduit-B", "msg-B"));
        await Task.Delay(100);

        Assert.Equal(1, delivered);
    }
}
