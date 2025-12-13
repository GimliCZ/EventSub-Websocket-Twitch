using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelBits;

public sealed class PowerUpData
{
    [JsonProperty("type")]
    public string Type { get; init; } = null!; // "message_effect", "celebration", "gigantify_an_emote"

    [JsonProperty("emote")]
    public PowerUpEmote? Emote { get; init; }

    [JsonProperty("message_effect_id")]
    public string? MessageEffectId { get; init; }
}