using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class MessagePipelineTests
{
    private static ShardInbound Notif(string broadcasterId, string id = "m") =>
        new("{}", new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
            Payload = new WebSocketNotificationPayload
            { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = broadcasterId } } }
        });

    private static ShardInbound Keepalive() =>
        new("{}", new WebSocketKeepAliveMessage
        { Metadata = new WebSocketMessageMetadata { MessageType = "session_keepalive", MessageId = "k", MessageTimestamp = System.DateTime.UtcNow.ToString("o") } });

    [Fact]
    public async Task Notification_RoutedToOwningUserOnly()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var a = new List<ShardInbound>();
        var b = new List<ShardInbound>();
        pipeline.RegisterUser("A", i => { a.Add(i); return Task.CompletedTask; });
        pipeline.RegisterUser("B", i => { b.Add(i); return Task.CompletedTask; });
        pipeline.Attach(subject);

        subject.OnNext(Notif("A"));
        subject.OnNext(Notif("B"));
        await Task.Delay(50);

        Assert.Single(a);
        Assert.Single(b);
    }

    [Fact]
    public async Task ControlMessage_BroadcastToAllUsers()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        int aCount = 0, bCount = 0;
        pipeline.RegisterUser("A", _ => { Interlocked.Increment(ref aCount); return Task.CompletedTask; });
        pipeline.RegisterUser("B", _ => { Interlocked.Increment(ref bCount); return Task.CompletedTask; });
        pipeline.Attach(subject);

        subject.OnNext(Keepalive());
        await Task.Delay(50);

        Assert.Equal(1, aCount);
        Assert.Equal(1, bCount);
    }

    [Fact]
    public async Task Delivery_PreservesArrivalOrder()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var order = new List<string>();
        pipeline.RegisterUser("A", async i =>
        {
            await Task.Delay(i.Parsed.Metadata!.MessageId == "1" ? 30 : 1);
            lock (order) order.Add(i.Parsed.Metadata!.MessageId);
        });
        pipeline.Attach(subject);

        subject.OnNext(Notif("A", "1"));
        subject.OnNext(Notif("A", "2"));
        subject.OnNext(Notif("A", "3"));
        await Task.Delay(200);

        Assert.Equal(new[] { "1", "2", "3" }, order);
    }
}
