using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancyProviderTests
{
    [Fact]
    public async Task StartAsync_AcquiresOneShardPerReplica()
    {
        var shardManager = new Mock<IShardManager>();
        shardManager.Setup(m => m.GetOrCreateShardForUserAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Mock.Of<IShardBinding>(b =>
                b.SessionId == "s" &&
                b.ShardStream == System.Reactive.Linq.Observable.Empty<ShardInbound>() &&
                b.NegotiatedKeepaliveSeconds == (int?)10));
        var conduit = new Mock<IConduitOrchestrator>();
        conduit.SetupGet(c => c.ConduitIds).Returns(new[] { "A", "B" });
        conduit.Setup(c => c.ConduitIdAt(It.IsAny<int>())).Returns<int>(i => i == 0 ? "A" : "B");

        var provider = new EventProvider("123", "tok", new List<SubscriptionTypes>(), "cid",
            NullLogger.Instance, allowRecovery: false, new TwitchApi(Mock.Of<IHttpClientFactory>()),
            conduit.Object, "app", shardManager.Object, new ReplayProtection(100),
            new MessagePipeline(NullLogger<MessagePipeline>.Instance), keepAliveTimeoutSeconds: 10,
            redundancyFactor: 2);

        await provider.StartAsync();

        shardManager.Verify(m => m.GetOrCreateShardForUserAsync("123", 0, It.IsAny<CancellationToken>()), Times.Once);
        shardManager.Verify(m => m.GetOrCreateShardForUserAsync("123", 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
