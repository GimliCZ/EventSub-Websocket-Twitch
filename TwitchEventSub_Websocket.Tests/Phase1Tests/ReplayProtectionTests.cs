using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase1Tests;

public class ReplayProtectionTests
{
    [Fact]
    public void IsDuplicate_SameId_ReturnsTrueOnSecondCall()
    {
        var rp = new ReplayProtection(100);
        Assert.False(rp.IsDuplicate("msg-001"));
        Assert.True(rp.IsDuplicate("msg-001"));
    }

    [Fact]
    public void IsDuplicate_DifferentIds_ReturnsFalse()
    {
        var rp = new ReplayProtection(100);
        Assert.False(rp.IsDuplicate("msg-001"));
        Assert.False(rp.IsDuplicate("msg-002"));
    }

    [Fact]
    public void IsDuplicate_ConcurrentCalls_DedupesExactlyOnce()
    {
        var rp = new ReplayProtection(200);
        const string messageId = "concurrent-msg-001";
        int seenCount = 0;

        Parallel.For(0, 20, _ =>
        {
            if (!rp.IsDuplicate(messageId))
                Interlocked.Increment(ref seenCount);
        });

        Assert.Equal(1, seenCount);
    }

    [Fact]
    public void IsDuplicate_BeyondCapacity_OldestEvicted()
    {
        var rp = new ReplayProtection(3);
        rp.IsDuplicate("a");
        rp.IsDuplicate("b");
        rp.IsDuplicate("c");
        // "a" should be evicted; adding it again should return false
        Assert.False(rp.IsDuplicate("a"));
    }

    [Fact]
    public void IsUpToDate_RecentTimestamp_ReturnsTrue()
    {
        var rp = new ReplayProtection(100);
        var recent = DateTime.UtcNow.ToString("O");
        Assert.True(rp.IsUpToDate(recent));
    }

    [Fact]
    public void IsUpToDate_OldTimestamp_ReturnsFalse()
    {
        var rp = new ReplayProtection(100);
        var old = DateTime.UtcNow.AddMinutes(-11).ToString("O");
        Assert.False(rp.IsUpToDate(old));
    }
}
