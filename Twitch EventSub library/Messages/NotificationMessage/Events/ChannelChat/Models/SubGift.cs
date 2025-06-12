using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class SubGift
    {
        [JsonProperty("duration_months")]
        public int DurationMonths { get; set; }

        [JsonProperty("cumulative_total")]
        public int CumulativeTotal { get; set; }

        [JsonProperty("recipient_user_id")]
        public string RecipientUserId { get; set; }

        [JsonProperty("recipient_user_name")]
        public string RecipientUserName { get; set; }

        [JsonProperty("recipient_user_login")]
        public string RecipientUserLogin { get; set; }

        [JsonProperty("sub_tier")]
        public string SubTier { get; set; }

        [JsonProperty("community_gift_id")]
        public string CommunityGiftId { get; set; }
    }
}