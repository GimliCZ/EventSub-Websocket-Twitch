using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
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

namespace Twitch.EventSub.Interfaces
{
    /// <summary>
    /// Primary source of events
    /// OnRefreshTokenAsync event is mandatory for propper function
    /// </summary>
    public interface IEventProvider
    {
        /// <summary>
        /// Directly reports Connection state from Socket, may be used for reconnect detection
        /// </summary>
        bool IsConnected { get; }

        event AsyncEventHandler<ChannelAdBreakBeginEvent> OnAdBreakBeginEventAsync;

        event AsyncEventHandler<AutomodMessageHoldEvent> OnAutomodMessageHoldEventAsync;

        event AsyncEventHandler<AutomodMessageUpdateEvent> OnAutomodMessageUpdateEventAsync;

        event AsyncEventHandler<AutomodTermsUpdateEvent> OnAutomodTermsUpdateEventAsync;

        event AsyncEventHandler<ChannelBanEvent> OnBanEventAsync;

        event AsyncEventHandler<ConduitShardDisabledEvent> OnConduitShardDisabledEventAsync;

        event AsyncEventHandler<ChannelFollowEvent> OnFollowEventAsync;

        event AsyncEventHandler<ChannelGoalBeginEvent> OnGoalBeginEventAsync;

        event AsyncEventHandler<ChannelGoalEndEvent> OnGoalEndEventAsync;

        event AsyncEventHandler<ChannelGoalProgressEvent> OnGoalProgressEventAsync;

        event AsyncEventHandler<ChannelGuestStarGuestUpdateEvent> OnGuestStarGuestUpdateEventAsync;

        event AsyncEventHandler<ChannelGuestStarSessionBeginEvent> OnGuestStarSessionBeginEventAsync;

        event AsyncEventHandler<ChannelGuestStarSessionEndEvent> OnGuestStarSessionEndEventAsync;

        event AsyncEventHandler<ChannelGuestStarSettingsUpdateEvent> OnGuestStarSettingsUpdateEventAsync;

        event AsyncEventHandler<ChannelHypeTrainBeginEvent> OnHypeTrainBeginEventAsync;

        event AsyncEventHandler<ChannelHypeTrainEndEvent> OnHypeTrainEndEventAsync;

        event AsyncEventHandler<ChannelHypeTrainProgressEvent> OnHypeTrainProgressEventAsync;

        event AsyncEventHandler<ChannelChatClearEvent> OnChatClearEventAsync;

        event AsyncEventHandler<ChannelChatClearUserMessagesEvent> OnChatClearUserMessages;

        event AsyncEventHandler<ChannelChatMessageEvent> OnChatEventAsync;

        event AsyncEventHandler<ChannelChatSettingsUpdateEvent> OnChatSettingsUpdateEventAsync;

        event AsyncEventHandler<ChannelChatUserMessageHoldEvent> OnChatUserMessageHoldEventAsync;

        event AsyncEventHandler<ChannelChatUserMessageUpdateEvent> OnChatUserMessageUpdateEventAsync;

        event AsyncEventHandler<ChannelPointsAutomaticRewardRedemptionAddEvent> OnPointsAutomaticRewardRedemptionAddEventAsync;

        event AsyncEventHandler<ChannelUpdateEvent> OnUpdateEventAsync;

        event AsyncEventHandler<ChannelVIPAddEvent> OnVIPAddEventAsync;

        event AsyncEventHandler<ChannelVIPRemoveEvent> OnVIPRemoveEventAsync;

        event AsyncEventHandler<ChannelWarningAcknowledgeEvent> OnWarningAcknowledgeEventAsync;

        event AsyncEventHandler<ChannelWarningSendEvent> OnWarningSendEventAsync;

        event AsyncEventHandler<ChannelCharityCampaignProgressEvent> OnCharityCampaignProgressEventAsync;

        event AsyncEventHandler<ChannelCharityCampaignStartEvent> OnCharityCampaignStartEventAsync;

        event AsyncEventHandler<ChannelCharityCampaignStopEvent> OnCharityCampaignStopEventAsync;

        event AsyncEventHandler<ChannelCharityDonationEvent> OnCharityDonationEventAsync;

        event AsyncEventHandler<ChannelChatMessageDeleteEvent> OnChatMessageDeleteEventAsync;

        event AsyncEventHandler<ChannelChatNotificationEvent> OnChatNotificationEventAsync;

        event AsyncEventHandler<ChannelCheerEvent> OnCheerEventAsync;

        event AsyncEventHandler<ChannelModeratorAddEvent> OnModeratorAddEventAsync;

        event AsyncEventHandler<ChannelModeratorRemoveEvent> OnModeratorRemoveEventAsync;

        event AsyncEventHandler<ChannelPointsCustomRewardAddEvent> OnPointsCustomRewardAddEventAsync;

        event AsyncEventHandler<ChannelPointsCustomRewardRedemptionAddEvent> OnPointsCustomRewardRedemptionAddEventAsync;

        event AsyncEventHandler<ChannelPointsCustomRewardRedemptionUpdateEvent> OnPointsCustomRewardRedemptionUpdateEventAsync;

        event AsyncEventHandler<ChannelPointsCustomRewardRemoveEvent> OnPointsCustomRewardRemoveEventAsync;

        event AsyncEventHandler<ChannelPointsCustomRewardUpdateEvent> OnPointsCustomRewardUpdateEventAsync;

        event AsyncEventHandler<ChannelPollBeginEvent> OnPollBeginEventAsync;

        event AsyncEventHandler<ChannelPollEndEvent> OnPollEndEventAsync;

        event AsyncEventHandler<ChannelPollProgressEvent> OnPollProgressEventAsync;

        event AsyncEventHandler<ChannelPredictionBeginEvent> OnPredictionBeginEventAsync;

        event AsyncEventHandler<ChannelPredictionEndEvent> OnPredictionEndEventAsync;

        event AsyncEventHandler<ChannelPredictionLockEvent> OnPredictionLockEventAsync;

        event AsyncEventHandler<ChannelPredictionProgressEvent> OnPredictionProgressEventAsync;

        event AsyncEventHandler<ChannelRaidEvent> OnRaidEventAsync;

        event AsyncEventHandler<string?> OnRawMessageAsync;

        /// <summary>
        /// Mandatory event for refreshing Access Token. To Update token use Update procedure of client
        /// </summary>
        event AsyncEventHandler<RefreshRequestArgs> OnRefreshTokenAsync;

        event AsyncEventHandler<ChannelShieldModeBeginEvent> OnShieldModeBeginEventAsync;

        event AsyncEventHandler<ChannelShieldModeEndEvent> OnShieldModeEndEventAsync;

        event AsyncEventHandler<ChannelShoutoutCreateEvent> OnShoutoutCreateEventAsync;

        event AsyncEventHandler<ChannelShoutoutReceivedEvent> OnShoutoutReceivedEventAsync;

        event AsyncEventHandler<StreamOfflineEvent> OnStreamOfflineEventAsync;

        event AsyncEventHandler<StreamOnlineEvent> OnStreamOnlineEventAsync;

        event AsyncEventHandler<ChannelSubscriptionEndEvent> OnSubscribeEndEventAsync;

        event AsyncEventHandler<ChannelSubscribeEvent> OnSubscribeEventAsync;

        event AsyncEventHandler<ChannelSubscriptionGiftEvent> OnSubscriptionGiftEventAsync;

        event AsyncEventHandler<ChannelSubscriptionMessageEvent> OnSubscriptionMessageEventAsync;

        event AsyncEventHandler<ChannelSuspiciousUserMessageEvent> OnSuspiciousUserMessageEventAsync;

        event AsyncEventHandler<ChannelSuspiciousUserUpdateEvent> OnSuspiciousUserUpdateEventAsync;

        event AsyncEventHandler<ChannelUnbanEvent> OnUnbanEventAsync;

        event AsyncEventHandler<ChannelUnbanRequestCreateEvent> OnUnbanRequestCreateEventAsync;

        event AsyncEventHandler<ChannelUnbanRequestResolveEvent> OnUnbanRequestResolveEventAsync;

        /// <summary>
        /// Notifies about connection termination.
        /// May contain also internal disconnect, so take with grain of salt
        /// </summary>
        event EventHandler<string?> OnUnexpectedConnectionTermination;
        
        public event AsyncEventHandler<ChannelSharedChatSessionBeginEvent>  OnSharedChatSessionBeginEventAsync;
        public event AsyncEventHandler<ChannelSharedChatSessionUpdateEvent>  OnSharedChatSessionUpdateEventAsync; 
        public event AsyncEventHandler<ChannelSharedChatSessionEndEvent> OnSharedChatSessionEndEventAsync;
        public event AsyncEventHandler<ChannelBitsUseEvent>  OnBitsUseEventAsync;
        public event AsyncEventHandler<UserUpdateEvent>  OnUserUpdateEventAsync; 
        public event AsyncEventHandler<AutomodMessageHoldEventV2>  OnAutomodMessageHoldV2EventAsync; 
        public event AsyncEventHandler<AutomodMessageUpdateEventV2>  OnAutomodMessageUpdateV2EventAsync; 
        public event AsyncEventHandler<AutomodSettingsUpdateEvent>  OnAutomodSettingsUpdateEventAsync; 
        public event AsyncEventHandler<ChannelPointsAutomaticRewardRedemptionAddV2Event>  OnPointsAutomaticRewardRedemptionAddV2EventAsync; 
        public event AsyncEventHandler<UserWhisperReceivedEvent> OnWhisperReceivedEventAsync; 
        
    }
}