using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class SyntheticScenarioTests
{
    private static ShardInbound N(string b, string id) => new("{}", new WebSocketNotificationMessage
    {
        Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
        Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = b } } }
    });
    private static ShardInbound K(string id) => new("{}", new WebSocketKeepAliveMessage
    { Metadata = new WebSocketMessageMetadata { MessageType = "session_keepalive", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") } });

    [Fact]
    public async Task TwoUsers_InterleavedSequence_EachGetsOwnSliceInOrder()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var a = new List<string>(); var b = new List<string>(); int keepA = 0, keepB = 0;
        pipeline.RegisterUser("A", i => { if (i.Parsed is WebSocketKeepAliveMessage) Interlocked.Increment(ref keepA); else lock (a) a.Add(i.Parsed.Metadata!.MessageId); return Task.CompletedTask; });
        pipeline.RegisterUser("B", i => { if (i.Parsed is WebSocketKeepAliveMessage) Interlocked.Increment(ref keepB); else lock (b) b.Add(i.Parsed.Metadata!.MessageId); return Task.CompletedTask; });
        pipeline.Attach(subject);

        foreach (var m in new[] { K("k1"), N("A", "a1"), N("B", "b1"), N("A", "a2"), K("k2"), N("B", "b2") })
            subject.OnNext(m);
        await Task.Delay(100);

        Assert.Equal(new[] { "a1", "a2" }, a);
        Assert.Equal(new[] { "b1", "b2" }, b);
        Assert.Equal(2, keepA);
        Assert.Equal(2, keepB);
    }
}
