using Twitch.EventSub.API.Enums;
using Twitch.EventSub.SubsRegister;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase1Tests;

public class UserUpdateConditionTests
{
    [Fact]
    public void UserUpdate_Subscription_UsesUserIdCondition_NotClientId()
    {
        // Twitch spec: user.update requires user_id condition, not client_id.
        // Regression guard for Register.cs bug at line ~693.
        var reg = Register.GetUserUpdateSubscription();
        Assert.Contains(reg.Conditions, c => c == ConditionTypes.UserId);
        Assert.DoesNotContain(reg.Conditions, c => c == ConditionTypes.ClientId);
    }
}
