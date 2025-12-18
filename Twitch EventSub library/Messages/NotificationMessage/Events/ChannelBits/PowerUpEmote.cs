using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelBits;

public sealed class PowerUpEmote
{
    [JsonProperty("id")]
    public string Id { get; init; } = null!;

    [JsonProperty("name")]
    public string Name { get; init; } = null!;
}