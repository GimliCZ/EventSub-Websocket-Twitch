using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class FragmentNotification
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("cheermote")]
        public CheermoteNotification Cheermote { get; set; }

        [JsonProperty("emote")]
        public EmoteNotification Emote { get; set; }

        [JsonProperty("mention")]
        public MentionNotification Mention { get; set; }
    }
}