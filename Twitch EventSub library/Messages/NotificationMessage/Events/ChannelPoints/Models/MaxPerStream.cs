using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models
{
    public class MaxPerStream
    {
        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("value")]
        public int Value { get; set; }
    }
}