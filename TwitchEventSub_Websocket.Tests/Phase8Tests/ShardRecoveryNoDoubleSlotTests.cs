using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.APIConduit;
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ShardRecoveryNoDoubleSlotTests
{
    private sealed class StubAccepted : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
    private static TwitchApi Api() => new(Mock.Of<IHttpClientFactory>(f => f.CreateClient(It.IsAny<string>()) == new HttpClient(new StubAccepted())));

    private static Mock<ITwitchConduitApi> NewApi()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        var n = 0;
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => $"conduit-{n++}");
        api.Setup(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        api.Setup(a => a.UpdateConduitShardSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return api;
    }

    [Fact]
    public async Task HandleShardDisabled_DoesNotExpandShardCount_NoSecondSlot()
    {
        var api = NewApi();
        var opts = Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxConduits = 5, RedundancyFactor = 1 });
        var orch = new ConduitOrchestrator(api.Object, opts, NullLogger<ConduitOrchestrator>.Instance, Api());
        orch.OpenReplacementSessionAsync = (replicaIndex, ct) => Task.FromResult("new-sess");
        await orch.InitializeAsync(CancellationToken.None);
        await orch.AddShardAsync(0, "shard-1", "old-sess", CancellationToken.None); // slot 0
        api.Invocations.Clear();

        await orch.HandleShardDisabledAsync("conduit-0", "0", CancellationToken.None);

        // EXACTLY ONE conduit write: the PATCH of slot 0. No shard_count expansion, no second slot PATCH.
        api.Verify(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-0", "0", "new-sess", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.UpdateConduitShardSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ShardManager production opener: must NOT raise an Add (OldSessionId==null) ────────────

    private sealed class TestableShardManager : ShardManager
    {
        public TestableShardManager(IOptions<EventSubClientOptions> options, IMessagePipeline pipeline)
            : base(options, NullLogger<ShardManager>.Instance, pipeline) { }

        public ShardSequencer? LastCreated { get; private set; }

        internal override ShardSequencer CreateShard(string shardId)
        {
            LastCreated = new ShardSequencer(shardId, NullLogger.Instance);
            return LastCreated;
        }

        internal override Task ConnectShardAsync(ShardSequencer shard, CancellationToken ct) => Task.CompletedTask;
    }

    private static IMessagePipeline NoopPipeline()
    {
        var p = new Mock<IMessagePipeline>();
        p.Setup(x => x.Attach(It.IsAny<IObservable<ShardInbound>>())).Returns(Mock.Of<IDisposable>());
        return p.Object;
    }

    [Fact]
    public async Task ProductionOpener_FirstWelcome_DoesNotRaiseAddEvent()
    {
        var opts = Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t" });
        var mgr = new TestableShardManager(opts, NoopPipeline());

        var raised = new List<SessionIdUpdatedArgs>();
        mgr.OnSessionIdUpdated += (_, a) => raised.Add(a);

        var openTask = mgr.OpenReplacementShardAsync(0, CancellationToken.None);

        // Simulate the new shard's welcome (drives SessionId + OnSessionAssigned).
        var seq = mgr.LastCreated!;
        await seq.SimulateConnectingForTestAsync();    // Disconnected → WaitingForWelcome
        await seq.HandleWelcomeAsync("recovery-sess"); // sets SessionId + fires OnSessionAssigned

        var session = await openTask;
        Assert.Equal("recovery-sess", session);

        // The opener returned a session, but NO Add-shaped event (OldSessionId==null) was raised.
        Assert.DoesNotContain(raised, a => a.OldSessionId == null && a.NewSessionId != null);
    }

    [Fact]
    public async Task ProductionOpener_TracksShardForDisposal()
    {
        var opts = Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t" });
        var mgr = new TestableShardManager(opts, NoopPipeline());

        int before = mgr.ShardCount;
        var openTask = mgr.OpenReplacementShardAsync(0, CancellationToken.None);
        var seq = mgr.LastCreated!;
        await seq.SimulateConnectingForTestAsync();
        await seq.HandleWelcomeAsync("recovery-sess");
        await openTask;

        Assert.Equal(before + 1, mgr.ShardCount); // tracked so DisposeAsync cleans it up
        await mgr.DisposeAsync();
        Assert.Equal(0, mgr.ShardCount);
    }
}
