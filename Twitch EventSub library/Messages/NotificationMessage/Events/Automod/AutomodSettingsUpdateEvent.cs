using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod;

public class AutomodSettingsUpdateEvent : WebSocketNotificationEvent
{
    [JsonProperty("data")]
    public List<Data> data { get; set; }
}

public class Data
{
    [JsonProperty("broadcaster_user_id")]
    public string BroadcasterUserId { get; set; }

    [JsonProperty("broadcaster_user_name")]
    public string BroadcasterUserName { get; set; }

    [JsonProperty("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; set; }

    [JsonProperty("moderator_user_id")]
    public string ModeratorUserId { get; set; }

    [JsonProperty("moderator_user_name")]
    public string ModeratorUserName { get; set; }

    [JsonProperty("moderator_user_login")]
    public string ModeratorUserLogin { get; set; }
    
    [JsonProperty("overall_level")]
    public int? OverallLevel { get; init; }

    [JsonProperty("disability")]
    public int Disability { get; set; }

    [JsonProperty("aggression")]
    public int Aggression { get; set; }

    [JsonProperty("sexuality_sex_or_gender")]
    public int SexualitySexOrGender { get; set; }

    [JsonProperty("misogyny")]
    public int Misogyny { get; set; }

    [JsonProperty("bullying")]
    public int Bullying { get; set; }

    [JsonProperty("swearing")]
    public int Swearing { get; set; }

    [JsonProperty("race_ethnicity_or_religion")]
    public int RaceEthnicityOrReligion { get; set; }

    [JsonProperty("sex_based_terms")]
    public int SexBasedTerms { get; set; }
}