using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ShardDisabledRoutingTests
{
    private static ShardInbound Disabled(string conduitId, string shardId) => new("{}", new WebSocketNotificationMessage
    {
        Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = "d1", MessageTimestamp = System.DateTime.UtcNow.ToString("o"), SubscriptionType = "conduit.shard.disabled" },
        Payload = new WebSocketNotificationPayload
        {
            Subscription = new WebSocketSubscription { Type = "conduit.shard.disabled", Condition = new Condition { ClientId = "c", ConduitId = conduitId } },
            Event = new ConduitShardDisabledEvent { ConduitId = conduitId, ShardId = shardId, Status = "websocket_disconnected" }
        }
    });

    [Fact]
    public async Task ShardDisabled_RoutesToPlatformHandler_NotUser()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        bool userGot = false; (string c, string s)? platform = null;
        pipeline.RegisterUser("anyone", _ => { userGot = true; return Task.CompletedTask; });
        pipeline.RegisterPlatformHandler(n =>
        {
            var ev = (ConduitShardDisabledEvent)n.Payload!.Event!;
            platform = (ev.ConduitId, ev.ShardId);
            return Task.CompletedTask;
        });
        pipeline.Attach(subject);

        subject.OnNext(Disabled("conduit-A", "3"));
        await Task.Delay(60);

        Assert.False(userGot);
        Assert.Equal(("conduit-A", "3"), platform);
    }
}
