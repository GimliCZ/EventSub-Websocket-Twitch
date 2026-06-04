using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ShardManagerReplicaTests
{
    private sealed class TestableShardManager : ShardManager
    {
        public TestableShardManager(IOptions<EventSubClientOptions> o)
            : base(o, NullLogger<ShardManager>.Instance, new MessagePipeline(NullLogger<MessagePipeline>.Instance)) { }
        internal override ShardSequencer CreateShard(string shardId) => new(shardId, NullLogger.Instance);
        internal override System.Threading.Tasks.Task ConnectShardAsync(ShardSequencer shard, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
    }

    private static IOptions<EventSubClientOptions> Opts() =>
        Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxShardsPerConduit = 10, MaxUsersPerShard = 100 });

    [Fact]
    public async Task SessionUpdate_CarriesReplicaIndex()
    {
        var manager = new TestableShardManager(Opts());
        SessionIdUpdatedArgs? captured = null;
        manager.OnSessionIdUpdated += (_, a) => captured = a;

        await manager.GetOrCreateShardForUserAsync("user-1", replicaIndex: 2, System.Threading.CancellationToken.None);
        manager.SimulateSessionIdUpdatedForTest("user-1", 2, "sess-1");

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.ReplicaIndex);
    }

    [Fact]
    public async Task SameUser_DifferentReplicas_GetDistinctShards()
    {
        var manager = new TestableShardManager(Opts());
        var b0 = await manager.GetOrCreateShardForUserAsync("user-1", 0, System.Threading.CancellationToken.None);
        var b1 = await manager.GetOrCreateShardForUserAsync("user-1", 1, System.Threading.CancellationToken.None);
        Assert.NotEqual(b0.ShardId, b1.ShardId);
    }
}
