using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription
{
    public class Message
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("emotes")]
        public List<Emote> Emotes { get; set; }

        [JsonProperty("fragments")]
        public List<Fragment> Fragments { get; set; }
    }
}