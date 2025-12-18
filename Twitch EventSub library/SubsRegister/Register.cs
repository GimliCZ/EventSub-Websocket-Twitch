using System.Reflection;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.Messages.NotificationMessage.Events;
using Twitch.EventSub.Messages.NotificationMessage.Events.Automod;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelBits;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelCharity;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelCheer;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelGoal;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelGuest;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelHype;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelModerator;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints.Models;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoll;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPrediction;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShared;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShield;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShoutout;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSuspicious;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelUnban;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelVIP;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelWarning;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.Messages.NotificationMessage.Events.User;
using Twitch.EventSub.SubsRegister.Models;

namespace Twitch.EventSub.SubsRegister
{
    public static class Register
    {
        public static readonly RegisterItem RegAutomodMessageHold = new RegisterItem
        {
            Key = RegisterKeys.AutomodMessageHold.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(AutomodMessageHoldEvent),
            SubscriptionType = SubscriptionTypes.AutomodMessageHold,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };
        
        public static readonly RegisterItem RegAutomodMessageHoldV2 = new RegisterItem
        {
            Key = RegisterKeys.AutomodMessageHoldV2.ToEventString(),
            Ver = "2",
            SpecificObject = typeof(AutomodMessageHoldEventV2),
            SubscriptionType = SubscriptionTypes.AutomodMessageHoldV2,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegAutomodMessageUpdate = new RegisterItem
        {
            Key = RegisterKeys.AutomodMessageUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(AutomodMessageUpdateEvent),
            SubscriptionType = SubscriptionTypes.AutomodMessageUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };
        
        public static readonly RegisterItem RegAutomodMessageUpdateV2 = new RegisterItem
        {
            Key = RegisterKeys.AutomodMessageUpdateV2.ToEventString(),
            Ver = "2",
            SpecificObject = typeof(AutomodMessageUpdateEventV2),
            SubscriptionType = SubscriptionTypes.AutomodMessageUpdateV2,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegAutomodTermsUpdate = new RegisterItem
        {
            Key = RegisterKeys.AutomodTermsUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(AutomodTermsUpdateEvent),
            SubscriptionType = SubscriptionTypes.AutomodTermsUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegAutomodSettingsUpdate = new RegisterItem()
        {
            Key = RegisterKeys.AutomodSettingsUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(AutomodSettingsUpdateEvent),
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId),
            SubscriptionType = SubscriptionTypes.AutomodSettingsUpdate
        };

        public static readonly RegisterItem RegChannelBitsUse = new RegisterItem()
        {
            Key = RegisterKeys.ChannelBitsUse.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelBitsUseEvent),
            SubscriptionType = SubscriptionTypes.ChannelBitsUse,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegConduitShardDisabled = new RegisterItem
        {
            Key = RegisterKeys.ConduitShardDisabled.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ConduitShardDisabledEvent),
            SubscriptionType = SubscriptionTypes.ConduitShardDisabled,
            Conditions = CondList(ConditionTypes.ClientId)
        };

        public static readonly RegisterItem RegChannelAdBreakBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelAdBreakBegin.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelAdBreakBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelAdBreakBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelBan = new RegisterItem
        {
            Key = RegisterKeys.ChannelBan.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelBanEvent),
            SubscriptionType = SubscriptionTypes.ChannelBan,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelFollow = new RegisterItem
        {
            Key = RegisterKeys.ChannelFollow.ToEventString(),
            Ver = "2",
            SpecificObject = typeof(ChannelFollowEvent),
            SubscriptionType = SubscriptionTypes.ChannelFollow,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelGoalBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelGoalBegin.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelGoalBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelGoalBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGoalEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelGoalEnd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelGoalEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelGoalEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGoalProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelGoalProgress.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelGoalProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelGoalProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarGuestUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarGuestUpdate.ToEventString(),
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarGuestUpdateEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarGuestUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarSessionBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarSessionBegin.ToEventString(),
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarSessionBeginEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarSessionBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarSessionEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarSessionEnd.ToEventString(),
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarSessionEndEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarSessionEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelGuestStarSettingsUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelGuestStarSettingsUpdate.ToEventString(),
            Ver = "beta",
            SpecificObject = typeof(ChannelGuestStarSettingsUpdateEvent),
            SubscriptionType = SubscriptionTypes.BetaChannelGuestStarSettingsUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelHypeTrainBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelHypeTrainBegin.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelHypeTrainBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelHypeTrainBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelHypeTrainEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelHypeTrainEnd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelHypeTrainEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelHypeTrainEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelHypeTrainProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelHypeTrainProgress.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelHypeTrainProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelHypeTrainProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityCampaignProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityCampaignProgress.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelCharityCampaignProgressEvent),
            SubscriptionType = SubscriptionTypes.CharityCampaignProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityCampaignStart = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityCampaignStart.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelCharityCampaignStartEvent),
            SubscriptionType = SubscriptionTypes.CharityCampaignStart,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityCampaignStop = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityCampaignStop.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelCharityCampaignStopEvent),
            SubscriptionType = SubscriptionTypes.CharityCampaignStop,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelCharityDonation = new RegisterItem
        {
            Key = RegisterKeys.ChannelCharityDonation.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelCharityDonationEvent),
            SubscriptionType = SubscriptionTypes.CharityDonation,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatClear = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatClear.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatClearEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatClear,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatClearUserMessages = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatClearUserMessages.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatClearUserMessagesEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatClearUserMessages,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem RegChannelChatMessage = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatMessage.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatMessageEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatMessage,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem RegChannelChatMessageDelete = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatMessageDelete.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatMessageDeleteEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatMessageDelete,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatNotification = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatNotification.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatNotificationEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatNotification,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatSettingsUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatSettingsUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatSettingsUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatSettingsUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelChatUserMessageHold = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatUserMessageHold.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatUserMessageHoldEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatUserMessageHold,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem RegChannelChatUserMessageUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelChatUserMessageUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelChatUserMessageUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelChatUserMessageUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.UserId)
        };

        public static readonly RegisterItem  RegChannelSharedChatSessionBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelSharedChatSessionBegin.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSharedChatSessionBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelSharedChatSessionBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };
        public static readonly RegisterItem RegChannelSharedChatSessionUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelSharedChatSessionUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSharedChatSessionUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelSharedChatSessionUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };
        public static readonly RegisterItem RegChannelSharedChatSessionEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelSharedChatSessionEnd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSharedChatSessionEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelSharedChatSessionEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };
        public static readonly RegisterItem RegChannelCheer = new RegisterItem
        {
            Key = RegisterKeys.ChannelCheer.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelCheerEvent),
            SubscriptionType = SubscriptionTypes.ChannelCheer,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelModeratorAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelModeratorAdd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelModeratorAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelModeratorAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelModeratorRemove = new RegisterItem
        {
            Key = RegisterKeys.ChannelModeratorRemove.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelModeratorRemoveEvent),
            SubscriptionType = SubscriptionTypes.ChannelModeratorRemove,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPointsAutomaticRewardRedemptionAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsAutomaticRewardRedemptionAdd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPointsAutomaticRewardRedemptionAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsAutomaticRewardRedemptionAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };
        
        public static readonly RegisterItem RegChannelPointsAutomaticRewardRedemptionAddV2 = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsAutomaticRewardRedemptionAddV2.ToEventString(),
            Ver = "2",
            SpecificObject = typeof(ChannelPointsAutomaticRewardRedemptionAddV2Event),
            SubscriptionType = SubscriptionTypes.ChannelPointsAutomaticRewardRedemptionAddV2,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardAdd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardRedemptionAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardRedemptionAdd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardRedemptionAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardRedemptionAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardRedemptionUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardRedemptionUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardRedemptionUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardRedemptionUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardRemove = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardRemove.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardRemoveEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardRemove,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPointsCustomRewardUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelPointsCustomRewardUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPointsCustomRewardUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelPointsCustomRewardUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.RewardId)
        };

        public static readonly RegisterItem RegChannelPollBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelPollBegin.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPollBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelPollBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPollEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPollEnd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPollEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelPollEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPollProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelPollProgress.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPollProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelPollProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionBegin.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionEnd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionLock = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionLock.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionLockEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionLock,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelPredictionProgress = new RegisterItem
        {
            Key = RegisterKeys.ChannelPredictionProgress.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelPredictionProgressEvent),
            SubscriptionType = SubscriptionTypes.ChannelPredictionProgress,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelRaid = new RegisterItem
        {
            Key = RegisterKeys.ChannelRaid.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelRaidEvent),
            SubscriptionType = SubscriptionTypes.ChannelRaid,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelShieldModeBegin = new RegisterItem
        {
            Key = RegisterKeys.ChannelShieldModeBegin.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelShieldModeBeginEvent),
            SubscriptionType = SubscriptionTypes.ChannelShieldModeBegin,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelShieldModeEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelShieldModeEnd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelShieldModeEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelShieldModeEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelShoutoutCreate = new RegisterItem
        {
            Key = RegisterKeys.ChannelShoutoutCreate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelShoutoutCreateEvent),
            SubscriptionType = SubscriptionTypes.ChannelShoutoutCreate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelShoutoutReceived = new RegisterItem
        {
            Key = RegisterKeys.ChannelShoutoutReceived.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelShoutoutReceivedEvent),
            SubscriptionType = SubscriptionTypes.ChannelShoutoutReceived,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelSubscribe = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscribe.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSubscribeEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscribe,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSubscriptionEnd = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscriptionEnd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSubscriptionEndEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscriptionEnd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSubscriptionGift = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscriptionGift.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSubscriptionGiftEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscriptionGift,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSubscriptionMessage = new RegisterItem
        {
            Key = RegisterKeys.ChannelSubscriptionMessage.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSubscriptionMessageEvent),
            SubscriptionType = SubscriptionTypes.ChannelSubscriptionMessage,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelUnban = new RegisterItem
        {
            Key = RegisterKeys.ChannelUnban.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelUnbanEvent),
            SubscriptionType = SubscriptionTypes.ChannelUnban,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelUnbanRequestCreate = new RegisterItem
        {
            Key = RegisterKeys.ChannelUnbanRequestCreate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelUnbanRequestCreateEvent),
            SubscriptionType = SubscriptionTypes.ChannelUnbanCreate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelUnbanRequestResolve = new RegisterItem
        {
            Key = RegisterKeys.ChannelUnbanRequestResolve.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelUnbanRequestResolveEvent),
            SubscriptionType = SubscriptionTypes.ChannelUnbanResolve,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelUpdate.ToEventString(),
            Ver = "2",
            SpecificObject = typeof(ChannelUpdateEvent),
            SubscriptionType = SubscriptionTypes.ChannelUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelVIPAdd = new RegisterItem
        {
            Key = RegisterKeys.ChannelVIPAdd.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelVIPAddEvent),
            SubscriptionType = SubscriptionTypes.ChannelVIPAdd,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelVIPRemove = new RegisterItem
        {
            Key = RegisterKeys.ChannelVIPRemove.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelVIPRemoveEvent),
            SubscriptionType = SubscriptionTypes.ChannelVIPRemove,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelWarningAcknowledge = new RegisterItem
        {
            Key = RegisterKeys.ChannelWarningAcknowledge.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelWarningAcknowledgeEvent),
            SubscriptionType = SubscriptionTypes.ChannelWarningAcknowledge,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegChannelWarningSend = new RegisterItem
        {
            Key = RegisterKeys.ChannelWarningSend.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelWarningSendEvent),
            SubscriptionType = SubscriptionTypes.ChannelWarningSend,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegStreamOffline = new RegisterItem
        {
            Key = RegisterKeys.StreamOffline.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(StreamOfflineEvent),
            SubscriptionType = SubscriptionTypes.StreamOffline,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegStreamOnline = new RegisterItem
        {
            Key = RegisterKeys.StreamOnline.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(StreamOnlineEvent),
            SubscriptionType = SubscriptionTypes.StreamOnline,
            Conditions = CondList(ConditionTypes.BroadcasterUserId)
        };

        public static readonly RegisterItem RegChannelSuspiciousUserMessage = new RegisterItem
        {
            Key = RegisterKeys.ChannelSuspiciousUserMessage.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSuspiciousUserMessageEvent),
            SubscriptionType = SubscriptionTypes.SuspiciousUserMessage,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegUserUpdate = new RegisterItem()
        {
            Key = RegisterKeys.UserUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(UserUpdateEvent),
            SubscriptionType = SubscriptionTypes.UserUpdate,
            Conditions = CondList(ConditionTypes.ClientId)
        };

        public static readonly RegisterItem RegChannelSuspiciousUserUpdate = new RegisterItem
        {
            Key = RegisterKeys.ChannelSuspiciousUserUpdate.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(ChannelSuspiciousUserUpdateEvent),
            SubscriptionType = SubscriptionTypes.SuspiciousUserUpdate,
            Conditions = CondList(ConditionTypes.BroadcasterUserId, ConditionTypes.ModeratorUserId)
        };

        public static readonly RegisterItem RegUserWhisperReceived = new RegisterItem()
        {
            Key = RegisterKeys.UserWhisperReceived.ToEventString(),
            Ver = "1",
            SpecificObject = typeof(UserWhisperReceivedEvent),
            SubscriptionType = SubscriptionTypes.UserWhisperReceived,
            Conditions = CondList(ConditionTypes.UserId)
        };
        
        // AUTO-GENERATED COLLECTIONS - Initialized ONCE in static constructor
        
        /// <summary>
        /// Dictionary keyed by RegisterKeyVersion (event + version) - populated once during static initialization
        /// Perfect for routing with version specificity
        /// </summary>
        public static readonly Dictionary<RegisterKeyVersion, RegisterItem> RegisterDictionaryByVersion;

        /// <summary>
        /// Static constructor - runs ONCE when the class is first accessed
        /// All reflection happens here and never again
        /// </summary>
        static Register()
        {
            // Get all RegisterItems once via reflection
            var allItems = GetAllRegisterItems();
            RegisterDictionaryByVersion = new Dictionary<RegisterKeyVersion, RegisterItem>();
            
            foreach (var item in allItems)
            {
                // Populate RegisterDictionary and RegisterDictionaryByVersion
                if (RegisterKeysExtensions.TryFromEventString(item.Key, out var registerKey))
                {
                    // For RegisterDictionaryByVersion, use composite key
                    var keyVersion = new RegisterKeyVersion(registerKey.ToEventString(), item.Ver);
                    RegisterDictionaryByVersion[keyVersion] = item;
                }
            }
        }

        /// <summary>
        /// Gets all RegisterItem static fields using reflection
        /// Called ONLY ONCE during static initialization
        /// </summary>
        private static List<RegisterItem> GetAllRegisterItems()
        {
            var items = new List<RegisterItem>();
            var type = typeof(Register);
            
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(RegisterItem) && f.Name.StartsWith("Reg"));
            
            foreach (var field in fields)
            {
                if (field.GetValue(null) is RegisterItem item)
                {
                    items.Add(item);
                }
            }
            
            return items;
        }

        private static List<ConditionTypes> CondList(params ConditionTypes[] types)
        {
            var list = new List<ConditionTypes>();
            list.AddRange(types);
            return list;
        }
    }
};