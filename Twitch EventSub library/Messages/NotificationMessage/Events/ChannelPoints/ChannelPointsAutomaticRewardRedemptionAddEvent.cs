using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints
{
    public class ChannelPointsAutomaticRewardRedemptionAddEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }
        
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("user_name")]
        public string UserName { get; set; }

        [JsonProperty("user_login")]
        public string UserLogin { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("reward")]
        public RewardAutomatic Reward { get; set; }

        [JsonProperty("message")]
        public Message Message { get; set; }

        [JsonProperty("user_input")]
        public string UserInput { get; set; }

        [JsonProperty("redeemed_at")]
        public string RedeemedAt { get; set; }
    }
}