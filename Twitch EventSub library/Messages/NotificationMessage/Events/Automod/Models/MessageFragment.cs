using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class MessageFragment
{
    [JsonProperty("type")]
    public string Type { get; init; } = null!; // text | emote | cheermote

    [JsonProperty("text")]
    public string Text { get; init; } = null!;

    [JsonProperty("emote")]
    public EmoteFragment? Emote { get; init; }

    [JsonProperty("cheermote")]
    public CheermoteFragment? Cheermote { get; init; }
}