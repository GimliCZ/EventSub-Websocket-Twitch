using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShoutout
{
    public class ChannelShoutoutReceivedEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }
        
        [JsonProperty("from_broadcaster_user_id")]
        public string FromBroadcasterUserId { get; set; }

        [JsonProperty("from_broadcaster_user_name")]
        public string FromBroadcasterUserName { get; set; }

        [JsonProperty("from_broadcaster_user_login")]
        public string FromBroadcasterUserLogin { get; set; }

        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }
    }
}