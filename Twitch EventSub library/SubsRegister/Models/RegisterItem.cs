using Twitch.EventSub.API.Enums;

namespace Twitch.EventSub.SubsRegister.Models
{
    public class RegisterItem
    {
        public string Key { get; set; }
        public Type SpecificObject { get; set; }
        public SubscriptionTypes SubscriptionType { get; set; }
        public string Ver { get; set; }
        public List<ConditionTypes> Conditions { get; set; }
    }
};