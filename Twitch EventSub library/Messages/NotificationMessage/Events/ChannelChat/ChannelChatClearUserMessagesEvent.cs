using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat
{
    public class ChannelChatClearUserMessagesEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }
        
        [JsonProperty("target_user_id")]
        public string TargetUserId { get; set; }

        [JsonProperty("target_user_name")]
        public string TargetUserName { get; set; }

        [JsonProperty("target_user_login")]
        public string TargetUserLogin { get; set; }
    }
}