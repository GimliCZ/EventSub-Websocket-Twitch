using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class ResubEvent
    {
        [JsonProperty("cumulative_months")]
        public int CumulativeMonths { get; set; }

        [JsonProperty("duration_months")]
        public int DurationMonths { get; set; }

        [JsonProperty("streak_months")]
        public int StreakMonths { get; set; }

        [JsonProperty("sub_tier")]
        public string SubTier { get; set; }

        [JsonProperty("is_prime")]
        public bool? IsPrime { get; set; }

        [JsonProperty("is_gift")]
        public bool IsGift { get; set; }

        [JsonProperty("gifter_is_anonymous")]
        public bool? GifterIsAnonymous { get; set; }

        [JsonProperty("gifter_user_id")]
        public string GifterUserId { get; set; }

        [JsonProperty("gifter_user_name")]
        public string GifterUserName { get; set; }

        [JsonProperty("gifter_user_login")]
        public string GifterUserLogin { get; set; }
    }
}