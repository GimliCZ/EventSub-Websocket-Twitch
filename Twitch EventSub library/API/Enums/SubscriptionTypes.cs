namespace Twitch.EventSub.API.Enums
{
    public enum SubscriptionTypes
    {
        AutomodMessageHold,

        // Channel Subscriptions
        ConduitShardDisabled,

        ChannelUpdate,
        ChannelFollow,
        ChannelAdBreakBegin,
        ChannelChatClear,
        ChannelChatClearUserMessages,
        ChannelChatMessage,
        ChannelChatUserMessageHold,
        ChannelChatMessageDelete,
        ChannelChatNotification,
        ChannelSubscribe,
        ChannelSubscriptionEnd,
        ChannelSubscriptionGift,
        ChannelSubscriptionMessage,
        ChannelCheer,
        ChannelRaid,
        ChannelBan,
        ChannelUnban,
        ChannelUnbanCreate,
        ChannelUnbanResolve,
        ChannelModeratorAdd,
        ChannelModeratorRemove,
        ChannelVIPAdd,
        ChannelVIPRemove,
        ChannelWarningAcknowledge,
        ChannelWarningSend,
        ChannelChatUserMessageUpdate,
        ChannelPointsAutomaticRewardRedemptionAdd,

        //Beta
        BetaChannelGuestStarSessionBegin,

        BetaChannelGuestStarSessionEnd,
        BetaChannelGuestStarGuestUpdate,
        BetaChannelGuestStarSettingsUpdate,

        // Channel Points
        ChannelPointsCustomRewardAdd,

        ChannelPointsCustomRewardUpdate,
        ChannelPointsCustomRewardRemove,
        ChannelPointsCustomRewardRedemptionAdd,
        ChannelPointsCustomRewardRedemptionUpdate,

        // Channel Poll
        ChannelPollBegin,

        ChannelPollProgress,
        ChannelPollEnd,

        // Channel Prediction
        ChannelPredictionBegin,

        ChannelPredictionProgress,
        ChannelPredictionLock,
        ChannelPredictionEnd,

        // Charity
        CharityDonation,

        CharityCampaignStart,
        CharityCampaignProgress,
        CharityCampaignStop,

        //webhook only
        /*
        // Drop Entitlement Grant
        DropEntitlementGrant,

        // Extension Bits Transaction
        ExtensionBitsTransactionCreate,
        */

        // Channel Goal
        ChannelGoalBegin,

        ChannelGoalProgress,
        ChannelGoalEnd,

        // Channel Hype Train
        ChannelHypeTrainBegin,

        ChannelHypeTrainProgress,
        ChannelHypeTrainEnd,

        // Channel Shield Mode
        ChannelShieldModeBegin,

        ChannelShieldModeEnd,

        // Channel Shoutout
        ChannelShoutoutCreate,

        ChannelShoutoutReceived,

        // Stream
        SuspiciousUserUpdate,

        StreamOffline,

        //webhook only
        /*
        // User Authorization
        UserAuthorizationGrant,
        UserAuthorizationRevoke,
        */

        // User Update
        UserUpdate,

        AutomodMessageUpdate,
        AutomodTermsUpdate,
        SuspiciousUserMessage,
        ChannelChatSettingsUpdate,
        StreamOnline
    }
}