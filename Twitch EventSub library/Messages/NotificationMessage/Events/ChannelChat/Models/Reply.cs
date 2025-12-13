using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat.Models
{
    public class Reply
    {
        [JsonProperty("parent_message_id")]
        public string ParentMessageId { get; set; }

        [JsonProperty("parent_message_body")]
        public string ParentMessageBody { get; set; }

        [JsonProperty("parent_user_id")]
        public string ParentUserId { get; set; }

        [JsonProperty("parent_user_name")]
        public string ParentUserName { get; set; }

        [JsonProperty("parent_user_login")]
        public string ParentUserLogin { get; set; }

        [JsonProperty("thread_message_id")]
        public string ThreadMessageId { get; set; }

        [JsonProperty("thread_user_id")]
        public string ThreadÜserId { get; set; }

        [JsonProperty("thread_user_name")]
        public string ThreadUserName { get; set; }

        [JsonProperty("thread_user_login")]
        public string ThreadUserLogin { get; set; }
    }
}