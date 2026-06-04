using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class OptionsConfigTests
{
    [Fact]
    public void DedupWindowSize_DefaultsTo100()
    {
        var o = new EventSubClientOptions();
        Assert.Equal(100, o.DedupWindowSize);
    }

    [Fact]
    public void KeepaliveTimeoutSeconds_DefaultsTo10()
    {
        var o = new EventSubClientOptions();
        Assert.Equal(10, o.KeepaliveTimeoutSeconds);
    }
}
