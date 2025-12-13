using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.User;

public class UserUpdateEvent: WebSocketNotificationEvent
{
    [JsonProperty("user_id")]
    public string UserId { get; set; }

    [JsonProperty("user_login")]
    public string UserLogin { get; set; }

    [JsonProperty("user_name")]
    public string UserName { get; set; }

    /// <summary>
    /// The user's email address. Only included if the app has the user:read:email scope; otherwise empty string.
    /// </summary>
    [JsonProperty("email")]
    public string Email { get; set; }

    /// <summary>
    /// Whether Twitch has verified the user's email address. 
    /// NOTE: Ignore this field if Email is an empty string.
    /// </summary>
    [JsonProperty("email_verified")]
    public bool EmailVerified { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }
}