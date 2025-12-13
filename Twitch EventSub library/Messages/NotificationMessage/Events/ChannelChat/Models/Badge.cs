using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class Badge
    {
        [JsonProperty("set_id")]
        public string SetId { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("info")]
        public string Info { get; set; }
    }
}