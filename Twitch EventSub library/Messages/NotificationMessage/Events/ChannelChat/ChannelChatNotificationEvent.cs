using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models;
using Twitch.EventSub.Messages.NotificationMessage.Events.NotificationEventClasses;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat
{
    public class ChannelChatNotificationEvent : WebSocketNotificationEvent
    {
        [JsonProperty("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonProperty("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonProperty("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }
        
        [JsonProperty("chatter_user_id")]
        public string ChatterUserId { get; set; }

        [JsonProperty("chatter_user_login")]
        public string ChatterUserLogin { get; set; }

        [JsonProperty("chatter_user_name")]
        public string ChatterUserName { get; set; }

        [JsonProperty("chatter_is_anonymous")]
        public bool ChatterIsAnonymous { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("badges")]
        public List<Badge> Badges { get; set; }

        [JsonProperty("system_message")]
        public string SystemMessage { get; set; }

        [JsonProperty("message_id")]
        public string MessageId { get; set; }

        [JsonProperty("message")]
        public MessageNotification Message { get; set; }

        [JsonProperty("notice_type")]
        public string NoticeType { get; set; }

        [JsonProperty("sub")]
        public SubEvent Sub { get; set; }

        [JsonProperty("resub")]
        public ResubEvent Resub { get; set; }

        [JsonProperty("sub_gift")]
        public SubGift SubGift { get; set; }

        [JsonProperty("community_sub_gift")]
        public CommunitySubGift CommunitySubGift { get; set; }

        [JsonProperty("gift_paid_upgrade")]
        public GiftPaidUpgradeNotification GiftPaidUpgrade { get; set; }

        [JsonProperty("prime_paid_upgrade")]
        public PrimePaidUpgradeNotification PrimePaidUpgrade { get; set; }

        [JsonProperty("pay_it_forward")]
        public PayItForwardNotification PayItForward { get; set; }

        [JsonProperty("raid")]
        public RaidNotification Raid { get; set; }

        [JsonProperty("unraid")]
        public string Unraid { get; set; }

        [JsonProperty("announcement")]
        public Announcement Announcement { get; set; }

        [JsonProperty("bits_badge_tier")]
        public BitsBadgeTierNotification BitsBadgeTier { get; set; }

        [JsonProperty("charity_donation")]
        public CharityDonationNotification CharityDonation { get; set; }
    }
}