using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShared;

public class ChannelSharedChatSessionBeginEvent: WebSocketNotificationEvent
{
    [JsonProperty("broadcaster_user_id")]
    public string BroadcasterUserId { get; set; }

    [JsonProperty("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; set; }

    [JsonProperty("broadcaster_user_name")]
    public string BroadcasterUserName { get; set; }
    
    [JsonProperty("session_id")]
    public string SessionId { get; set; }

    [JsonProperty("host_broadcaster_user_id")]
    public string HostBroadcasterUserId { get; set; }

    [JsonProperty("host_broadcaster_user_name")]
    public string HostBroadcasterUserName { get; set; }

    [JsonProperty("host_broadcaster_user_login")]
    public string HostBroadcasterUserLogin { get; set; }

    [JsonProperty("participants")]
    public List<Participant> Participants { get; set; }
}