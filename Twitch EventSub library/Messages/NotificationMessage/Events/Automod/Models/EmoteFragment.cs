using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class EmoteFragment
{
    [JsonProperty("id")]
    public string Id { get; init; } = null!;

    [JsonProperty("emote_set_id")]
    public string EmoteSetId { get; init; } = null!;
}