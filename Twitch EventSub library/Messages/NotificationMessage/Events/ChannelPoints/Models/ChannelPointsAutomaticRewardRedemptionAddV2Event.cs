using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models
{
    public class ChannelPointsAutomaticRewardRedemptionAddV2Event : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; init; } = null!;

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; init; } = null!;

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; init; } = null!;

        [JsonProperty("user_id")]
        public string UserId { get; init; } = null!;

        [JsonProperty("user_login")]
        public string UserLogin { get; init; } = null!;

        [JsonProperty("user_name")]
        public string UserName { get; init; } = null!;

        [JsonProperty("id")]
        public string RedemptionId { get; init; } = null!;

        [JsonProperty("reward")]
        public RewardInfo Reward { get; init; } = null!;

        [JsonProperty("message")]
        public RedemptionMessage? Message { get; init; }

        [JsonProperty("redeemed_at")]
        public DateTimeOffset RedeemedAt { get; init; }
    }
}