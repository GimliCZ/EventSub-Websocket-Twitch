using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class MessageEmote
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("emote_set_id")]
        public string EmoteSetId { get; set; }

        [JsonProperty("owner_id")]
        public string OwnerId { get; set; }

        [JsonProperty("format")]
        public List<string> Format { get; set; }
    }
}