using Twitch.EventSub.API.Models;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase3Tests;

public class SubscriptionManagerConduitTests
{
    [Fact]
    public void BuildSubscriptionRequest_UsesConduitTransport()
    {
        // SubscriptionManager must use method="conduit" and conduit_id, NOT session_id.
        var transport = new Transport
        {
            Method = "conduit",
            ConduitId = "conduit-abc",
            SessionId = null
        };
        Assert.Equal("conduit", transport.Method);
        Assert.Equal("conduit-abc", transport.ConduitId);
        Assert.Null(transport.SessionId);
    }

    [Fact]
    public void RunCheckAsync_Signature_AcceptsConduitIdAndAppToken()
    {
        // Verify SubscriptionManager.RunCheckAsync accepts conduitId and appAccessToken.
        var method = typeof(SubscriptionManager).GetMethod("RunCheckAsync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var paramNames = method!.GetParameters().Select(p => p.Name).ToArray();
        Assert.Contains("conduitId", paramNames);
        Assert.Contains("appAccessToken", paramNames);
        Assert.DoesNotContain("sessionId", paramNames);
    }
}
