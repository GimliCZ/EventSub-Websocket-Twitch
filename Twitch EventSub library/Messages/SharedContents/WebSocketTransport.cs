using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.SharedContents
{
    public class WebSocketTransport
    {
        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("session_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? SessionId { get; set; }

        [JsonProperty("conduit_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? ConduitId { get; set; }

        [JsonProperty("connected_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? ConnectedAt { get; set; }

        [JsonProperty("disconnected_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? DisconnectedAt { get; set; }
    }
}