using Newtonsoft.Json;

namespace Twitch.EventSub.API.Models
{
    public class CreateSubscriptionRequest
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("condition")]
        public Condition Condition { get; set; }

        [JsonProperty("transport")]
        public Transport Transport { get; set; }
    }
}