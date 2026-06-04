using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.APIConduit;
using Twitch.EventSub.APIConduit.Models.Shared;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class OrphanReconcileTests
{
    private sealed class StubAccepted : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }

    private static TwitchApi BuildTwitchApi()
    {
        var factory = Mock.Of<IHttpClientFactory>(f =>
            f.CreateClient(It.IsAny<string>()) == new HttpClient(new StubAccepted()));
        return new TwitchApi(factory);
    }

    [Fact]
    public async Task Reuse_RebuildsShardCountFromTwitch_NotResetTo1()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string> { "existing-A" });
        api.Setup(a => a.GetAllConduitGetShardsAsync(It.IsAny<string>(), It.IsAny<string>(), "existing-A",
                It.IsAny<CancellationTokenSource>(), It.IsAny<ILogger>(), It.IsAny<SubscriptionStatusTypes>()))
           .ReturnsAsync(new List<ConduitShard>
           {
               new ConduitShard { Id = "0", Status = "enabled" },
               new ConduitShard { Id = "1", Status = "enabled" },
               new ConduitShard { Id = "2", Status = "enabled" },
           });
        api.Setup(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        api.Setup(a => a.UpdateConduitShardSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var opts = Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxConduits = 5, RedundancyFactor = 1 });
        var orch = new ConduitOrchestrator(api.Object, opts, NullLogger<ConduitOrchestrator>.Instance, BuildTwitchApi());

        await orch.InitializeAsync(CancellationToken.None);

        // count rebuilt to 3 → next added shard expands to 4 (index 3), not overwrite slot 1
        await orch.AddShardAsync(0, "new-shard", "sess", CancellationToken.None);
        api.Verify(a => a.UpdateConduitShardCountAsync("existing-A", 4, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
