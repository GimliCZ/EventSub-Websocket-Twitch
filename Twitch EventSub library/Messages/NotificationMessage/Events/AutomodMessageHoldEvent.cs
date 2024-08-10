﻿using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events
{
    public class AutomodMessageHoldEvent : WebSocketNotificationEvent
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("user_name")]
        public string UserName { get; set; }

        [JsonProperty("user_login")]
        public string UserLogin { get; set; }

        [JsonProperty("message_id")]
        public string MessageId { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("held_at")]
        public DateTime HeldAt { get; set; }

        [JsonProperty("fragments")]
        public Fragments Fragments { get; set; }
    }
}