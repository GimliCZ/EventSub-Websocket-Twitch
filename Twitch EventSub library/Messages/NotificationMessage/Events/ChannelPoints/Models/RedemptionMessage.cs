using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;

public sealed class RedemptionMessage
{
    [JsonProperty("text")]
    public string Text { get; init; } = null!;

    [JsonProperty("fragments")]
    public IReadOnlyList<MessageFragment> Fragments { get; init; } = Array.Empty<MessageFragment>();
}