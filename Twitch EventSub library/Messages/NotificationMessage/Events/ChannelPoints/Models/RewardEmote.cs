using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;

public sealed class RewardEmote
{
    [JsonProperty("id")]
    public string Id { get; init; } = null!;

    [JsonProperty("name")]
    public string Name { get; init; } = null!;
}