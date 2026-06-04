using Twitch.EventSub.API.Extensions;
using Twitch.EventSub.API.Models;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ConduitShardDisabledConditionTests
{
    [Fact]
    public void SetConduitShardDisabled_SetsTypeClientIdAndConduitId()
    {
        var req = new CreateSubscriptionRequest { Condition = new Condition(), Transport = new Transport() };
        req.SetConduitShardDisabled(clientId: "client-1", conduitId: "conduit-A");

        Assert.Equal("conduit.shard.disabled", req.Type);
        Assert.Equal("1", req.Version);
        Assert.Equal("client-1", req.Condition.ClientId);
        Assert.Equal("conduit-A", req.Condition.ConduitId);
    }
}
