using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class MessageNotification
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("fragments")]
        public List<FragmentNotification> Fragments { get; set; }
    }
}