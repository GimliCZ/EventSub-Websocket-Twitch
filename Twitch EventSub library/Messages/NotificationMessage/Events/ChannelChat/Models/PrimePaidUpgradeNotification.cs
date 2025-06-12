using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class PrimePaidUpgradeNotification
    {
        [JsonProperty("sub_tier")]
        public string SubTier { get; set; }
    }
}