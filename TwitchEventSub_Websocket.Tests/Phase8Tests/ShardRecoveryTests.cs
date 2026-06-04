using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.APIConduit;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ShardRecoveryTests
{
    private sealed class StubAccepted : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
    private static TwitchApi Api() => new(Mock.Of<IHttpClientFactory>(f => f.CreateClient(It.IsAny<string>()) == new HttpClient(new StubAccepted())));

    [Fact]
    public async Task HandleShardDisabled_OpensFreshSession_AndPatchesSlot()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        var n = 0;
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => $"conduit-{n++}");
        api.Setup(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        api.Setup(a => a.UpdateConduitShardSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var opts = Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxConduits = 5, RedundancyFactor = 1 });
        var orch = new ConduitOrchestrator(api.Object, opts, NullLogger<ConduitOrchestrator>.Instance, Api());
        // Recovery seam: supply a fresh session opener.
        orch.OpenReplacementSessionAsync = (replicaIndex, ct) => Task.FromResult("new-sess");
        await orch.InitializeAsync(CancellationToken.None);
        await orch.AddShardAsync(0, "shard-1", "old-sess", CancellationToken.None);
        api.Invocations.Clear();

        await orch.HandleShardDisabledAsync("conduit-0", "0", CancellationToken.None);

        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-0", "0",
            It.Is<string>(s => !string.IsNullOrEmpty(s) && s != "old-sess"),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
