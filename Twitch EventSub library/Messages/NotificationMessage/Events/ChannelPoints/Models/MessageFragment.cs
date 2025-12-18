using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;

public sealed class MessageFragment
{
    [JsonProperty("type")]
    public string Type { get; init; } = null!; // text | emote

    [JsonProperty("text")]
    public string Text { get; init; } = null!;

    [JsonProperty("emote")]
    public MessageEmote? Emote { get; init; }
}