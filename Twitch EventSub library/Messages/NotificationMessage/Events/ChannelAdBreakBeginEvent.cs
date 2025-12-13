using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events
{
    public class ChannelAdBreakBeginEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }
        
        [JsonProperty("duration_seconds")]
        public string DurationSeconds { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("is_automatic")]
        public string IsAutomatic { get; set; }

        [JsonProperty("requester_user_id")]
        public string RequesterUserId { get; set; }

        [JsonProperty("requester_user_login")]
        public string RequesterUserLogin { get; set; }

        [JsonProperty("requester_user_name")]
        public string RequesterUserName { get; set; }
    }
}