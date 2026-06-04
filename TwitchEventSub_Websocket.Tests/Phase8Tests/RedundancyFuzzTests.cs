using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;
using Xunit.Abstractions;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancyFuzzTests
{
    private readonly ITestOutputHelper _out;
    public RedundancyFuzzTests(ITestOutputHelper o) => _out = o;

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task RedundantDeliveries_CollapseToDistinctEvents(int seed)
    {
        var rng = new Random(seed);
        var rp = new ReplayProtection(2000);
        var delivered = new HashSet<string>();
        var expected = new HashSet<string>();
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        pipeline.RegisterUser("1", i =>
        {
            if (i.Parsed is WebSocketNotificationMessage n)
            {
                var key = EventKey.Compute(n);
                if (!rp.IsDuplicateEvent(key)) lock (delivered) delivered.Add(((StreamOnlineEvent)n.Payload!.Event!).StartedAt.ToString("o"));
            }
            return Task.CompletedTask;
        });
        var a = new Subject<ShardInbound>(); var b = new Subject<ShardInbound>();
        pipeline.Attach(a); pipeline.Attach(b);

        ShardInbound Ev(System.DateTime startedAt, string conduit) => new("{}", new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = System.Guid.NewGuid().ToString(), MessageTimestamp = System.DateTime.UtcNow.ToString("o"), SubscriptionType = "stream.online", SubscriptionVersion = "1" },
            Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Type = "stream.online", Version = "1", Condition = new Condition { BroadcasterUserId = "1" }, Transport = new WebSocketTransport { Method = "conduit", ConduitId = conduit } }, Event = new StreamOnlineEvent { BroadcasterUserId = "1", Type = "live", StartedAt = startedAt } }
        });

        for (int i = 0; i < 400; i++)
        {
            var startedAt = new System.DateTime(2026,1,1, rng.Next(0,24), rng.Next(0,60), rng.Next(0,60), System.DateTimeKind.Utc);
            expected.Add(startedAt.ToString("o"));
            a.OnNext(Ev(startedAt, "conduit-A"));
            if (rng.Next(2) == 0) b.OnNext(Ev(startedAt, "conduit-B"));
        }
        await Task.Delay(400);

        Assert.Equal(expected, delivered);
        _out.WriteLine($"seed={seed} distinct={expected.Count}");
    }
}
