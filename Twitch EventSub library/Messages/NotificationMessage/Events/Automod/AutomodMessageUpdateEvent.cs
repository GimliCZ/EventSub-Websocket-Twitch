using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod
{
    public class AutomodMessageUpdateEvent : WebSocketNotificationEvent
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("user_name")]
        public string UserName { get; set; }

        [JsonProperty("user_login")]
        public string UserLogin { get; set; }

        [JsonProperty("moderator_user_id")]
        public string ModeratorUserId { get; set; }

        [JsonProperty("moderator_user_login")]
        public string ModeratorUserLogin { get; set; }

        [JsonProperty("moderator_user_name")]
        public string ModeratorUserName { get; set; }

        [JsonProperty("message_id")]
        public string MessageId { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("held_at")]
        public DateTime HeldAt { get; set; }

        [JsonProperty("fragments")]
        public Fragments Fragments { get; set; }
    }
}