using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Stream
{
    public class StreamOnlineEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }
        
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("started_at")]
        public DateTime StartedAt { get; set; }
    }
}