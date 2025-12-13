using Newtonsoft.Json;
using Twitch.EventSub.API.Models;

namespace Twitch.EventSub.Messages.NotificationMessage.Events
{
    public class ConduitShardDisabledEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        
        public string BroadcasterUserName { get; set; }
        [JsonProperty("conduit_id")]
        public string ConduitId { get; set; }

        [JsonProperty("shard_id")]
        public string ShardId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("transport")]
        public Transport Transport { get; set; }
    }
}