using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class SubEvent
    {
        [JsonProperty("sub_tier")]
        public string SubTier { get; set; }

        [JsonProperty("is_prime")]
        public bool IsPrime { get; set; }

        [JsonProperty("duration_months")]
        public int DurationMonths { get; set; }
    }
}