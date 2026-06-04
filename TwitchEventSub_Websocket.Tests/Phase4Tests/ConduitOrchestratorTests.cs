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

namespace TwitchEventSub_Websocket.Tests.Phase4Tests;

public class ConduitOrchestratorTests
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

    private static ConduitOrchestrator BuildOrchestrator(ITwitchConduitApi api)
    {
        var options = Options.Create(new EventSubClientOptions
        {
            ClientId = "client-id",
            AppAccessToken = "app-token"
        });
        return new ConduitOrchestrator(api, options, NullLogger<ConduitOrchestrator>.Instance, BuildTwitchApi());
    }

    // ── Initialization ────────────────────────────────────────────────────────

    [Fact]
    public async Task Initialize_ExistingConduit_ReusesItWithoutCreatingNew()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string> { "existing-conduit-id" });
        api.Setup(a => a.GetAllConduitGetShardsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationTokenSource>(), It.IsAny<ILogger>(), It.IsAny<SubscriptionStatusTypes>()))
           .ReturnsAsync(new List<ConduitShard>());

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);

        api.Verify(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("existing-conduit-id", orch.ConduitIdAt(0));
        // No longer resets shard_count=1 on reuse — instead reconciles from Twitch shard list
        api.Verify(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Initialize_NoExistingConduit_CreatesNew()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string>());
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync("new-conduit-id");

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);

        api.Verify(a => a.CreateConduitAsync("app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("new-conduit-id", orch.ConduitIdAt(0));
    }

    // ── AddShardAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddShard_FirstShard_UsesIndex0WithoutExpandingCount()
    {
        var api = SetupInitializedApi("conduit-abc");

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);

        await orch.AddShardAsync(0, "shard-1", "session-A", CancellationToken.None);

        // shard_count starts at 1; index 0 already exists → no expansion needed
        api.Verify(a => a.UpdateConduitShardCountAsync("conduit-abc", It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-abc", "0", "session-A", "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddShard_SecondShard_ExpandsShardCountThenAssignsIndex1()
    {
        var api = SetupInitializedApi("conduit-abc");

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);
        await orch.AddShardAsync(0, "shard-1", "session-A", CancellationToken.None);
        await orch.AddShardAsync(0, "shard-2", "session-B", CancellationToken.None);

        // Second shard requires expanding to shard_count=2
        api.Verify(a => a.UpdateConduitShardCountAsync("conduit-abc", 2, "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-abc", "1", "session-B", "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateShardAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateShard_UsesStoredTwitchIndex()
    {
        var api = SetupInitializedApi("conduit-abc");

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);
        await orch.AddShardAsync(0, "shard-1", "session-A", CancellationToken.None);

        await orch.UpdateShardAsync(0, "shard-1", "session-A", "session-B", CancellationToken.None);

        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-abc", "0", "session-B", "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── RemoveShardAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveShard_OnlyOneShard_ReducesCountTo1WithoutSwap()
    {
        var api = SetupInitializedApi("conduit-abc");

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);
        await orch.AddShardAsync(0, "shard-1", "session-A", CancellationToken.None);

        api.Invocations.Clear();

        await orch.RemoveShardAsync(0, "shard-1", CancellationToken.None);

        // No swap needed (target IS the last slot); count goes 1 → 0 but clamped at >=1
        api.Verify(a => a.UpdateConduitShardSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        // shard_count would become 0 which is invalid — orchestrator must not call UpdateConduitShardCountAsync below 1
        api.Verify(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveShard_NotLastSlot_SwapsLastSessionIntoFreedSlotThenReducesCount()
    {
        var api = SetupInitializedApi("conduit-abc");

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);
        await orch.AddShardAsync(0, "shard-1", "session-A", CancellationToken.None); // index 0
        await orch.AddShardAsync(0, "shard-2", "session-B", CancellationToken.None); // index 1

        api.Invocations.Clear();

        // Remove index-0 shard; shard-2 (index 1, last) must be swapped into slot 0
        await orch.RemoveShardAsync(0, "shard-1", CancellationToken.None);

        // Swap last session into freed slot
        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-abc", "0", "session-B", "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
        // Reduce shard_count 2 → 1
        api.Verify(a => a.UpdateConduitShardCountAsync("conduit-abc", 1, "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── TeardownAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Teardown_DeletesConduit()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string> { "conduit-1" });
        api.Setup(a => a.GetAllConduitGetShardsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationTokenSource>(), It.IsAny<ILogger>(), It.IsAny<SubscriptionStatusTypes>()))
           .ReturnsAsync(new List<ConduitShard>());
        api.Setup(a => a.DeleteConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);

        var orch = BuildOrchestrator(api.Object);
        await orch.InitializeAsync(CancellationToken.None);
        await orch.TeardownAsync(CancellationToken.None);

        api.Verify(a => a.DeleteConduitAsync("conduit-1", "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(orch.ConduitIds);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mock<ITwitchConduitApi> SetupInitializedApi(string conduitId)
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string>());
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(conduitId);
        api.Setup(a => a.UpdateConduitShardCountAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        api.Setup(a => a.UpdateConduitShardSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        return api;
    }
}
