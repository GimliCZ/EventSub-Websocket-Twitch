using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;

public sealed class MessageEmote
{
    [JsonProperty("id")]
    public string Id { get; init; } = null!;
}