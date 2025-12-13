using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;

public sealed class RewardInfo
{
    [JsonProperty("type")]
    public string Type { get; init; } = null!; // e.g., single_message_bypass_sub_mode, send_highlighted_message, etc.

    [JsonProperty("channel_points")]
    public int ChannelPoints { get; init; }

    [JsonProperty("emote")]
    public RewardEmote? Emote { get; init; }
}