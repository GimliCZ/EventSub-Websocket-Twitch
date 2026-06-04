using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class ShardInboundTests
{
    [Fact]
    public void Carries_RawAndParsed()
    {
        var parsed = new WebSocketKeepAliveMessage();
        var inbound = new ShardInbound("{\"raw\":true}", parsed);
        Assert.Equal("{\"raw\":true}", inbound.Raw);
        Assert.Same(parsed, inbound.Parsed);
    }
}
