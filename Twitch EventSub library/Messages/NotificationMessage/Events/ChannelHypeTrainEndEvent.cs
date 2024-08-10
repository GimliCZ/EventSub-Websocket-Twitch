﻿using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events
{
    public class ChannelHypeTrainEndEvent : WebSocketNotificationEvent
    {
        [JsonProperty("ended_at")]
        public string EndedAt { get; set; }

        [JsonProperty("cooldown_ends_at")]
        public string CooldownEndsAt { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("progress")]
        public int Progress { get; set; }

        [JsonProperty("goal")]
        public int Goal { get; set; }

        [JsonProperty("top_contributions")]
        public List<TopContribution> TopContributions { get; set; }

        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }
}