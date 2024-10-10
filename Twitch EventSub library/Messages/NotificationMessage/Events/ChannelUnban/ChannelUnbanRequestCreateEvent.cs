﻿using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelUnban
{
    public class ChannelUnbanRequestCreateEvent : WebSocketNotificationEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("user_login")]
        public string UserLogin { get; set; }

        [JsonProperty("user_name")]
        public string UserName { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }
}