using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.SubsRegister;

namespace Twitch.EventSub.API.Extensions
{
    public static class CreateSubscriptionRequestExtension
    {
        private static readonly Dictionary<SubscriptionTypes, (string Type, string Version, List<ConditionTypes> Conditions)> TypeVersionConditionMap = GenerateSubscriptionDisctionary();

        private static Dictionary<SubscriptionTypes, (string Type, string Version, List<ConditionTypes> Conditions)> GenerateSubscriptionDisctionary()
        {
            var newDict = new Dictionary<SubscriptionTypes, (string Type, string Version, List<ConditionTypes> Conditions)>();

            foreach (var register in Twitch.EventSub.SubsRegister.Register.RegisterDictionaryByVersion.Values)
            {
                newDict.Add(register.SubscriptionType, (register.Key, register.Ver, register.Conditions));
            }
            return newDict;
        }

        //Reward Id enables to sub to specific reward only. As null it subs all rewards
        public static CreateSubscriptionRequest SetSubscriptionType(this CreateSubscriptionRequest request,
         SubscriptionTypes subscriptionType, string userId, string? rewardId = null)
        {
            if (TypeVersionConditionMap.TryGetValue(subscriptionType, out var typeVersionCondition))
            {
                request.Type = typeVersionCondition.Type;
                request.Version = typeVersionCondition.Version;
                foreach (var conditionType in typeVersionCondition.Conditions)
                {
                    switch (conditionType)
                    {
                        case ConditionTypes.BroadcasterUserId:
                            request.Condition.BroadcasterUserId = userId;
                            break;

                        case ConditionTypes.ToBroadcasterUserId:
                            request.Condition.ToBroadcasterUserId = userId;
                            break;

                        case ConditionTypes.ModeratorUserId:
                            request.Condition.ModeratorUserId = userId;
                            break;
                        //webhook only
                        /*    case ConditionType.OrganizationId:
                                request.Condition.OrganizationId = organizationId;
                                break;

                            case ConditionType.CampaignId:
                                request.Condition.CampaignId = campaignId;
                                break;

                            case ConditionType.CategoryId:
                                request.Condition.CategoryId = categoryId;
                                break;*/
                        case ConditionTypes.ClientId:
                            request.Condition.ClientId = userId;
                            break;
                        /*case ConditionType.ExtensionClientId:
                            request.Condition.ExtensionClientId = userId;
                            break;*/
                        case ConditionTypes.UserId:
                            request.Condition.UserId = userId;
                            break;

                        case ConditionTypes.RewardId:
                            request.Condition.RewardId = rewardId;
                            break;

                        case ConditionTypes.ConduitId:
                            request.Condition.ConduitId = userId;
                            break;

                        default:
                            throw new ArgumentException("Invalid subscription type");
                    }
                }

                return request;
            }
            throw new ArgumentException("Invalid subscription");
        }

        /// <summary>
        /// Dedicated builder for conduit.shard.disabled subscriptions.
        /// This subscription requires both client_id and conduit_id as conditions,
        /// and is the recommended health-monitoring subscription for conduits.
        /// </summary>
        public static CreateSubscriptionRequest SetConduitShardDisabled(this CreateSubscriptionRequest request, string clientId, string conduitId)
        {
            request.Type = Twitch.EventSub.SubsRegister.RegisterKeys.ConduitShardDisabled.ToEventString();
            request.Version = "1";
            request.Condition ??= new Condition();
            request.Condition.ClientId = clientId;
            request.Condition.ConduitId = conduitId;
            return request;
        }
    }
}