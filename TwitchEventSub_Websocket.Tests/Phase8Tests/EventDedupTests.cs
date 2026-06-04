using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class EventDedupTests
{
    [Fact]
    public void IsDuplicateEvent_SecondSameKey_True_FirstFalse()
    {
        var rp = new ReplayProtection(100);
        Assert.False(rp.IsDuplicateEvent("key-1"));
        Assert.True(rp.IsDuplicateEvent("key-1"));
        Assert.False(rp.IsDuplicateEvent("key-2"));
    }

    [Fact]
    public void IsDuplicateEvent_IsIndependentOf_IsDuplicate()
    {
        var rp = new ReplayProtection(100);
        // message-id window and event-key window are separate
        Assert.False(rp.IsDuplicate("same"));
        Assert.False(rp.IsDuplicateEvent("same"));   // same string, different window → not a dup
    }
}
