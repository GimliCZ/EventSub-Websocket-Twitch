using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase6Tests;

/// <summary>
/// Phase 6 (A3): EventProvider applies a shard binding to its UserSequencer so the user is
/// actually attached to a live shard instead of failing at the Websocket state.
/// </summary>
public class EventProviderBindingTests
{
    private sealed class FakeShardBinding : IShardBinding
    {
        private readonly Subject<ShardInbound> _subject = new();
        public FakeShardBinding(string shardId, string sessionId) { ShardId = shardId; SessionId = sessionId; }
        public string ShardId { get; }
        public string SessionId { get; }
        public IObservable<ShardInbound> ShardStream => _subject;
        public int? NegotiatedKeepaliveSeconds => null;
        public event EventHandler? OnShardLost { add { } remove { } }
        public event EventHandler<string>? OnSessionIdChanged { add { } remove { } }
        public void Dispose() => _subject.Dispose();
    }

    private static EventProvider CreateProvider(IShardManager shardManager) => new(
        userId: "123",
        accessToken: "user-token",
        listOfSubs: new List<SubscriptionTypes>(),
        clientId: "client",
        logger: NullLogger.Instance,
        allowRecovery: false,
        twitchApi: new TwitchApi(Mock.Of<IHttpClientFactory>()),
        conduitOrchestrator: Mock.Of<IConduitOrchestrator>(),
        appAccessToken: "app-token",
        shardManager: shardManager,
        replayProtection: new ReplayProtection(100),
        messagePipeline: new MessagePipeline(NullLogger<MessagePipeline>.Instance),
        keepAliveTimeoutSeconds: 10,
        redundancyFactor: 1);

    [Fact]
    public void Provider_IsNotConnected_BeforeBinding()
    {
        var provider = CreateProvider(Mock.Of<IShardManager>());
        Assert.False(provider.IsConnected);
    }

    [Fact]
    public void SetShardBinding_AttachesBindingToSequencer_SoUserIsConnected()
    {
        var provider = CreateProvider(Mock.Of<IShardManager>());

        provider.SetShardBinding(new FakeShardBinding("shard-1", "sess-x"));

        Assert.True(provider.IsConnected);
    }
}
