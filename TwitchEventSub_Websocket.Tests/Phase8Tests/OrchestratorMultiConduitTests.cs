using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.APIConduit;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class OrchestratorMultiConduitTests
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

    private static IOptions<EventSubClientOptions> Opts(int redundancy) =>
        Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxConduits = 5, RedundancyFactor = redundancy });

    private static Mock<ITwitchConduitApi> ApiCreatingConduits()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string>());
        var created = 0;
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(() => $"conduit-{created++}");
        api.Setup(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        api.Setup(a => a.UpdateConduitShardSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return api;
    }

    [Fact]
    public async Task Initialize_CreatesRedundancyFactorConduits()
    {
        var api = ApiCreatingConduits();
        var orch = new ConduitOrchestrator(api.Object, Opts(2), NullLogger<ConduitOrchestrator>.Instance, BuildTwitchApi());
        await orch.InitializeAsync(CancellationToken.None);
        Assert.Equal(2, orch.ConduitIds.Count);
        Assert.Equal("conduit-0", orch.ConduitIdAt(0));
        Assert.Equal("conduit-1", orch.ConduitIdAt(1));
    }

    [Fact]
    public async Task AddShard_RoutesToNamedReplica()
    {
        var api = ApiCreatingConduits();
        var orch = new ConduitOrchestrator(api.Object, Opts(2), NullLogger<ConduitOrchestrator>.Instance, BuildTwitchApi());
        await orch.InitializeAsync(CancellationToken.None);
        await orch.AddShardAsync(replicaIndex: 1, "shard-x", "sess-x", CancellationToken.None);
        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-1", "0", "sess-x",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Teardown_DeletesAllReplicas()
    {
        var api = ApiCreatingConduits();
        api.Setup(a => a.DeleteConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var orch = new ConduitOrchestrator(api.Object, Opts(2), NullLogger<ConduitOrchestrator>.Instance, BuildTwitchApi());
        await orch.InitializeAsync(CancellationToken.None);
        await orch.TeardownAsync(CancellationToken.None);
        api.Verify(a => a.DeleteConduitAsync("conduit-0", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.DeleteConduitAsync("conduit-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(orch.ConduitIds);
    }
}
