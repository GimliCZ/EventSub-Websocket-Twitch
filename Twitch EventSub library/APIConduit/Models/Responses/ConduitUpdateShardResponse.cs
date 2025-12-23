using Newtonsoft.Json;
using System;
using Twitch.EventSub.APIConduit.Models.Shared;

namespace Twitch.EventSub.APIConduit.Models.Responses
{
    public class ConduitUpdateShardResponse
    {
        [JsonProperty("data")]
        public List<ConduitShard> Data { get; set; } = new();

        [JsonProperty("errors")]
        public List<ConduitShardError> Errors { get; set; } = new();
    }
}
