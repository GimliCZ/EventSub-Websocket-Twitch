﻿using Newtonsoft.Json;

namespace Twitch.EventSub.API.Models
{
    public class CreateSubscriptionRequest
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("condition")]
        public Condition Condition { get; set; }

        [JsonProperty("transport")]
        public Transport Transport { get; set; }
    }

    public class Condition
    {
        [JsonProperty("user_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? UserId { get; set; }

        [JsonProperty("broadcaster_user_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? BroadcasterUserId { get; set; }

        [JsonProperty("to_broadcaster_user_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? ToBroadcasterUserId { get; set; }

        [JsonProperty("moderator_user_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? ModeratorUserId { get; set; }

        [JsonProperty("organization_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? OrganizationId { get; set; }

        [JsonProperty("category_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? CategoryId { get; set; }

        [JsonProperty("campaign_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? CampaignId { get; set; }

        [JsonProperty("extension_client_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? ExtensionClientId { get; set; }

        [JsonProperty("client_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? ClientId { get; set; }

        [JsonProperty("reward_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? RewardId { get; set; }
    }

    public class Transport
    {
        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("session_id", NullValueHandling = NullValueHandling.Ignore)]
        public string SessionId { get; set; }

        [JsonProperty("conduit_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ConduitId { get; set; }

    }
}
