using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class CheermoteFragment
{
    [JsonProperty("prefix")]
    public string Prefix { get; init; } = null!;

    [JsonProperty("bits")]
    public int Bits { get; init; }

    [JsonProperty("tier")]
    public int Tier { get; init; }
}