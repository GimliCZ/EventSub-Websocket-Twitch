using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models
{
    public class RewardAutomatic
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("unlocked_emote")]
        public UnlockedEmote UnlockedEmote { get; set; }
    }
}