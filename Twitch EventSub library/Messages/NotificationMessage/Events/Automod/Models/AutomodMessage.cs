using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class AutomodMessage
{
    [JsonProperty("text")]
    public string Text { get; init; } = null!;

    [JsonProperty("fragments")]
    public IReadOnlyList<MessageFragment> Fragments { get; init; } = Array.Empty<MessageFragment>();
}