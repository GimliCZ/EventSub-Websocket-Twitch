using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public sealed class TextBoundary
{
    [JsonProperty("start_pos")]
    public int StartPos { get; init; }

    [JsonProperty("end_pos")]
    public int EndPos { get; init; }
}