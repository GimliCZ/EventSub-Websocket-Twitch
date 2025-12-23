using Newtonsoft.Json;
using System;

namespace Twitch.EventSub.APIConduit.Models.Shared
{
    public class ConduitShardError
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;
    }
}
