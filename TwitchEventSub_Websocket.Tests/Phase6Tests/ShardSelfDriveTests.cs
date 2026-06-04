using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.SharedContents;
using Twitch.EventSub.Messages.WelcomeMessage;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase6Tests;

/// <summary>
/// Phase 6 (A1): ShardSequencer drives its own state machine from its message stream
/// and announces session assignment so ShardManager can register it with the conduit.
/// </summary>
public class ShardSelfDriveTests
{
    private static WebSocketWelcomeMessage MakeWelcome(string sessionId) => new()
    {
        Metadata = new WebSocketMessageMetadata
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = "session_welcome",
            MessageTimestamp = DateTime.UtcNow.ToString("o")
        },
        Payload = new WebSocketWelcomePayload
        {
            Session = new WebSocketSession { Id = sessionId, KeepAliveTimeoutSeconds = 10 }
        }
    };

    [Fact]
    public async Task HandleWelcome_RaisesOnSessionAssigned()
    {
        var shard = new ShardSequencer("shard-1", NullLogger.Instance);
        await shard.SimulateConnectingForTestAsync();
        string? assigned = null;
        shard.OnSessionAssigned += (_, s) => assigned = s;

        await shard.HandleWelcomeAsync("sess-1");

        Assert.Equal("sess-1", assigned);
    }

    [Fact]
    public async Task ActiveWelcomeMessage_DrivesActive_AndRaisesOnSessionAssigned()
    {
        var shard = new ShardSequencer("shard-1", NullLogger.Instance);
        await shard.SimulateConnectingForTestAsync(); // Disconnected -> WaitingForWelcome
        string? assigned = null;
        shard.OnSessionAssigned += (_, s) => assigned = s;

        await shard.DriveFromMessageAsync(new ShardInbound("{}", MakeWelcome("sess-1")), isPending: false);

        Assert.Equal(ShardSequencer.ShardState.Active, shard.State);
        Assert.Equal("sess-1", shard.SessionId);
        Assert.Equal("sess-1", assigned);
    }

    [Fact]
    public async Task ActiveMessage_IsPublishedToSubscribers()
    {
        var shard = new ShardSequencer("shard-1", NullLogger.Instance);
        await shard.SimulateConnectingForTestAsync();
        ShardInbound? published = null;
        using var sub = shard.Messages.Subscribe(m => published = m);

        await shard.DriveFromMessageAsync(new ShardInbound("{}", MakeWelcome("sess-1")), isPending: false);

        Assert.NotNull(published);
    }

    [Fact]
    public async Task PendingMessages_AreNotPublishedToSubscribers()
    {
        var shard = new ShardSequencer("shard-1", NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();
        await shard.SimulateReconnectingForTestAsync();
        ShardInbound? published = null;
        using var sub = shard.Messages.Subscribe(m => published = m);

        // Welcome arriving on the pending (reconnect) connection completes the reconnect
        // but must not be surfaced to user subscribers.
        await shard.DriveFromMessageAsync(new ShardInbound("{}", MakeWelcome("sess-2")), isPending: true);

        Assert.Null(published);
        Assert.Equal(ShardSequencer.ShardState.Active, shard.State);
        Assert.Equal("sess-2", shard.SessionId);
    }
}
