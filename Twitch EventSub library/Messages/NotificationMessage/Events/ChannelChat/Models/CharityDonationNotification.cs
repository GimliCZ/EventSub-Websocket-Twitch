using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class CharityDonationNotification
    {
        [JsonProperty("charity_name")]
        public string CharityName { get; set; }

        [JsonProperty("amount")]
        public DonationAmount Amount { get; set; }
    }
}