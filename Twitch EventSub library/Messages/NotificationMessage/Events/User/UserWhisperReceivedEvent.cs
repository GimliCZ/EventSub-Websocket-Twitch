using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.User;

public class UserWhisperReceivedEvent:WebSocketNotificationEvent
{
    [JsonProperty("from_user_id")]
    public string FromUserId { get; set; }

    [JsonProperty("from_user_name")]
    public string FromUserName { get; set; } 

    [JsonProperty("from_user_login")]
    public string FromUserLogin { get; set; } 

    [JsonProperty("to_user_id")]
    public string ToUserId { get; set; } 

    [JsonProperty("to_user_name")]
    public string ToUserName { get; set; } 

    [JsonProperty("to_user_login")]
    public string ToUserLogin { get; set; } 

    [JsonProperty("whisper_id")]
    public string WhisperId { get; set; } 

    [JsonProperty("whisper")]
    public WhisperContent Whisper { get; set; } 
}