using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models
{
    public class GlobalCooldown
    {
        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("seconds")]
        public int Seconds { get; set; }
    }
}