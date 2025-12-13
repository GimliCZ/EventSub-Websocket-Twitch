using Newtonsoft.Json;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models;

public class CheermoteOld
{
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("Amount")]
        public int Amount { get; set; }
        [JsonProperty("prefix")]
        public string Prefix { get; set; }
        [JsonProperty("tier")]
        public int Tier { get; set; }
}