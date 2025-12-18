using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class BlockedTermMatch
{
    [JsonProperty("term_id")]
    public string TermId { get; init; } = null!;

    [JsonProperty("boundary")]
    public TextBoundary Boundary { get; init; } = null!;

    [JsonProperty("owner_broadcaster_user_id")]
    public string OwnerBroadcasterUserId { get; init; } = null!;

    [JsonProperty("owner_broadcaster_user_login")]
    public string OwnerBroadcasterUserLogin { get; init; } = null!;

    [JsonProperty("owner_broadcaster_user_name")]
    public string OwnerBroadcasterUserName { get; init; } = null!;
}