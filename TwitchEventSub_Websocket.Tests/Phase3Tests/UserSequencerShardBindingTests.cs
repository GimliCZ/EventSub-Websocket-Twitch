using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase3Tests;

public class UserSequencerShardBindingTests
{
    [Fact]
    public void UserBase_HasNoSocketField()
    {
        // After refactor: UserBase must NOT own a Socket.
        // Fail first (Socket field exists), then pass after removal.
        var field = typeof(Twitch.EventSub.User.UserBase)
            .GetField("Socket", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.Null(field);
    }

    [Fact]
    public void UserBase_HasReconnectingEntryAsync_AbstractMethod()
    {
        var method = typeof(Twitch.EventSub.User.UserBase)
            .GetMethod("ReconnectingEntryAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.True(method!.IsAbstract);
    }

    [Fact]
    public void UserBase_HasAwaitShardReadyAsync_AbstractMethod()
    {
        var method = typeof(Twitch.EventSub.User.UserBase)
            .GetMethod("AwaitShardReadyAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.True(method!.IsAbstract);
    }
}
