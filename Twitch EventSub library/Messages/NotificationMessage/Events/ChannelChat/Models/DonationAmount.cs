using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class DonationAmount
    {
        [JsonProperty("value")]
        public int Value { get; set; }

        [JsonProperty("decimal_place")]
        public int DecimalPlace { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }
}