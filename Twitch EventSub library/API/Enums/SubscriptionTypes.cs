namespace Twitch.EventSub.API.Enums
{
    public enum SubscriptionTypes
    {
        AutomodMessageHold,
        AutomodMessageHoldV2,

        // Channel Subscriptions
        ConduitShardDisabled,

        ChannelUpdate,
        ChannelFollow,
        ChannelAdBreakBegin,
        ChannelBitsUse,
        ChannelChatClear,
        ChannelChatClearUserMessages,
        ChannelChatMessage,
        ChannelChatUserMessageHold,
        ChannelChatMessageDelete,
        ChannelChatNotification,
        ChannelSharedChatSessionBegin,
        ChannelSharedChatSessionUpdate,
        ChannelSharedChatSessionEnd,
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
        ChannelPointsAutomaticRewardRedemptionAddV2,

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
        
        UserUpdate,
        UserWhisperReceived,
        AutomodMessageUpdate,
        AutomodMessageUpdateV2,
        AutomodTermsUpdate,
        AutomodSettingsUpdate,
        SuspiciousUserMessage,
        ChannelChatSettingsUpdate,
        StreamOnline
    }
}