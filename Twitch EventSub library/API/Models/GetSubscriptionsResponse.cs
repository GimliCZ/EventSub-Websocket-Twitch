using Newtonsoft.Json;
using Twitch.EventSub.Messages.SharedContents;

namespace Twitch.EventSub.API.Models
{
    public class GetSubscriptionsResponse
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("data")]
        public List<WebSocketSubscription> Data { get; set; } = new();

        [JsonProperty("total_cost")]
        public int TotalCost { get; set; }

        [JsonProperty("max_total_cost")]
        public int MaxTotalCost { get; set; }

        [JsonProperty("pagination")]
        public SubscriptionPagination Pagination { get; set; } = new();
    }

    public class SubscriptionPagination
    {
        [JsonProperty("cursor")]
        public string? Cursor { get; set; }
    }
}
