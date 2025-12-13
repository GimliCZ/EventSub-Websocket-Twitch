using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShared;

public class Participant
{
    [JsonProperty("broadcaster_user_id")]
    public string BroadcasterUserId { get; set; }

    [JsonProperty("broadcaster_user_name")]
    public string BroadcasterUserName { get; set; }

    [JsonProperty("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; set; }
}