using Newtonsoft.Json;
using System;
using Twitch.EventSub.APIConduit.Models.Shared;

namespace Twitch.EventSub.APIConduit.Models.Requests
{
    public class ConduitUpdateShardRequest
    {
        [JsonProperty("conduit_id")]
        public string ConduitId { get; set; } = string.Empty;

        [JsonProperty("shards")]
        public List<ShardUpdateItem> Shards { get; set; } = new();
    }
}
