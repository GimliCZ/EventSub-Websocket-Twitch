using Newtonsoft.Json;
using System;

namespace Twitch.EventSub.APIConduit.Models.Requests
{
    public class ConduitUpdateRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("shard_count")]
        public int ShardCount { get; set; }
    }
}
