using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class BlockedTermDetails
{
    [JsonProperty("terms_found")]
    public IReadOnlyList<BlockedTermMatch> TermsFound { get; init; } = Array.Empty<BlockedTermMatch>();
}