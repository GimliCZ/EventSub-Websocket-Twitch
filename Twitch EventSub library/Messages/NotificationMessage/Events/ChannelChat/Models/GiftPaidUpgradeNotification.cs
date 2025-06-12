using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class GiftPaidUpgradeNotification
    {
        [JsonProperty("gifter_is_anonymous")]
        public bool GifterIsAnonymous { get; set; }

        [JsonProperty("gifter_user_id")]
        public string GifterUserId { get; set; }

        [JsonProperty("gifter_user_name")]
        public string GifterUserName { get; set; }

        [JsonProperty("gifter_user_login")]
        public string GifterUserLogin { get; set; }
    }
}