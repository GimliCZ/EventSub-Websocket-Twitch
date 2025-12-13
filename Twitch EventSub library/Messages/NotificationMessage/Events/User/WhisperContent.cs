using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.User;

public class WhisperContent
{
    [JsonProperty("text")]
    public string Text { get; set; }
}