using Newtonsoft.Json;
using System;
using Twitch.EventSub.APIConduit.Models.Shared;

namespace Twitch.EventSub.APIConduit.Models.Responses
{
    // --- Responses ---
    public class ConduitCreateResponse
    {
        [JsonProperty("data")]
        public List<ConduitData> Data { get; set; } = new();
    }
}
