using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class CommunitySubGift
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("sub_tier")]
        public string SubTier { get; set; }

        [JsonProperty("cumulative_total")]
        public int CumulativeTotal { get; set; }
    }
}