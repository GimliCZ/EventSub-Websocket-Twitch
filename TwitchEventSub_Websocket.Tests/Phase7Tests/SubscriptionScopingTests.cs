using Twitch.EventSub.API.Models;
using Twitch.EventSub.Messages.SharedContents;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class SubscriptionScopingTests
{
    [Fact]
    public void OwnedSlice_MatchesByBroadcasterUserId()
    {
        var all = new List<WebSocketSubscription>
        {
            new() { Id = "a1", Type = "channel.update", Version = "2", Condition = new Condition { BroadcasterUserId = "userA" } },
            new() { Id = "b1", Type = "channel.follow", Version = "2", Condition = new Condition { BroadcasterUserId = "userB" } },
            new() { Id = "a2", Type = "channel.chat.message", Version = "1", Condition = new Condition { BroadcasterUserId = "userA", UserId = "userA" } },
        };

        var slice = SubscriptionManager.OwnedSlice(all, "userA");

        Assert.Equal(new[] { "a1", "a2" }, slice.Select(s => s.Id).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void OwnedSlice_MatchesByUserId_AndModerator()
    {
        var all = new List<WebSocketSubscription>
        {
            new() { Id = "w1", Type = "user.whisper.message", Version = "1", Condition = new Condition { UserId = "userA" } },
            new() { Id = "m1", Type = "channel.follow", Version = "2", Condition = new Condition { BroadcasterUserId = "userB", ModeratorUserId = "userA" } },
            new() { Id = "x1", Type = "channel.update", Version = "2", Condition = new Condition { BroadcasterUserId = "userB" } },
        };

        var slice = SubscriptionManager.OwnedSlice(all, "userA");

        Assert.Equal(new[] { "m1", "w1" }, slice.Select(s => s.Id).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void OwnedSlice_WithConduitId_ExcludesOtherConduitsCopies()
    {
        // Redundancy: the same user+condition sub exists on two conduits. A replica managing
        // conduit-A must NOT see conduit-B's copy as its own (else it would delete it every check).
        var all = new List<WebSocketSubscription>
        {
            new() { Id = "a1", Type = "channel.update", Version = "2",
                Condition = new Condition { BroadcasterUserId = "userA" },
                Transport = new WebSocketTransport { Method = "conduit", ConduitId = "conduit-A" } },
            new() { Id = "b1", Type = "channel.update", Version = "2",
                Condition = new Condition { BroadcasterUserId = "userA" },
                Transport = new WebSocketTransport { Method = "conduit", ConduitId = "conduit-B" } },
        };

        var sliceA = SubscriptionManager.OwnedSlice(all, "userA", "conduit-A");
        var sliceB = SubscriptionManager.OwnedSlice(all, "userA", "conduit-B");

        Assert.Equal(new[] { "a1" }, sliceA.Select(s => s.Id).ToArray());
        Assert.Equal(new[] { "b1" }, sliceB.Select(s => s.Id).ToArray());
    }

    [Fact]
    public void ReconcileReport_RecordsOwnedCount()
    {
        var report = new SubscriptionManager.ReconcileReport(userId: "userA", ownedCount: 2, created: 1, removed: 0);
        Assert.Equal("userA", report.UserId);
        Assert.Equal(2, report.OwnedCount);
        Assert.Equal(1, report.Created);
        Assert.Equal(0, report.Removed);
    }
}
