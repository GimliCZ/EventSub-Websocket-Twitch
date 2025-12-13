using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod
{
    public class AutomodMessageHoldEventV2 : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; init; } = null!;

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; init; } = null!;

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; init; } = null!;

        [JsonProperty("user_id")]
        public string UserId { get; init; } = null!;

        [JsonProperty("user_login")]
        public string UserLogin { get; init; } = null!;

        [JsonProperty("user_name")]
        public string UserName { get; init; } = null!;

        [JsonProperty("message_id")]
        public string MessageId { get; init; } = null!;

        [JsonProperty("message")]
        public AutomodMessage Message { get; init; } = null!;

        [JsonProperty("held_at")]
        public DateTimeOffset HeldAt { get; init; }

        [JsonProperty("reason")]
        public string Reason { get; init; } = null!;

        [JsonProperty("automod")]
        public AutomodDetails? Automod { get; init; }

        [JsonProperty("blocked_term")]
        public BlockedTermDetails? BlockedTerm { get; init; }
    }
}