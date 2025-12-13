using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class MessageMessage
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("fragments")]
        public List<Fragment> Fragments { get; set; }
    }
}