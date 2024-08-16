﻿using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelCheer
{
    public class ChannelCheerEvent : WebSocketNotificationEvent
    {
        [JsonProperty("is_anonymous")]
        public bool IsAnonymous { get; set; }

        [JsonProperty("user_id")]
        public string? UserId { get; set; }

        [JsonProperty("user_login")]
        public string? UserLogin { get; set; }

        [JsonProperty("user_name")]
        public string? UserName { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("bits")]
        public int Bits { get; set; }
    }
}