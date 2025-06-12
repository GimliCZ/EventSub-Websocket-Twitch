using Newtonsoft.Json;

namespace Twitch.EventSub.API.Models
{
    public class Transport
    {
        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("session_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? SessionId { get; set; }

        [JsonProperty("conduit_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? ConduitId { get; set; }

        [JsonProperty("callback", NullValueHandling = NullValueHandling.Ignore)]
        public string? Callback;

        [JsonProperty("connected_at", NullValueHandling = NullValueHandling.Ignore)]
        public string? ConnectedAt;

        [JsonProperty("disconnected_at", NullValueHandling = NullValueHandling.Ignore)]
        public string? DisconnectedAt;
    }
}