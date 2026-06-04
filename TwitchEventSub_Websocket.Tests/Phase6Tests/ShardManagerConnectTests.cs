using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase6Tests;

/// <summary>
/// Phase 6 (A2): ShardManager opens the socket for a newly created shard and forwards the
/// shard's session assignments to the conduit via OnSessionIdUpdated.
/// </summary>
public class ShardManagerConnectTests
{
    /// <summary>Overrides the network-touching seam so creation/connection can be tested offline.</summary>
    private sealed class TestableShardManager : ShardManager
    {
        public ShardSequencer? LastShard;
        public int ConnectCalls;

        public TestableShardManager(IOptions<EventSubClientOptions> options)
            : base(options, NullLogger<ShardManager>.Instance, new MessagePipeline(NullLogger<MessagePipeline>.Instance)) { }

        internal override ShardSequencer CreateShard(string shardId)
        {
            LastShard = new ShardSequencer(shardId, NullLogger.Instance);
            return LastShard;
        }

        internal override Task ConnectShardAsync(ShardSequencer shard, CancellationToken ct)
        {
            ConnectCalls++;
            return Task.CompletedTask; // do not touch the network
        }
    }

    private static IOptions<EventSubClientOptions> Opts() => Options.Create(new EventSubClientOptions
    {
        ClientId = "test-client",
        AppAccessToken = "test-token",
        MaxShardsPerConduit = 10,
        MaxUsersPerShard = 100
    });

    [Fact]
    public async Task NewShard_IsConnected()
    {
        var manager = new TestableShardManager(Opts());

        await manager.GetOrCreateShardForUserAsync("user-1", replicaIndex: 0, CancellationToken.None);

        Assert.Equal(1, manager.ConnectCalls);
        Assert.NotNull(manager.LastShard);
    }

    [Fact]
    public async Task FirstWelcome_ForwardsAsAdd_WithNullOldSession()
    {
        var manager = new TestableShardManager(Opts());
        SessionIdUpdatedArgs? captured = null;
        manager.OnSessionIdUpdated += (_, a) => captured = a;

        await manager.GetOrCreateShardForUserAsync("user-1", replicaIndex: 0, CancellationToken.None);
        await manager.LastShard!.SimulateConnectingForTestAsync(); // Disconnected -> WaitingForWelcome
        await manager.LastShard.HandleWelcomeAsync("sess-1");       // raises OnSessionAssigned

        Assert.NotNull(captured);
        Assert.Null(captured!.OldSessionId);
        Assert.Equal("sess-1", captured.NewSessionId);
        Assert.Equal(manager.LastShard.ShardId, captured.ShardId);
    }

    [Fact]
    public async Task SecondWelcome_ForwardsAsUpdate_WithPreviousOldSession()
    {
        var manager = new TestableShardManager(Opts());
        var captures = new List<SessionIdUpdatedArgs>();
        manager.OnSessionIdUpdated += (_, a) => captures.Add(a);

        await manager.GetOrCreateShardForUserAsync("user-1", replicaIndex: 0, CancellationToken.None);
        await manager.LastShard!.SimulateConnectingForTestAsync();
        await manager.LastShard.HandleWelcomeAsync("sess-1");
        await manager.LastShard.SimulateReconnectingForTestAsync();        // Active -> Reconnecting
        await manager.LastShard.HandleNewConnectionWelcomeAsync("sess-2"); // raises OnSessionAssigned again

        Assert.Equal(2, captures.Count);
        Assert.Null(captures[0].OldSessionId);
        Assert.Equal("sess-1", captures[1].OldSessionId);
        Assert.Equal("sess-2", captures[1].NewSessionId);
    }
}
