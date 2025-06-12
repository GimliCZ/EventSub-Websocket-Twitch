using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class BitsBadgeTierNotification
    {
        [JsonProperty("tier")]
        public int Tier { get; set; }
    }
}