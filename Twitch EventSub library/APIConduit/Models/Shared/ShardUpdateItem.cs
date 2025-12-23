using Newtonsoft.Json;
using System;
using Twitch.EventSub.API.Models;

namespace Twitch.EventSub.APIConduit.Models.Shared
{
    public class ShardUpdateItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("transport")]
        public Transport Transport { get; set; } = new();
    }
}
