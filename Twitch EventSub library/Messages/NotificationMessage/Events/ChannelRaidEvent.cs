using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events
{
    public class ChannelRaidEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }
        
        [JsonProperty("from_broadcaster_user_id")]
        public string FromBroadcasterUserId { get; set; }

        [JsonProperty("from_broadcaster_user_login")]
        public string FromBroadcasterUserLogin { get; set; }

        [JsonProperty("from_broadcaster_user_name")]
        public string FromBroadcasterUserName { get; set; }

        [JsonProperty("to_broadcaster_user_id")]
        public string ToBroadcasterUserId { get; set; }

        [JsonProperty("to_broadcaster_user_login")]
        public string ToBroadcasterUserLogin { get; set; }

        [JsonProperty("to_broadcaster_user_name")]
        public string ToBroadcasterUserName { get; set; }

        [JsonProperty("viewers")]
        public int Viewers { get; set; }
    }
}