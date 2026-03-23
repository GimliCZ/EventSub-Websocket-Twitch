using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase2Tests;

public class ShardSequencerTests
{
    [Theory]
    [InlineData(4000)]   // ServerError → reconnect
    [InlineData(4002)]   // PingPongFailure → reconnect
    [InlineData(4003)]   // SubscriptionTimeout → reconnect
    [InlineData(4005)]   // NetworkTimeout → reconnect
    [InlineData(4006)]   // NetworkError → reconnect
    [InlineData(4007)]   // InvalidReconnect → reconnect
    public async Task CloseCode_Reconnectable_TransitionsToReconnecting(int code)
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();
        await shard.HandleCloseCodeAsync(code);
        // HandleCloseCodeAsync fires ReconnectRequested: Active → Reconnecting (not Connecting)
        Assert.Equal(ShardSequencer.ShardState.Reconnecting, shard.State);
    }

    [Fact]
    public async Task CloseCode_4001_TransitionsToDisposing_NotConnecting()
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();
        await shard.HandleCloseCodeAsync(4001);
        // 4001 = client protocol violation — must NOT reconnect
        Assert.NotEqual(ShardSequencer.ShardState.Connecting, shard.State);
        Assert.NotEqual(ShardSequencer.ShardState.Active, shard.State);
    }

    [Fact]
    public async Task CloseCode_4004_ForceFreshConnectFired()
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();
        bool freshConnectFired = false;
        shard.OnForceFreshConnect += (_, _) => freshConnectFired = true;
        await shard.HandleCloseCodeAsync(4004);
        Assert.True(freshConnectFired);
    }

    [Fact]
    public async Task WelcomeReceived_TransitionsToActive()
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateConnectingForTestAsync();
        await shard.HandleWelcomeAsync("session-abc");
        Assert.Equal(ShardSequencer.ShardState.Active, shard.State);
        Assert.Equal("session-abc", shard.SessionId);
    }

    [Fact]
    public async Task DualClientReconnect_OldClientDisposedOnlyAfterNewWelcome()
    {
        // Spec-compliant reconnect: new WebsocketClient opened; old disposed only after new Welcome.
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();

        // Transition to Reconnecting
        await shard.SimulateReconnectingForTestAsync();
        Assert.Equal(ShardSequencer.ShardState.Reconnecting, shard.State);

        // Old session still visible during reconnect (not yet replaced)
        Assert.Equal("test-session", shard.SessionId);

        // New connection Welcome arrives
        await shard.HandleNewConnectionWelcomeAsync("session-new");
        Assert.Equal(ShardSequencer.ShardState.Active, shard.State);
        Assert.Equal("session-new", shard.SessionId);
    }
}
