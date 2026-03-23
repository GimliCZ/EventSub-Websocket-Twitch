using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase4Tests;

public class EventSubClientHostedServiceTests
{
    [Fact]
    public void EventSubClient_ImplementsIHostedService()
    {
        Assert.True(typeof(IHostedService).IsAssignableFrom(typeof(EventSubClient)));
    }

    [Fact]
    public void EventSubClient_ImplementsIAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(EventSubClient)));
    }

    [Fact]
    public async Task StartAsync_CallsConduitOrchestratorInitialize()
    {
        var orchestratorMock = new Mock<IConduitOrchestrator>();
        orchestratorMock.Setup(o => o.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        orchestratorMock.SetupGet(o => o.ConduitId).Returns("conduit-1");

        var shardManagerMock = new Mock<IShardManager>();
        shardManagerMock.SetupAdd(m => m.OnSessionIdUpdated += null);

        var client = CreateClientWithMocks(orchestratorMock.Object, shardManagerMock.Object);

        await ((IHostedService)client).StartAsync(CancellationToken.None);

        orchestratorMock.Verify(o => o.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_CallsConduitOrchestratorTeardown()
    {
        var orchestratorMock = new Mock<IConduitOrchestrator>();
        orchestratorMock.Setup(o => o.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        orchestratorMock.Setup(o => o.TeardownAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        orchestratorMock.SetupGet(o => o.ConduitId).Returns("conduit-1");

        var shardManagerMock = new Mock<IShardManager>();
        shardManagerMock.SetupAdd(m => m.OnSessionIdUpdated += null);
        shardManagerMock.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var client = CreateClientWithMocks(orchestratorMock.Object, shardManagerMock.Object);

        await ((IHostedService)client).StartAsync(CancellationToken.None);
        await ((IHostedService)client).StopAsync(CancellationToken.None);

        orchestratorMock.Verify(o => o.TeardownAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static EventSubClient CreateClientWithMocks(IConduitOrchestrator orchestrator, IShardManager shardManager)
    {
        var options = Options.Create(new EventSubClientOptions
        {
            ClientId = "test-client-id",
            AppAccessToken = "test-token"
        });
        var logger = NullLogger<EventSubClient>.Instance;

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new System.Net.Http.HttpClient());
        var twitchApi = new TwitchApi(httpClientFactoryMock.Object);

        return new EventSubClient(options, logger, twitchApi, orchestrator, shardManager);
    }
}
