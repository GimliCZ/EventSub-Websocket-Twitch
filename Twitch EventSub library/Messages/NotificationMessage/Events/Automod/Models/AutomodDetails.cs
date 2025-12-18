using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class AutomodDetails
{
    [JsonProperty("category")]
    public string Category { get; init; } = null!;

    [JsonProperty("level")]
    public int Level { get; init; }

    [JsonProperty("boundaries")]
    public IReadOnlyList<TextBoundary> Boundaries { get; init; } = Array.Empty<TextBoundary>();
}