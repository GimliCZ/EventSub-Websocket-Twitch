using Newtonsoft.Json;
using System;

namespace Twitch.EventSub.APIConduit.Models.Shared
{
    // --- Conduit Data ---
    public class ConduitData
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("shard_count")]
        public int ShardCount { get; set; }
    }
}
