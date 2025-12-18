using Microsoft.Extensions.Logging;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Extensions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Interfaces;
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

namespace Twitch.EventSub.User
{
    /// <summary>
    /// Primary source of events
    /// OnRefreshTokenAsync event is mandatory for propper function
    /// </summary>
    public class EventProvider : IEventProvider
    {
        private string _accessToken;
        private readonly string _clientId;
        private List<SubscriptionTypes> _listOfSubs;
        private readonly ILogger _logger;
        private readonly string _userId;
        private UserSequencer _userSequencer;
        private readonly Timer _recoveryTimer;
        private readonly bool _allowRecovery;
        private string? _testingApiUrl;
        private string? _testingWebsocketUrl;

        public EventProvider(
            string userId,
            string accessToken,
            List<SubscriptionTypes> listOfSubs,
            string clientId,
            ILogger logger,
            bool allowRecovery)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken, nameof(accessToken));
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId, nameof(clientId));
            ArgumentNullException.ThrowIfNull(listOfSubs, nameof(listOfSubs));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            _userId = userId;
            _accessToken = accessToken;
            _listOfSubs = listOfSubs;
            _clientId = clientId;
            _logger = logger;
            _recoveryTimer = new(_ => OnRecoveryTimerEnlapsedAsync(), null, Timeout.Infinite, Timeout.Infinite);
            _allowRecovery = allowRecovery;
            Create();
        }

        private async void OnRecoveryTimerEnlapsedAsync()
        {
            try
            {
                if (_userSequencer?.State == UserBase.UserState.Disposed && _allowRecovery == true)
                {
                    await StartAsync(_testingApiUrl, _testingWebsocketUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnRecoveryTimer failed due {message}", ex.Message);
            }
        }

        /// <summary>
        /// Directly reports Connection state from Socket, may be used for reconnect detection
        /// </summary>
        public bool IsConnected => _userSequencer?.Socket?.IsRunning == true;

        /// <summary>
        /// Notifies about connection termination.
        /// May contain also internal disconnect, so take with grain of salt
        /// </summary>
        public event EventHandler<string?> OnUnexpectedConnectionTermination;

        /// <summary>
        /// Mandatory event for refreshing Access Token. To Update token use Update procedure of client
        /// </summary>
        public event AsyncEventHandler<RefreshRequestArgs> OnRefreshTokenAsync;

        /// <summary>
        /// Raw messages
        /// </summary>
        public event AsyncEventHandler<string?> OnRawMessageAsync;

        /// <summary>
        /// Method to create UserSequencer instance and set up event handlers
        /// </summary>
        private void Create(string? testApiUrl = null, string? testWebsocketUrl = null)
        {
            var listOfRequests = new List<CreateSubscriptionRequest>();
            foreach (var type in _listOfSubs)
            {
                listOfRequests.Add(new CreateSubscriptionRequest()
                {
                    Transport = new Transport() { Method = "websocket" },
                    Condition = new Condition()
                }.SetSubscriptionType(type, _userId));
            }
            _userSequencer = new UserSequencer(_userId, _accessToken, listOfRequests, _clientId, _logger, testApiUrl, testWebsocketUrl);

            _userSequencer.AccessTokenRequestedEvent += AccessTokenRequestedEventAsync;
            _userSequencer.OnRawMessageRecievedAsync += OnRawMessageReceivedAsync;
            _userSequencer.OnOutsideDisconnectAsync += OnOutsideDisconnectAsync;
            _userSequencer.OnNotificationMessageAsync += Sequencer_OnNotificationMessageAsync;
            _userSequencer.OnDispose += _userSequencer_OnDispose;
        }

        private void _userSequencer_OnDispose(object? sender, string? e)
        {
            _userSequencer.AccessTokenRequestedEvent -= AccessTokenRequestedEventAsync;
            _userSequencer.OnRawMessageRecievedAsync -= OnRawMessageReceivedAsync;
            _userSequencer.OnOutsideDisconnectAsync -= OnOutsideDisconnectAsync;
            _userSequencer.OnNotificationMessageAsync -= Sequencer_OnNotificationMessageAsync;
            _userSequencer.OnDispose -= _userSequencer_OnDispose;
        }

        /// <summary>
        /// Method to start the UserSequencer instance asynchronously
        /// Regenerates Sequencer object, in case if its internaly disposed
        /// </summary>
        /// <returns></returns>
        internal Task StartAsync(string testingApiUrl = null, string testingWebsocketUrl = null)
        {
            if (_userSequencer.IsDisposed())
            {
                _testingApiUrl = testingApiUrl;
                _testingWebsocketUrl = testingWebsocketUrl;
                Create(testingApiUrl, testingWebsocketUrl);
            }
            ResolveRecovery(true);
            return _userSequencer.StartAsync();
        }

        private void ResolveRecovery(bool shouldRun)
        {
            if (_allowRecovery)
            {
                if (shouldRun)
                {
                    _recoveryTimer.Change(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
                }
                else
                {
                    _recoveryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Method to stop the UserSequencer instance asynchronously
        /// </summary>
        /// <returns>Returns true on success, false if object is in invalid state to be stopped.</returns>
        internal Task<bool> StopAsync()
        {
            ResolveRecovery(false);
            return _userSequencer.StopAsync();
        }

        /// <summary>
        /// Updates parameters of userSequencer
        /// Primary usage is to update Access Token.
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="listOfSubs"></param>
        /// <returns>Retuns true if update is successful</returns>
        internal bool Update(string accessToken, List<SubscriptionTypes> listOfSubs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken, nameof(accessToken));
            ArgumentNullException.ThrowIfNull(listOfSubs, nameof(listOfSubs));

            _accessToken = accessToken;
            _listOfSubs = listOfSubs;

            var listOfRequests = new List<CreateSubscriptionRequest>();
            foreach (var type in listOfSubs)
            {
                listOfRequests.Add(new CreateSubscriptionRequest()
                {
                    Transport = new Transport() { Method = "websocket" },
                    Condition = new Condition()
                }.SetSubscriptionType(type, _userSequencer.UserId));
            }

            return _userSequencer.Update(accessToken, listOfRequests);
        }

        /// <summary>
        /// Notification about outside disconnect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task OnOutsideDisconnectAsync(object sender, string? e)
        {
            OnUnexpectedConnectionTermination.Invoke(sender, e);
            return;
        }

        /// <summary>
        /// Raw message handler.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task OnRawMessageReceivedAsync(object message, string? e)
        {
            await OnRawMessageAsync.TryInvoke(message, e);
        }

        /// <summary>
        /// Access Token request handler.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private async Task AccessTokenRequestedEventAsync(object sender, RefreshRequestArgs ex)
        {
            await OnRefreshTokenAsync.TryInvoke(sender, ex);
        }

        /// <summary>
        /// Notification message handler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task Sequencer_OnNotificationMessageAsync(object sender, Messages.NotificationMessage.WebSocketNotificationPayload e)
        {
            switch (e.Event)
            {
                case AutomodMessageUpdateEvent automodMessageUpdateEvent:
                    await OnAutomodMessageUpdateEventAsync.TryInvoke(sender, automodMessageUpdateEvent);
                    break;
                
                case AutomodMessageUpdateEventV2 automodMessageUpdateEventV2:
                    await OnAutomodMessageUpdateV2EventAsync.TryInvoke(sender, automodMessageUpdateEventV2);
                    break;

                case AutomodMessageHoldEvent automodMessageHoldEvent:
                    await OnAutomodMessageHoldEventAsync.TryInvoke(sender, automodMessageHoldEvent);
                    break;
                
                case AutomodMessageHoldEventV2 automodMessageHoldEventV2:
                    await OnAutomodMessageHoldV2EventAsync.TryInvoke(sender, automodMessageHoldEventV2);
                    break;
                
                case AutomodSettingsUpdateEvent automodSettingsUpdateEvent:
                    await OnAutomodSettingsUpdateEventAsync.TryInvoke(sender, automodSettingsUpdateEvent);
                    break;

                case AutomodTermsUpdateEvent automodTermsUpdateEvent:
                    await OnAutomodTermsUpdateEventAsync.TryInvoke(sender, automodTermsUpdateEvent);
                    break;

                case ChannelAdBreakBeginEvent channelAdBreakBeginEvent:
                    await OnAdBreakBeginEventAsync.TryInvoke(sender, channelAdBreakBeginEvent);
                    break;

                case ChannelChatMessageDeleteEvent channelChatMessageDeleteEvent:
                    await OnChatMessageDeleteEventAsync.TryInvoke(sender, channelChatMessageDeleteEvent);
                    break;

                case ChannelChatNotificationEvent channelChatNotificationEvent:
                    await OnChatNotificationEventAsync.TryInvoke(sender, channelChatNotificationEvent);
                    break;

                case ChannelChatSettingsUpdateEvent channelChatSettingsUpdateEvent:
                    await OnChatSettingsUpdateEventAsync.TryInvoke(sender, channelChatSettingsUpdateEvent);
                    break;

                case ChannelSuspiciousUserMessageEvent channelSuspiciousUserMessageEvent:
                    await OnSuspiciousUserMessageEventAsync.TryInvoke(sender, channelSuspiciousUserMessageEvent);
                    break;

                case ChannelSuspiciousUserUpdateEvent channelSuspiciousUserUpdateEvent:
                    await OnSuspiciousUserUpdateEventAsync.TryInvoke(sender, channelSuspiciousUserUpdateEvent);
                    break;

                case ConduitShardDisabledEvent conduitShardDisabledEvent:
                    await OnConduitShardDisabledEventAsync.TryInvoke(sender, conduitShardDisabledEvent);
                    break;

                case ChannelUpdateEvent updateEvent:
                    await OnUpdateEventAsync.TryInvoke(sender, updateEvent);
                    break;

                case ChannelFollowEvent followEvent:
                    await OnFollowEventAsync.TryInvoke(sender, followEvent);
                    break;

                case ChannelSubscriptionEndEvent subscribeEndEvent:
                    await OnSubscribeEndEventAsync.TryInvoke(sender, subscribeEndEvent);
                    break;

                case ChannelSubscribeEvent subscribeEvent:
                    await OnSubscribeEventAsync.TryInvoke(sender, subscribeEvent);
                    break;

                case ChannelChatUserMessageHoldEvent messageHoldEvent:
                    await OnChatUserMessageHoldEventAsync.TryInvoke(sender, messageHoldEvent);
                    break;

                case ChannelChatUserMessageUpdateEvent messageHoldEvent:
                    await OnChatUserMessageUpdateEventAsync.TryInvoke(sender, messageHoldEvent);
                    break;

                case ChannelChatMessageEvent channelChatMessage:
                    await OnChatEventAsync.TryInvoke(sender, channelChatMessage);
                    break;

                case ChannelChatClearEvent clearEvent:
                    await OnChatClearEventAsync.TryInvoke(sender, clearEvent);
                    break;

                case ChannelChatClearUserMessagesEvent clearUserMessagesEvent:
                    await OnChatClearUserMessages.TryInvoke(sender, clearUserMessagesEvent);
                    break;

                case ChannelSubscriptionGiftEvent subscriptionGiftEvent:
                    await OnSubscriptionGiftEventAsync.TryInvoke(sender, subscriptionGiftEvent);
                    break;

                case ChannelSubscriptionMessageEvent subscriptionMessageEvent:
                    await OnSubscriptionMessageEventAsync.TryInvoke(sender, subscriptionMessageEvent);
                    break;

                case ChannelCheerEvent cheerEvent:
                    await OnCheerEventAsync.TryInvoke(sender, cheerEvent);
                    break;

                case ChannelRaidEvent raidEvent:
                    await OnRaidEventAsync.TryInvoke(sender, raidEvent);
                    break;

                case ChannelBanEvent banEvent:
                    await OnBanEventAsync.TryInvoke(sender, banEvent);
                    break;

                case ChannelUnbanEvent unbanEvent:
                    await OnUnbanEventAsync.TryInvoke(sender, unbanEvent);
                    break;

                case ChannelUnbanRequestCreateEvent unbanCreateEvent:
                    await OnUnbanRequestCreateEventAsync.TryInvoke(sender, unbanCreateEvent);
                    break;

                case ChannelUnbanRequestResolveEvent unBanResolveEvent:
                    await OnUnbanRequestResolveEventAsync.TryInvoke(sender, unBanResolveEvent);
                    break;

                case ChannelModeratorRemoveEvent moderatorRemoveEvent:
                    await OnModeratorRemoveEventAsync.TryInvoke(sender, moderatorRemoveEvent);
                    break;

                case ChannelModeratorAddEvent moderatorAddEvent:
                    await OnModeratorAddEventAsync.TryInvoke(sender, moderatorAddEvent);
                    break;

                case ChannelGuestStarSessionEndEvent guestStarSessionEndEvent:
                    await OnGuestStarSessionEndEventAsync.TryInvoke(sender, guestStarSessionEndEvent);
                    break;

                case ChannelGuestStarSessionBeginEvent guestStarSessionBeginEvent:
                    await OnGuestStarSessionBeginEventAsync.TryInvoke(sender, guestStarSessionBeginEvent);
                    break;

                case ChannelGuestStarGuestUpdateEvent guestStarGuestUpdateEvent:
                    await OnGuestStarGuestUpdateEventAsync.TryInvoke(sender, guestStarGuestUpdateEvent);
                    break;

                case ChannelGuestStarSettingsUpdateEvent guestStarSettingsUpdateEvent:
                    await OnGuestStarSettingsUpdateEventAsync.TryInvoke(sender, guestStarSettingsUpdateEvent);
                    break;

                case ChannelPointsAutomaticRewardRedemptionAddEvent channelPointsAutomaticRewardRedemptionAddEvent:
                    await OnPointsAutomaticRewardRedemptionAddEventAsync.TryInvoke(sender, channelPointsAutomaticRewardRedemptionAddEvent);
                    break;
                
                case ChannelPointsAutomaticRewardRedemptionAddV2Event  channelPointsAutomaticRewardRedemptionAddV2Event:
                    await OnPointsAutomaticRewardRedemptionAddV2EventAsync.TryInvoke(sender, channelPointsAutomaticRewardRedemptionAddV2Event);
                    break;

                case ChannelPointsCustomRewardUpdateEvent customRewardUpdateEvent:
                    await OnPointsCustomRewardUpdateEventAsync.TryInvoke(sender, customRewardUpdateEvent);
                    break;

                case ChannelPointsCustomRewardRemoveEvent customRewardRemoveEvent:
                    await OnPointsCustomRewardRemoveEventAsync.TryInvoke(sender, customRewardRemoveEvent);
                    break;

                case ChannelPointsCustomRewardRedemptionUpdateEvent customRewardRedemptionUpdateEvent:
                    await OnPointsCustomRewardRedemptionUpdateEventAsync.TryInvoke(sender, customRewardRedemptionUpdateEvent);
                    break;

                case ChannelPointsCustomRewardRedemptionAddEvent customRewardRedemptionAddEvent:
                    await OnPointsCustomRewardRedemptionAddEventAsync.TryInvoke(sender, customRewardRedemptionAddEvent);
                    break;

                case ChannelPointsCustomRewardAddEvent customRewardAddEvent:
                    await OnPointsCustomRewardAddEventAsync.TryInvoke(sender, customRewardAddEvent);
                    break;

                case ChannelPollProgressEvent pollProgressEvent:
                    await OnPollProgressEventAsync.TryInvoke(sender, pollProgressEvent);
                    break;

                case ChannelPollEndEvent pollEndEvent:
                    await OnPollEndEventAsync.TryInvoke(sender, pollEndEvent);
                    break;

                case ChannelPollBeginEvent pollBeginEvent:
                    await OnPollBeginEventAsync.TryInvoke(sender, pollBeginEvent);
                    break;

                case ChannelPredictionProgressEvent predictionProgressEvent:
                    await OnPredictionProgressEventAsync.TryInvoke(sender, predictionProgressEvent);
                    break;

                case ChannelPredictionLockEvent predictionLockEvent:
                    await OnPredictionLockEventAsync.TryInvoke(sender, predictionLockEvent);
                    break;

                case ChannelPredictionEndEvent predictionEndEvent:
                    await OnPredictionEndEventAsync.TryInvoke(sender, predictionEndEvent);
                    break;

                case ChannelPredictionBeginEvent predictionBeginEvent:
                    await OnPredictionBeginEventAsync.TryInvoke(sender, predictionBeginEvent);
                    break;

                case ChannelHypeTrainProgressEvent hypeTrainProgressEvent:
                    await OnHypeTrainProgressEventAsync.TryInvoke(sender, hypeTrainProgressEvent);
                    break;

                case ChannelHypeTrainEndEvent hypeTrainEndEvent:
                    await OnHypeTrainEndEventAsync.TryInvoke(sender, hypeTrainEndEvent);
                    break;

                case ChannelHypeTrainBeginEvent hypeTrainBeginEvent:
                    await OnHypeTrainBeginEventAsync.TryInvoke(sender, hypeTrainBeginEvent);
                    break;

                case ChannelCharityCampaignStartEvent charityCampaignStartEvent:
                    await OnCharityCampaignStartEventAsync.TryInvoke(sender, charityCampaignStartEvent);
                    break;

                case ChannelCharityCampaignProgressEvent charityCampaignProgressEvent:
                    await OnCharityCampaignProgressEventAsync.TryInvoke(sender, charityCampaignProgressEvent);
                    break;

                case ChannelCharityCampaignStopEvent charityCampaignStopEvent:
                    await OnCharityCampaignStopEventAsync.TryInvoke(sender, charityCampaignStopEvent);
                    break;

                case ChannelCharityDonationEvent charityDonationEvent:
                    await OnCharityDonationEventAsync.TryInvoke(sender, charityDonationEvent);
                    break;

                //Cant be used by websocket
                //case DropEntitlementGrantEvent dropEntitlementGrantEvent:
                //    await OnDropEntitlementGrantEventAsync.TryInvoke(sender, dropEntitlementGrantEvent);
                //    break;

                //case ExtensionBitsTransactionCreateEvent bitsTransactionCreateEvent:
                //    await OnExtensionBitsTransactionCreateEventAsync.TryInvoke(sender, bitsTransactionCreateEvent);
                //    break;

                case ChannelGoalProgressEvent goalProgressEvent:
                    await OnGoalProgressEventAsync.TryInvoke(sender, goalProgressEvent);
                    break;

                case ChannelGoalEndEvent goalEndEvent:
                    await OnGoalEndEventAsync.TryInvoke(sender, goalEndEvent);
                    break;

                case ChannelGoalBeginEvent goalBeginEvent:
                    await OnGoalBeginEventAsync.TryInvoke(sender, goalBeginEvent);
                    break;

                case ChannelShieldModeEndEvent shieldModeEndEvent:
                    await OnShieldModeEndEventAsync.TryInvoke(sender, shieldModeEndEvent);
                    break;

                case ChannelShieldModeBeginEvent shieldModeBeginEvent:
                    await OnShieldModeBeginEventAsync.TryInvoke(sender, shieldModeBeginEvent);
                    break;

                case ChannelShoutoutCreateEvent shoutoutCreateEvent:
                    await OnShoutoutCreateEventAsync.TryInvoke(sender, shoutoutCreateEvent);
                    break;

                case ChannelShoutoutReceivedEvent shoutoutReceivedEvent:
                    await OnShoutoutReceivedEventAsync.TryInvoke(sender, shoutoutReceivedEvent);
                    break;

                case StreamOnlineEvent streamOnlineEvent:
                    await OnStreamOnlineEventAsync.TryInvoke(sender, streamOnlineEvent);
                    break;

                case StreamOfflineEvent streamOfflineEvent:
                    await OnStreamOfflineEventAsync.TryInvoke(sender, streamOfflineEvent);
                    break;

                case ChannelVIPAddEvent vipAddEvent:
                    await OnVIPAddEventAsync.TryInvoke(sender, vipAddEvent);
                    break;

                case ChannelVIPRemoveEvent vipRemoveEvent:
                    await OnVIPRemoveEventAsync.TryInvoke(sender, vipRemoveEvent);
                    break;

                case ChannelWarningAcknowledgeEvent warningAcknowledgeEvent:
                    await OnWarningAcknowledgeEventAsync.TryInvoke(sender, warningAcknowledgeEvent);
                    break;

                case ChannelWarningSendEvent channelWarningSendEvent:
                    await OnWarningSendEventAsync.TryInvoke(sender, channelWarningSendEvent);
                    break;
                
                case ChannelBitsUseEvent channelBitsUseEvent:
                    await OnBitsUseEventAsync.TryInvoke(sender, channelBitsUseEvent);
                    break;
                
                case ChannelSharedChatSessionBeginEvent channelSharedChatSessionBeginEvent:
                    await OnSharedChatSessionBeginEventAsync.TryInvoke(sender, channelSharedChatSessionBeginEvent);
                    break;
                
                case ChannelSharedChatSessionUpdateEvent channelSharedChatSessionUpdateEvent:
                    await OnSharedChatSessionUpdateEventAsync.TryInvoke(sender, channelSharedChatSessionUpdateEvent);
                    break;
                
                case ChannelSharedChatSessionEndEvent channelSharedChatSessionEndEvent:
                    await OnSharedChatSessionEndEventAsync.TryInvoke(sender, channelSharedChatSessionEndEvent);
                    break;
                
                case UserWhisperReceivedEvent userWhisperReceivedEvent:
                    await OnWhisperReceivedEventAsync.TryInvoke(sender, userWhisperReceivedEvent);
                    break;
                
                case UserUpdateEvent userUpdateEvent:
                    await OnUserUpdateEventAsync.TryInvoke(sender, userUpdateEvent);
                    break;
                
                //Cant be used by websocket

                //case UserAuthorizationGrantEvent userAuthorizationGrantEvent:
                //    await OnUserAuthorizationGrantEventAsync.TryInvoke(sender, userAuthorizationGrantEvent);
                //    break;

                //case UserAuthorizationRevokeEvent userAuthorizationRevokeEvent:
                //    await OnUserAuthorizationRevokeEventAsync.TryInvoke(sender, userAuthorizationRevokeEvent);
                //    break;
                
                default:
                    throw new NotImplementedException();
            }
        }

        #region Available events

        public event AsyncEventHandler<AutomodMessageHoldEvent> OnAutomodMessageHoldEventAsync;

        public event AsyncEventHandler<ChannelUnbanRequestCreateEvent> OnUnbanRequestCreateEventAsync;

        public event AsyncEventHandler<ChannelUnbanRequestResolveEvent> OnUnbanRequestResolveEventAsync;

        public event AsyncEventHandler<ChannelChatClearUserMessagesEvent> OnChatClearUserMessages;

        public event AsyncEventHandler<ChannelChatSettingsUpdateEvent> OnChatSettingsUpdateEventAsync;

        public event AsyncEventHandler<AutomodMessageUpdateEvent> OnAutomodMessageUpdateEventAsync;
        
        public event AsyncEventHandler<AutomodTermsUpdateEvent> OnAutomodTermsUpdateEventAsync;

        public event AsyncEventHandler<ChannelAdBreakBeginEvent> OnAdBreakBeginEventAsync;

        public event AsyncEventHandler<ChannelChatMessageDeleteEvent> OnChatMessageDeleteEventAsync;

        public event AsyncEventHandler<ChannelChatNotificationEvent> OnChatNotificationEventAsync;

        public event AsyncEventHandler<ChannelSuspiciousUserMessageEvent> OnSuspiciousUserMessageEventAsync;

        public event AsyncEventHandler<ChannelSuspiciousUserUpdateEvent> OnSuspiciousUserUpdateEventAsync;

        public event AsyncEventHandler<ChannelVIPAddEvent> OnVIPAddEventAsync;

        public event AsyncEventHandler<ChannelVIPRemoveEvent> OnVIPRemoveEventAsync;

        public event AsyncEventHandler<ChannelWarningAcknowledgeEvent> OnWarningAcknowledgeEventAsync;

        public event AsyncEventHandler<ChannelWarningSendEvent> OnWarningSendEventAsync;

        public event AsyncEventHandler<ConduitShardDisabledEvent> OnConduitShardDisabledEventAsync;

        public event AsyncEventHandler<ChannelUpdateEvent> OnUpdateEventAsync;

        public event AsyncEventHandler<ChannelFollowEvent> OnFollowEventAsync;

        public event AsyncEventHandler<ChannelChatUserMessageHoldEvent> OnChatUserMessageHoldEventAsync;

        public event AsyncEventHandler<ChannelChatUserMessageUpdateEvent> OnChatUserMessageUpdateEventAsync;

        public event AsyncEventHandler<ChannelChatClearEvent> OnChatClearEventAsync;

        public event AsyncEventHandler<ChannelChatMessageEvent> OnChatEventAsync;

        public event AsyncEventHandler<ChannelSubscribeEvent> OnSubscribeEventAsync;

        public event AsyncEventHandler<ChannelSubscriptionEndEvent> OnSubscribeEndEventAsync;

        public event AsyncEventHandler<ChannelSubscriptionGiftEvent> OnSubscriptionGiftEventAsync;

        public event AsyncEventHandler<ChannelSubscriptionMessageEvent> OnSubscriptionMessageEventAsync;

        public event AsyncEventHandler<ChannelCheerEvent> OnCheerEventAsync;

        public event AsyncEventHandler<ChannelRaidEvent> OnRaidEventAsync;

        public event AsyncEventHandler<ChannelBanEvent> OnBanEventAsync;

        public event AsyncEventHandler<ChannelUnbanEvent> OnUnbanEventAsync;

        public event AsyncEventHandler<ChannelModeratorAddEvent> OnModeratorAddEventAsync;

        public event AsyncEventHandler<ChannelModeratorRemoveEvent> OnModeratorRemoveEventAsync;

        public event AsyncEventHandler<ChannelGuestStarSessionBeginEvent> OnGuestStarSessionBeginEventAsync;

        public event AsyncEventHandler<ChannelGuestStarSessionEndEvent> OnGuestStarSessionEndEventAsync;

        public event AsyncEventHandler<ChannelGuestStarGuestUpdateEvent> OnGuestStarGuestUpdateEventAsync;

        public event AsyncEventHandler<ChannelGuestStarSettingsUpdateEvent> OnGuestStarSettingsUpdateEventAsync;

        public event AsyncEventHandler<ChannelPointsAutomaticRewardRedemptionAddEvent> OnPointsAutomaticRewardRedemptionAddEventAsync;

        public event AsyncEventHandler<ChannelPointsCustomRewardAddEvent> OnPointsCustomRewardAddEventAsync;

        public event AsyncEventHandler<ChannelPointsCustomRewardUpdateEvent> OnPointsCustomRewardUpdateEventAsync;

        public event AsyncEventHandler<ChannelPointsCustomRewardRemoveEvent> OnPointsCustomRewardRemoveEventAsync;

        public event AsyncEventHandler<ChannelPointsCustomRewardRedemptionAddEvent> OnPointsCustomRewardRedemptionAddEventAsync;

        public event AsyncEventHandler<ChannelPointsCustomRewardRedemptionUpdateEvent> OnPointsCustomRewardRedemptionUpdateEventAsync;

        public event AsyncEventHandler<ChannelPollBeginEvent> OnPollBeginEventAsync;

        public event AsyncEventHandler<ChannelPollProgressEvent> OnPollProgressEventAsync;

        public event AsyncEventHandler<ChannelPollEndEvent> OnPollEndEventAsync;

        public event AsyncEventHandler<ChannelPredictionBeginEvent> OnPredictionBeginEventAsync;

        public event AsyncEventHandler<ChannelPredictionProgressEvent> OnPredictionProgressEventAsync;

        public event AsyncEventHandler<ChannelPredictionLockEvent> OnPredictionLockEventAsync;

        public event AsyncEventHandler<ChannelPredictionEndEvent> OnPredictionEndEventAsync;

        public event AsyncEventHandler<ChannelCharityDonationEvent> OnCharityDonationEventAsync;

        public event AsyncEventHandler<ChannelCharityCampaignStartEvent> OnCharityCampaignStartEventAsync;

        public event AsyncEventHandler<ChannelCharityCampaignProgressEvent> OnCharityCampaignProgressEventAsync;

        public event AsyncEventHandler<ChannelCharityCampaignStopEvent> OnCharityCampaignStopEventAsync;

        //public event AsyncEventHandler<DropEntitlementGrantEvent> OnDropEntitlementGrantEventAsync;
        //public event AsyncEventHandler<ExtensionBitsTransactionCreateEvent> OnExtensionBitsTransactionCreateEventAsync;
        public event AsyncEventHandler<ChannelGoalBeginEvent> OnGoalBeginEventAsync;

        public event AsyncEventHandler<ChannelGoalProgressEvent> OnGoalProgressEventAsync;

        public event AsyncEventHandler<ChannelGoalEndEvent> OnGoalEndEventAsync;

        public event AsyncEventHandler<ChannelHypeTrainBeginEvent> OnHypeTrainBeginEventAsync;

        public event AsyncEventHandler<ChannelHypeTrainProgressEvent> OnHypeTrainProgressEventAsync;

        public event AsyncEventHandler<ChannelHypeTrainEndEvent> OnHypeTrainEndEventAsync;

        public event AsyncEventHandler<ChannelShieldModeBeginEvent> OnShieldModeBeginEventAsync;

        public event AsyncEventHandler<ChannelShieldModeEndEvent> OnShieldModeEndEventAsync;

        public event AsyncEventHandler<ChannelShoutoutCreateEvent> OnShoutoutCreateEventAsync;

        public event AsyncEventHandler<ChannelShoutoutReceivedEvent> OnShoutoutReceivedEventAsync;

        public event AsyncEventHandler<StreamOnlineEvent> OnStreamOnlineEventAsync;

        public event AsyncEventHandler<StreamOfflineEvent> OnStreamOfflineEventAsync;

        //public event AsyncEventHandler<UserAuthorizationGrantEvent> OnUserAuthorizationGrantEventAsync;
        //public event AsyncEventHandler<UserAuthorizationRevokeEvent> OnUserAuthorizationRevokeEventAsync;
        //public event AsyncEventHandler<UserUpdateEvent> OnUserUpdateEventAsync;
        
        public event AsyncEventHandler<ChannelSharedChatSessionBeginEvent>  OnSharedChatSessionBeginEventAsync;
        public event AsyncEventHandler<ChannelSharedChatSessionUpdateEvent>  OnSharedChatSessionUpdateEventAsync; 
        public event AsyncEventHandler<ChannelSharedChatSessionEndEvent> OnSharedChatSessionEndEventAsync;
        public event AsyncEventHandler<ChannelBitsUseEvent>  OnBitsUseEventAsync;
        public event AsyncEventHandler<UserUpdateEvent>  OnUserUpdateEventAsync;
        public event AsyncEventHandler<UserWhisperReceivedEvent> OnWhisperReceivedEventAsync; 
        public event AsyncEventHandler<AutomodMessageHoldEventV2>  OnAutomodMessageHoldV2EventAsync; 
        public event AsyncEventHandler<AutomodMessageUpdateEventV2>  OnAutomodMessageUpdateV2EventAsync; 
        public event AsyncEventHandler<AutomodSettingsUpdateEvent>  OnAutomodSettingsUpdateEventAsync; 
        public event AsyncEventHandler<ChannelPointsAutomaticRewardRedemptionAddV2Event>  OnPointsAutomaticRewardRedemptionAddV2EventAsync; 

        #endregion Available events
    }
}