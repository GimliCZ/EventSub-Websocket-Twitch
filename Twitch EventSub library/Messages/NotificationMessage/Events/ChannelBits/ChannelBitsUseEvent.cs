using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelBits;

public class ChannelBitsUseEvent : WebSocketNotificationEvent
{
    [JsonProperty("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonProperty("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonProperty("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonProperty("user_id")]
    public string UserId { get; init; } = null!;

    [JsonProperty("user_login")]
    public string UserLogin { get; init; } = null!;

    [JsonProperty("user_name")]
    public string UserName { get; init; } = null!;

    [JsonProperty("bits")]
    public int Bits { get; init; }

    [JsonProperty("type")]
    public string Type { get; init; } = null!; // "cheer" | "power_up"

    [JsonProperty("message")]
    public Message? Message { get; init; }

    [JsonProperty("power_up")]
    public PowerUpData? PowerUp { get; init; }
}