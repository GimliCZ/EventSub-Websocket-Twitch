using Newtonsoft.Json;
using System;
using Twitch.EventSub.APIConduit.Models.Shared;

namespace Twitch.EventSub.APIConduit.Models.Responses
{
    public class ConduitGetShardsResponse
    {
        [JsonProperty("data")]
        public List<ConduitShard> Data { get; set; } = new();

        [JsonProperty("pagination")]
        public ConduitPagination Pagination { get; set; } = new();
    }
}
