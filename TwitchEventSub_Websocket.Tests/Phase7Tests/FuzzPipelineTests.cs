using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;
using Xunit.Abstractions;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class FuzzPipelineTests
{
    private readonly ITestOutputHelper _out;
    public FuzzPipelineTests(ITestOutputHelper o) => _out = o;

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task RandomInterleavings_NoCrossUserLeak_NoDuplicateDelivery(int seed)
    {
        var rng = new Random(seed);
        var users = new[] { "U0", "U1", "U2" };
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var delivered = users.ToDictionary(u => u, _ => new List<string>());
        foreach (var u in users)
        {
            var key = u;
            pipeline.RegisterUser(u, i =>
            {
                if (i.Parsed is WebSocketNotificationMessage)
                    lock (delivered) delivered[key].Add(i.Parsed.Metadata!.MessageId);
                return Task.CompletedTask;
            });
        }
        pipeline.Attach(subject);

        var expected = users.ToDictionary(u => u, _ => new List<string>());
        int n = 500;
        for (int i = 0; i < n; i++)
        {
            var kind = rng.Next(100);
            if (kind < 70)
            {
                var u = users[rng.Next(users.Length)];
                var id = $"m{i}";
                expected[u].Add(id);
                subject.OnNext(new ShardInbound("{}", new WebSocketNotificationMessage
                {
                    Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
                    Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = u } } }
                }));
            }
            else if (kind < 85)
            {
                subject.OnNext(new ShardInbound("{}", new WebSocketKeepAliveMessage
                { Metadata = new WebSocketMessageMetadata { MessageType = "session_keepalive", MessageId = $"k{i}", MessageTimestamp = System.DateTime.UtcNow.ToString("o") } }));
            }
            else
            {
                subject.OnNext(new ShardInbound("{}", new WebSocketNotificationMessage
                {
                    Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = $"x{i}", MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
                    Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = "GHOST" } } }
                }));
            }
        }
        await Task.Delay(300);

        foreach (var u in users)
        {
            Assert.Equal(expected[u], delivered[u]);
            Assert.Equal(delivered[u].Distinct().Count(), delivered[u].Count);
        }
        _out.WriteLine($"seed={seed} ok");
    }
}
