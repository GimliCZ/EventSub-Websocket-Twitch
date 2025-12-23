using Newtonsoft.Json;
using System;

namespace Twitch.EventSub.APIConduit.Models.Requests
{
    // --- Requests ---
    public class ConduitCreateRequest
    {
        [JsonProperty("shard_count")]
        public int ShardCount { get; set; }
    }
}
