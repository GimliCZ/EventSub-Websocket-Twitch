using Twitch.EventSub.API;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ConduitReplicaTests
{
    [Fact]
    public void AddShard_AssignsSequentialIndices_AndExpands()
    {
        var r = new ConduitReplica(0, "conduit-A");
        var i0 = r.ReserveShardSlot("shard-1");
        Assert.Equal("0", i0.TwitchIndex);
        Assert.Equal(1, i0.NewShardCount);
        r.TwitchShardCount = i0.NewShardCount;
        r.CommitShard("shard-1", "0", "sess-1");

        var i1 = r.ReserveShardSlot("shard-2");
        Assert.Equal("1", i1.TwitchIndex);
        Assert.Equal(2, i1.NewShardCount);
        r.TwitchShardCount = i1.NewShardCount;
        r.CommitShard("shard-2", "1", "sess-2");

        Assert.Equal(2, r.TwitchShardCount);
        Assert.True(r.TryGetShard("shard-1", out var e1) && e1.SessionId == "sess-1");
    }

    [Fact]
    public void RemoveShard_SwapsLastIntoFreedSlot_AndScalesDown()
    {
        var r = new ConduitReplica(0, "conduit-A");
        r.CommitShard("shard-1", "0", "sess-1"); r.TwitchShardCount = 1;
        r.CommitShard("shard-2", "1", "sess-2"); r.TwitchShardCount = 2;

        var plan = r.PlanRemoval("shard-1");
        Assert.False(plan.TargetIsLast);
        Assert.Equal("0", plan.FreedIndex);
        Assert.Equal("sess-2", plan.LastSessionId);

        r.ApplyRemoval("shard-1", plan);
        Assert.Equal(1, r.TwitchShardCount);
        Assert.True(r.TryGetShard("shard-2", out var moved) && moved.TwitchIndex == "0");
        Assert.False(r.TryGetShard("shard-1", out _));
    }
}
