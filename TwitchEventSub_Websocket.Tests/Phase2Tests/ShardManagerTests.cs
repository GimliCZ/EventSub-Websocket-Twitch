using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase2Tests;

public class ShardManagerTests
{
    private static IOptions<EventSubClientOptions> DefaultOptions() =>
        Options.Create(new EventSubClientOptions { ClientId = "test", AppAccessToken = "token", MaxShardsPerConduit = 5 });

    [Fact]
    public async Task GetOrCreateShard_FirstUser_CreatesOneShard()
    {
        var manager = new ShardManager(DefaultOptions(), NullLogger<ShardManager>.Instance);
        var binding = await manager.GetOrCreateShardForUserAsync("user-1", CancellationToken.None);
        Assert.NotNull(binding);
        Assert.Equal(1, manager.ShardCount);
    }

    [Fact]
    public async Task ReleaseUser_LastUserOnShard_ShardDisposed()
    {
        var manager = new ShardManager(DefaultOptions(), NullLogger<ShardManager>.Instance);
        await manager.GetOrCreateShardForUserAsync("user-1", CancellationToken.None);
        Assert.Equal(1, manager.ShardCount);
        await manager.ReleaseUserFromShardAsync("user-1", CancellationToken.None);
        Assert.Equal(0, manager.ShardCount);
    }

    [Fact]
    public async Task ConcurrentAddUsers_DoNotExceedMaxShards()
    {
        var opts = Options.Create(new EventSubClientOptions { ClientId = "test", AppAccessToken = "token", MaxShardsPerConduit = 2 });
        var manager = new ShardManager(opts, NullLogger<ShardManager>.Instance);
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(() => manager.GetOrCreateShardForUserAsync($"user-{i}", CancellationToken.None)))
            .ToArray();
        await Task.WhenAll(tasks);
        Assert.True(manager.ShardCount <= 2);
    }

    [Fact]
    public async Task SessionIdUpdated_FiredWhenSimulatedActive()
    {
        var manager = new ShardManager(DefaultOptions(), NullLogger<ShardManager>.Instance);
        SessionIdUpdatedArgs? received = null;
        manager.OnSessionIdUpdated += (_, args) => received = args;
        await manager.GetOrCreateShardForUserAsync("user-1", CancellationToken.None);
        manager.SimulateSessionIdUpdatedForTest("user-1", "session-xyz");
        Assert.NotNull(received);
        Assert.Equal("session-xyz", received!.NewSessionId);
    }
}
