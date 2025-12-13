using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class Fragment
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("cheermote")]
        public Cheermote Cheermote { get; set; }

        [JsonProperty("emote")]
        public Emote Emote { get; set; }

        [JsonProperty("mention")]
        public string Mention { get; set; }
    }
}