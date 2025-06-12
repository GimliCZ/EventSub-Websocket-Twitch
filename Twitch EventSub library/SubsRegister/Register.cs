using Twitch.EventSub.API.Enums;
using Twitch.EventSub.Messages.NotificationMessage.Events;
using Twitch.EventSub.Messages.NotificationMessage.Events.Automod;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelCharity;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelCheer;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelGoal;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelGuest;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelHype;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelModerator;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoll;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPrediction;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShield;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShoutout;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSuspicious;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelUnban;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelVIP;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelWarning;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.SubsRegister.Models;

namespace Twitch.EventSub.SubsRegister
{
    //TODO:
    //Add  RegisterItem dictionary refrection to simplify changes.
    public static class Register
    {
        public static readonly RegisterItem RegAutomodMessageHold = new RegisterItem
        {
            Key = RegisterKeys.AutomodMessageHold,
            Ver = "1",
            SpecificObject = typeof(AutomodMessageHoldEvent),
            SubscriptionType = SubscriptionTypes.AutomodMessageHold,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegAutomodMessageUpdate = new RegisterItem
        {
            Key = RegisterKeys.AutomodMessageUpdate,
            Ver = "1",
            SpecificObject = typeof(AutomodMessageHoldEvent),
            SubscriptionType = SubscriptionTypes.AutomodMessageUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegAutomodTermsUpdate = new RegisterItem
        {
            Key = RegisterKeys.AutomodTermsUpdate,
            Ver = "1",
            SpecificObject = typeof(AutomodTermsUpdateEvent),
            SubscriptionType = SubscriptionTypes.AutomodTermsUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegConduitShardDisabled = new RegisterItem
        {
            Key = RegisterKeys.ConduitShardDisabled,
            Ver = "1",
            SpecificObject = typeof(ConduitShardDisabledEvent),
            SubscriptionType = SubscriptionTypes.ConduitShardDisabled,
            Conditions = CondList(ConditionTypes.ClientId)
        };

        public static readonly RegisterItem RegChannelAdBreakBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelAdBreakBegin,
            Ver = "1",
            SpecificObject = typeof(ChannelAdBreakBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelAdBreakBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelBan = new RegisterItem
        {
            Key = RegisterKeys.ChannelBan,
            Ver = "1",
            SpecificObject = typeof(ChannelBanEvent),
            SubscriptionType = SubscriptionTypes.ChannelBan,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelFollow = new RegisterItem
        {
            Key = RegisterKeys.ChannelFollow,
            Ver = "2",
            SpecificObject = typeof(ChannelFollowEvent),
            SubscriptionType = SubscriptionTypes.ChannelFollow,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelGoalBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelGoalBegin,
            Ver = "1",
            SpecificObject = typeof(ChannelGoalBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelGoalBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGoalEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelGoalEnd,
            Ver = "1",
            SpecificObject = typeof(ChannelGoalEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelGoalEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGoalProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelGoalProgress,
            Ver = "1",
            SpecificObject = typeof(ChannelGoalProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelGoalProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarGuestUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarGuestUpdate,
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarGuestUpdateEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarGuestUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarSessionBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarSessionBegin,
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarSessionBeginEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarSessionBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarSessionEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarSessionEnd,
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarSessionEndEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarSessionEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarSettingsUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarSettingsUpdate,
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarSettingsUpdateEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarSettingsUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelHypeTrainBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelHypeTrainBegin,
            Ver = "1",
            SpecificObject = typeof(ChannelHypeTrainBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelHypeTrainBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelHypeTrainEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelHypeTrainEnd,
            Ver = "1",
            SpecificObject = typeof(ChannelHypeTrainEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelHypeTrainEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelHypeTrainProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelHypeTrainProgress,
            Ver = "1",
            SpecificObject = typeof(ChannelHypeTrainProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelHypeTrainProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityCampaignProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityCampaignProgress,
            Ver = "1",
            SpecificObject = typeof(ChannelCharityCampaignProgressEvent),
            SubscriptionType = SubscriptionTypes.CharityCampaignProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityCampaignStart = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityCampaignStart,
            Ver = "1",
            SpecificObject = typeof(ChannelCharityCampaignStartEvent),
            SubscriptionType = SubscriptionTypes.CharityCampaignStart,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityCampaignStop = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityCampaignStop,
            Ver = "1",
            SpecificObject = typeof(ChannelCharityCampaignStopEvent),
            SubscriptionType = SubscriptionTypes.CharityCampaignStop,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityDonation = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityDonation,
            Ver = "1",
            SpecificObject = typeof(ChannelCharityDonationEvent),
            SubscriptionType = SubscriptionTypes.CharityDonation,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatClear = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatClear,
            Ver = "1",
            SpecificObject = typeof(ChannelChatClearEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatClear,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatClearUserMessages = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatClearUserMessages,
            Ver = "1",
            SpecificObject = typeof(ChannelChatClearUserMessagesEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatClearUserMessages,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem RegChannelChatMessage = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatMessage,
            Ver = "1",
            SpecificObject = typeof(ChannelChatMessageEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatMessage,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem RegChannelChatMessageDelete = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatMessageDelete,
            Ver = "1",
            SpecificObject = typeof(ChannelChatMessageDeleteEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatMessageDelete,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatNotification = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatNotification,
            Ver = "1",
            SpecificObject = typeof(ChannelChatNotificationEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatNotification,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatSettingsUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatSettingsUpdate,
            Ver = "1",
            SpecificObject = typeof(ChannelChatSettingsUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatSettingsUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatUserMessageHold = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatUserMessageHold,
            Ver = "1",
            SpecificObject = typeof(ChannelChatUserMessageHoldEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatUserMessageHold,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem RegChannelChatUserMessageUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatUserMessageUpdate,
            Ver = "1",
            SpecificObject = typeof(ChannelChatUserMessageUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatUserMessageUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem RegChannelCheer = new RegisterItem
        {
            Key = RegisterKeys.ChannelCheer,
            Ver = "1",
            SpecificObject = typeof(ChannelCheerEvent),
            SubscriptionType = SubscriptionTypes.ChannelCheer,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelModeratorAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelModeratorAdd,
            Ver = "1",
            SpecificObject = typeof(ChannelModeratorAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelModeratorAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelModeratorRemove = new RegisterItem
        {
            Key = RegisterKeys.ChannelModeratorRemove,
            Ver = "1",
            SpecificObject = typeof(ChannelModeratorRemoveEvent),
            SubscriptionType = SubscriptionTypes.ChannelModeratorRemove,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPointsAutomaticRewardRedemptionAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsAutomaticRewardRedemptionAdd,
            Ver = "1",
            SpecificObject = typeof(ChannelPointsAutomaticRewardRedemptionAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsAutomaticRewardRedemptionAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardAdd,
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardRedemptionAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardRedemptionAdd,
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardRedemptionAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardRedemptionAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardRedemptionUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardRedemptionUpdate,
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardRedemptionUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardRedemptionUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardRemove = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardRemove,
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardRemoveEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardRemove,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardUpdate,
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPollBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelPollBegin,
            Ver = "1",
            SpecificObject = typeof(ChannelPollBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelPollBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPollEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPollEnd,
            Ver = "1",
            SpecificObject = typeof(ChannelPollEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelPollEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPollProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelPollProgress,
            Ver = "1",
            SpecificObject = typeof(ChannelPollProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelPollProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionBegin,
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionEnd,
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionLock = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionLock,
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionLockEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionLock,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionProgress,
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelRaid = new RegisterItem
        {
            Key = RegisterKeys.ChannelRaid,
            Ver = "1",
            SpecificObject = typeof(ChannelRaidEvent),
            SubscriptionType = SubscriptionTypes.ChannelRaid,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelShieldModeBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelShieldModeBegin,
            Ver = "1",
            SpecificObject = typeof(ChannelShieldModeBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelShieldModeBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelShieldModeEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelShieldModeEnd,
            Ver = "1",
            SpecificObject = typeof(ChannelShieldModeEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelShieldModeEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelShoutoutCreate = new RegisterItem
        {
            Key = RegisterKeys.ChannelShoutoutCreate,
            Ver = "1",
            SpecificObject = typeof(ChannelShoutoutCreateEvent),
            SubscriptionType = SubscriptionTypes.ChannelShoutoutCreate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelShoutoutReceived = new RegisterItem
        {
            Key = RegisterKeys.ChannelShoutoutReceived,
            Ver = "1",
            SpecificObject = typeof(ChannelShoutoutReceivedEvent),
            SubscriptionType = SubscriptionTypes.ChannelShoutoutReceived,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelSubscribe = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscribe,
            Ver = "1",
            SpecificObject = typeof(ChannelSubscribeEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscribe,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSubscriptionEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscriptionEnd,
            Ver = "1",
            SpecificObject = typeof(ChannelSubscriptionEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscriptionEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSubscriptionGift = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscriptionGift,
            Ver = "1",
            SpecificObject = typeof(ChannelSubscriptionGiftEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscriptionGift,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSubscriptionMessage = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscriptionMessage,
            Ver = "1",
            SpecificObject = typeof(ChannelSubscriptionMessageEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscriptionMessage,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelUnban = new RegisterItem
        {
            Key = RegisterKeys.ChannelUnban,
            Ver = "1",
            SpecificObject = typeof(ChannelUnbanEvent),
            SubscriptionType = SubscriptionTypes.ChannelUnban,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelUnbanRequestCreate = new RegisterItem
        {
            Key = RegisterKeys.ChannelUnbanRequestCreate,
            Ver = "1",
            SpecificObject = typeof(ChannelUnbanRequestCreateEvent),
            SubscriptionType = SubscriptionTypes.ChannelUnbanCreate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelUnbanRequestResolve = new RegisterItem
        {
            Key = RegisterKeys.ChannelUnbanRequestResolve,
            Ver = "1",
            SpecificObject = typeof(ChannelUnbanRequestResolveEvent),
            SubscriptionType = SubscriptionTypes.ChannelUnbanResolve,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelUpdate,
            Ver = "2",
            SpecificObject = typeof(ChannelUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelVIPAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelVIPAdd,
            Ver = "1",
            SpecificObject = typeof(ChannelVIPAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelVIPAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelVIPRemove = new RegisterItem
        {
            Key = RegisterKeys.ChannelVIPRemove,
            Ver = "1",
            SpecificObject = typeof(ChannelVIPRemoveEvent),
            SubscriptionType = SubscriptionTypes.ChannelVIPRemove,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelWarningAcknowledge = new RegisterItem
        {
            Key = RegisterKeys.ChannelWarningAcknowledge,
            Ver = "1",
            SpecificObject = typeof(ChannelWarningAcknowledgeEvent),
            SubscriptionType = SubscriptionTypes.ChannelWarningAcknowledge,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelWarningSend = new RegisterItem
        {
            Key = RegisterKeys.ChannelWarningSend,
            Ver = "1",
            SpecificObject = typeof(ChannelWarningSendEvent),
            SubscriptionType = SubscriptionTypes.ChannelWarningSend,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegStreamOffline = new RegisterItem
        {
            Key = RegisterKeys.StreamOffline,
            Ver = "1",
            SpecificObject = typeof(StreamOfflineEvent),
            SubscriptionType = SubscriptionTypes.StreamOffline,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegStreamOnline = new RegisterItem
        {
            Key = RegisterKeys.StreamOnline,
            Ver = "1",
            SpecificObject = typeof(StreamOnlineEvent),
            SubscriptionType = SubscriptionTypes.StreamOnline,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSuspiciousUserMessage = new RegisterItem
        {
            Key = RegisterKeys.ChannelSuspiciousUserMessage,
            Ver = "1",
            SpecificObject = typeof(ChannelSuspiciousUserMessageEvent),
            SubscriptionType = SubscriptionTypes.SuspiciousUserMessage,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelSuspiciousUserUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelSuspiciousUserUpdate,
            Ver = "1",
            SpecificObject = typeof(ChannelSuspiciousUserUpdateEvent),
            SubscriptionType = SubscriptionTypes.SuspiciousUserUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly List<RegisterItem> RegisterList = GetRegisterList();

        public static readonly Dictionary<string, RegisterItem> RegisterDictionary = GetRegisterDictionary();

        public static List<RegisterItem> GetRegisterList()
        {
            var registryKeysList = RegisterKeys.KeysList;
            var registryItems = new List<RegisterItem>();

            foreach (var key in registryKeysList)
            {
                var item = GetRegistryItem(key);
                if (item != null)
                {
                    registryItems.Add(item);
                }
            }

            return registryItems;
        }

        public static Dictionary<string, RegisterItem> GetRegisterDictionary()
        {
            var registryKeysList = RegisterKeys.KeysList;
            var registryItems = new Dictionary<string, RegisterItem>();

            foreach (string key in registryKeysList)
            {
                var item = GetRegistryItem(key);
                if (item != null)
                {
                    registryItems.Add(key, item);
                }
            }

            return registryItems;
        }

        // Retrieves the RegistryItem based on the event key
        public static RegisterItem GetRegistryItem(string key)
        {
            return key switch
            {
                RegisterKeys.AutomodMessageHold => RegAutomodMessageHold,
                RegisterKeys.AutomodMessageUpdate => RegAutomodMessageUpdate,
                RegisterKeys.AutomodTermsUpdate => RegAutomodTermsUpdate,
                RegisterKeys.ConduitShardDisabled => RegConduitShardDisabled,
                RegisterKeys.ChannelAdBreakBegin => RegChannelAdBreakBegin,
                RegisterKeys.ChannelBan => RegChannelBan,
                RegisterKeys.ChannelFollow => RegChannelFollow,
                RegisterKeys.ChannelGoalBegin => RegChannelGoalBegin,
                RegisterKeys.ChannelGoalEnd => RegChannelGoalEnd,
                RegisterKeys.ChannelGoalProgress => RegChannelGoalProgress,
                RegisterKeys.ChannelGuestStarGuestUpdate => RegChannelGuestStarGuestUpdate,
                RegisterKeys.ChannelGuestStarSessionBegin => RegChannelGuestStarSessionBegin,
                RegisterKeys.ChannelGuestStarSessionEnd => RegChannelGuestStarSessionEnd,
                RegisterKeys.ChannelGuestStarSettingsUpdate => RegChannelGuestStarSettingsUpdate,
                RegisterKeys.ChannelHypeTrainBegin => RegChannelHypeTrainBegin,
                RegisterKeys.ChannelHypeTrainEnd => RegChannelHypeTrainEnd,
                RegisterKeys.ChannelHypeTrainProgress => RegChannelHypeTrainProgress,
                RegisterKeys.ChannelCharityCampaignProgress => RegChannelCharityCampaignProgress,
                RegisterKeys.ChannelCharityCampaignStart => RegChannelCharityCampaignStart,
                RegisterKeys.ChannelCharityCampaignStop => RegChannelCharityCampaignStop,
                RegisterKeys.ChannelCharityDonation => RegChannelCharityDonation,
                RegisterKeys.ChannelChatClear => RegChannelChatClear,
                RegisterKeys.ChannelChatClearUserMessages => RegChannelChatClearUserMessages,
                RegisterKeys.ChannelChatMessage => RegChannelChatMessage,
                RegisterKeys.ChannelChatMessageDelete => RegChannelChatMessageDelete,
                RegisterKeys.ChannelChatNotification => RegChannelChatNotification,
                RegisterKeys.ChannelChatSettingsUpdate => RegChannelChatSettingsUpdate,
                RegisterKeys.ChannelChatUserMessageHold => RegChannelChatUserMessageHold,
                RegisterKeys.ChannelChatUserMessageUpdate => RegChannelChatUserMessageUpdate,
                RegisterKeys.ChannelCheer => RegChannelCheer,
                RegisterKeys.ChannelModeratorAdd => RegChannelModeratorAdd,
                RegisterKeys.ChannelModeratorRemove => RegChannelModeratorRemove,
                RegisterKeys.ChannelPointsAutomaticRewardRedemptionAdd => RegChannelPointsAutomaticRewardRedemptionAdd,
                RegisterKeys.ChannelPointsCustomRewardAdd => RegChannelPointsCustomRewardAdd,
                RegisterKeys.ChannelPointsCustomRewardRedemptionAdd => RegChannelPointsCustomRewardRedemptionAdd,
                RegisterKeys.ChannelPointsCustomRewardRedemptionUpdate => RegChannelPointsCustomRewardRedemptionUpdate,
                RegisterKeys.ChannelPointsCustomRewardRemove => RegChannelPointsCustomRewardRemove,
                RegisterKeys.ChannelPointsCustomRewardUpdate => RegChannelPointsCustomRewardUpdate,
                RegisterKeys.ChannelPollBegin => RegChannelPollBegin,
                RegisterKeys.ChannelPollEnd => RegChannelPollEnd,
                RegisterKeys.ChannelPollProgress => RegChannelPollProgress,
                RegisterKeys.ChannelPredictionBegin => RegChannelPredictionBegin,
                RegisterKeys.ChannelPredictionEnd => RegChannelPredictionEnd,
                RegisterKeys.ChannelPredictionLock => RegChannelPredictionLock,
                RegisterKeys.ChannelPredictionProgress => RegChannelPredictionProgress,
                RegisterKeys.ChannelRaid => RegChannelRaid,
                RegisterKeys.ChannelShieldModeBegin => RegChannelShieldModeBegin,
                RegisterKeys.ChannelShieldModeEnd => RegChannelShieldModeEnd,
                RegisterKeys.ChannelShoutoutCreate => RegChannelShoutoutCreate,
                RegisterKeys.ChannelShoutoutReceived => RegChannelShoutoutReceived,
                RegisterKeys.ChannelSubscribe => RegChannelSubscribe,
                RegisterKeys.ChannelSubscriptionEnd => RegChannelSubscriptionEnd,
                RegisterKeys.ChannelSubscriptionGift => RegChannelSubscriptionGift,
                RegisterKeys.ChannelSubscriptionMessage => RegChannelSubscriptionMessage,
                RegisterKeys.ChannelSuspiciousUserMessage => RegChannelSuspiciousUserMessage,
                RegisterKeys.ChannelSuspiciousUserUpdate => RegChannelSuspiciousUserUpdate,
                RegisterKeys.ChannelUnban => RegChannelUnban,
                RegisterKeys.ChannelUnbanRequestCreate => RegChannelUnbanRequestCreate,
                RegisterKeys.ChannelUnbanRequestResolve => RegChannelUnbanRequestResolve,
                RegisterKeys.ChannelUpdate => RegChannelUpdate,
                RegisterKeys.ChannelVIPAdd => RegChannelVIPAdd,
                RegisterKeys.ChannelVIPRemove => RegChannelVIPRemove,
                RegisterKeys.ChannelWarningAcknowledge => RegChannelWarningAcknowledge,
                RegisterKeys.ChannelWarningSend => RegChannelWarningSend,
                RegisterKeys.StreamOffline => RegStreamOffline,
                RegisterKeys.StreamOnline => RegStreamOnline,
                _ => throw new ArgumentException($"RegistryItem for key '{key}' not found."),
            };
        }

        private static List<ConditionTypes> CondList(params ConditionTypes[] types)
        {
            var list = new List<ConditionTypes>();
            list.AddRange(types);
            return list;
        }
    }
};