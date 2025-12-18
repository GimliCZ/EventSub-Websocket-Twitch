using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints
{

    public class ChannelPointsCustomRewardAddEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        
        public string BroadcasterUserName { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("is_paused")]
        public bool IsPaused { get; set; }

        [JsonProperty("is_in_stock")]
        public bool IsInStock { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("is_user_input_required")]
        public bool IsUserInputRequired { get; set; }

        [JsonProperty("should_redemptions_skip_request_queue")]
        public bool ShouldRedemptionsSkipRequestQueue { get; set; }

        [JsonProperty("cooldown_expires_at")]
        public object CooldownExpiresAt { get; set; }

        [JsonProperty("redemptions_redeemed_current_stream")]
        public object RedemptionsRedeemedCurrentStream { get; set; }

        [JsonProperty("max_per_stream")]
        public MaxPerStream MaxPerStream { get; set; }

        [JsonProperty("max_per_user_per_stream")]
        public MaxPerUserPerStream MaxPerUserPerStream { get; set; }

        [JsonProperty("global_cooldown")]
        public GlobalCooldown GlobalCooldown { get; set; }

        [JsonProperty("background_color")]
        public string BackgroundColor { get; set; }

        [JsonProperty("image")]
        public Image Image { get; set; }

        [JsonProperty("default_image")]
        public DefaultImage DefaultImage { get; set; }
    }
}