using Newtonsoft.Json;
using System;

namespace Twitch.EventSub.APIConduit.Models.Shared
{
    public class ConduitPagination
    {
        [JsonProperty("cursor")]
        public string? Cursor { get; set; }
    }
}
