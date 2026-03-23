using System.ComponentModel.DataAnnotations;

namespace Twitch.EventSub;

public record EventSubClientOptions
{
    /// <summary>Your Twitch application client ID.</summary>
    [MinLength(1)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>App access token for conduit management and subscription creation.</summary>
    [MinLength(1)]
    public string AppAccessToken { get; set; } = string.Empty;

    /// <summary>Keepalive timeout in seconds (10–600). Sent to Twitch WebSocket URL.</summary>
    [Range(10, 600)]
    public int KeepaliveTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Twitch hard limit: maximum number of enabled conduits per client.
    /// Subject to change — see https://dev.twitch.tv/docs/eventsub/eventsub-reference/
    /// </summary>
    public const int TwitchMaxConduits = 5;

    /// <summary>
    /// Twitch hard limit: maximum number of shards per conduit.
    /// Subject to change — see https://dev.twitch.tv/docs/eventsub/eventsub-reference/
    /// </summary>
    public const int TwitchMaxShardsPerConduit = 20_000;

    /// <summary>Operator ceiling on enabled conduits. Must not exceed <see cref="TwitchMaxConduits"/>.</summary>
    [Range(1, TwitchMaxConduits)]
    public int MaxConduits { get; set; } = TwitchMaxConduits;

    /// <summary>Operator ceiling on shard count per conduit. Must not exceed <see cref="TwitchMaxShardsPerConduit"/>.</summary>
    [Range(1, TwitchMaxShardsPerConduit)]
    public int MaxShardsPerConduit { get; set; } = 20000;

    /// <summary>
    /// Maximum number of users assigned to a single WebSocket shard before a new shard is opened.
    /// Keeps reconnect impact isolated: only this many users re-subscribe when one shard drops.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxUsersPerShard { get; set; } = 1000;

    public TimeSpan WelcomeMessageTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan AccessTokenValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan SubscriptionOperationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan WatchdogTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan ReconnectGraceTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
