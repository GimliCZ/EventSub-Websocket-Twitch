using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Reactive.Linq;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.PingMessage;
using Twitch.EventSub.Messages.ReconnectMessage;
using Twitch.EventSub.Messages.RevocationMessage;
using Twitch.EventSub.Messages.WelcomeMessage;
using Websocket.Client;

namespace Twitch.EventSub.User
{
    /// <summary>
    /// Manages user-specific EventSub sequences, handling WebSocket connections, state transitions,
    /// subscription management, and processing various types of messages from Twitch.
    /// </summary>
    public class UserSequencer : UserBase
    {
        //Socket is currently running in sequence mode
        //Each blocking operation must be done within 10 seconds, else we risk missing messages
        //This also serves as additional layer of protection, if events run way to long
        private const int RevocationResubscribeTolerance = 1000; //[ms]

        private const int StopGroupUnsubscribeTolerance = 5000; //[ms]
        private const int RunGroupSubscribeTolerance = 5000; //[ms]
        private const int AccessTokenValidationTolerance = 5000; //[ms]
        private const int WelcomeMessageDelayTolerance = 1000;//[ms]
        private const int NewAccessTokenRequestDelay = 1000;//[ms]
        private const int NumberOfRetries = 3;
        private readonly AsyncAutoResetEvent _awaitMessage = new(false);
        private readonly AsyncAutoResetEvent _awaitRefresh = new(false);
        private readonly ILogger _logger;
        private readonly Timer _managerTimer;
        private readonly ReplayProtection _replayProtection;
        private readonly SubscriptionManager _subscriptionManager;
        private readonly Watchdog _watchdog;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserSequencer"/> class.
        /// </summary>
        /// <param name="id">User ID.</param>
        /// <param name="access">Access token.</param>
        /// <param name="requestedSubscriptions">List of requested subscriptions.</param>
        /// <param name="clientId">Client ID.</param>
        /// <param name="logger">Logger instance.</param>
        public UserSequencer(string id, string access, List<CreateSubscriptionRequest> requestedSubscriptions, string clientId, ILogger logger, string apiTestingUrl = null, string socketTestingUrl = null) : base(id, access, requestedSubscriptions, socketTestingUrl)
        {
            _logger = logger;
            ClientId = clientId;
            _watchdog = new Watchdog(logger);
            _replayProtection = new ReplayProtection(10);
            _subscriptionManager = new SubscriptionManager(apiTestingUrl);
            _logger.LogDebug("[UserSequencer] Initialized with UserId: {UserId}, ClientId: {ClientId}", id, clientId);
            _managerTimer = new Timer(_ => OnManagerTimerEnlapsedAsync(), null, Timeout.Infinite, Timeout.Infinite);
            _watchdog.OnWatchdogTimeout -= OnWatchdogTimeoutAsync;
            _watchdog.OnWatchdogTimeout += OnWatchdogTimeoutAsync;
        }

        public event CoreFunctions.AsyncEventHandler<string?> OnRawMessageRecievedAsync;

        public event CoreFunctions.AsyncEventHandler<string?> OnOutsideDisconnectAsync;

        public event CoreFunctions.AsyncEventHandler<RefreshRequestArgs> AccessTokenRequestedEvent;

        public event CoreFunctions.AsyncEventHandler<WebSocketNotificationPayload> OnNotificationMessageAsync;

        /// <summary>
        /// Handles the periodic refresh of subscriptions.
        /// </summary>
        private async void OnManagerTimerEnlapsedAsync()
        {
            try
            {
                var tries = 0;
                while (tries < NumberOfRetries)
                {
                    //This is fix for state, when we want to do checks for subscriptions
                    //And we are right in middle of access token refresh or other non critical state
                    //Reason why are we not stopping manager and restarting it in refresh, is so that we retain
                    //inicial subscription timing.
                    if (StateMachine.CanFire(UserActions.RunningProceed))
                    {
                        await StateMachine.FireAsync(UserActions.RunningProceed);
                        return;
                    }
                    else
                    {
                        // this is so that we leave termination states as fast as possible.
                        if (StateMachine.IsInState(UserState.Stoping) ||
                            StateMachine.IsInState(UserState.Failing) ||
                            StateMachine.IsInState(UserState.Disposed))
                        {
                            return;
                        }

                        tries++;
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("ManagerTimer returned error. {ex}", ex);
            }
        }

        /// <summary>
        /// Executes the handshake process, validating initial subscriptions.enException
        /// </summary>
        protected override async Task RunHandshakeAsync()
        {
            _logger.LogDebug("[RunHandshakeAsync] Starting handshake for UserId: {UserId}", UserId);
            _subscriptionManager.OnRefreshTokenRequestAsync -= OnRefreshTokenRequestAsync;
            _subscriptionManager.OnRefreshTokenRequestAsync += OnRefreshTokenRequestAsync;
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                var checkOk = await _subscriptionManager.RunCheckAsync(
                    UserId,
                    RequestedSubscriptions,
                    ClientId,
                    AccessToken,
                    SessionId,
                    cts,
                    _logger
                    );

                await HandShakeNextActionAsync(checkOk);
            }
        }

        private async Task HandShakeNextActionAsync(bool checkOk)
        {
            if (checkOk)
            {
                _logger.LogDebug("[RunHandshakeAsync] Handshake success for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.HandShakeSuccess);
            }
            else
            {
                _logger.LogDebug("[RunHandshakeAsync] Handshake failed for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.HandShakeFail);
            }
        }

        /// <summary>
        /// Handles refresh token request events.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Exception containing user ID and time of exception</exception>
        private async Task OnRefreshTokenRequestAsync(object sender, RefreshRequestArgs e)
        {
            _logger.LogDebug($"RefreshToken request: {e}");
            if (UserId != e.UserId)
            {
                _logger.LogError("[OnRefreshTokenRequestAsync] SourceUserId does not match UserId: {UserId}", UserId);
                return;
            }
            if (e is null)
            {
                _logger.LogError("[OnRefreshTokenRequestAsync] Got Null Invalid Access Token Exception");
            }
            LastAccessViolationArgs = e;
            _logger.LogDebug("[OnRefreshTokenRequestAsync] InvalidAccessTokenException received for UserId: {UserId}, State: {State}", UserId, StateMachine.State);
            await RefreshTokenNextActionAsync(e);
            _awaitRefresh.Set();
        }

        private async Task RefreshTokenNextActionAsync(RefreshRequestArgs e)
        {
            switch (StateMachine.State)
            {
                case UserState.Running:
                    _logger.LogDebug($"[OnRefreshTokenRequestAsync] Invoking Running access token renew procedure {e}");
                    await StateMachine.FireAsync(UserActions.RunningAccessFail);
                    break;

                case UserState.HandShake:
                    _logger.LogDebug($"[OnRefreshTokenRequestAsync] Invoking Handshake access token renew procedure {e}");
                    await StateMachine.FireAsync(UserActions.HandShakeAccessFail);
                    break;

                case UserState.InitialAccessTest:
                    _logger.LogDebug($"[OnRefreshTokenRequestAsync] Invoking Test access token renew procedure {e}");
                    await StateMachine.FireAsync(UserActions.AccessFailed);
                    break;

                case UserState.Stoping:
                    //We should probably attempt to refresh token to clear subscriptions,
                    //but since we are stopping or reseting session and subscriptions without connection clear anyway,
                    //we can just ignore it.
                    _logger.LogDebug($"[OnRefreshTokenRequestAsync] Access token request triggered during subscription clear while Stoping [ignore]");
                    break;

                case UserState.ReconnectingFromWatchdog:
                    _logger.LogDebug($"[OnRefreshTokenRequestAsync] Access token request triggered during subscription clear while watchdog Reconnecting [ignore]");
                    break;

                default:
                    _logger.LogError("[OnRefreshTokenRequestAsync] Unexpected state: {State}", StateMachine.State);
                    throw new InvalidOperationException("[EventSubClient] - [UserSequencer] OnRefreshTokenRequestAsync went into unknown state");
            }
        }

        /// <summary>
        /// Awaits the welcome message from the WebSocket.
        /// </summary>
        protected override async Task AwaitWelcomeMessageAsync()
        {
            _logger.LogDebug("[AwaitWelcomeMessageAsync] Awaiting welcome message for UserId: {UserId}", UserId);
            try
            {
                using (var cls = new CancellationTokenSource(WelcomeMessageDelayTolerance))
                {
                    await _awaitMessage.WaitAsync(cls.Token);
                    _logger.LogDebug("[AwaitWelcomeMessageAsync] Welcome message received for UserId: {UserId}", UserId);
                    await StateMachine.FireAsync(UserActions.WelcomeMessageSuccess);
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Welcome message didn't come in time. Exception message: " + ex.Message, ex, Socket, DateTime.Now);
                await StateMachine.FireAsync(UserActions.WelcomeMessageFail);
            }
        }

        /// <summary>
        /// Runs the subscription manager
        /// </summary>
        protected override async Task RunManagerAsync()
        {
            {
                _logger.LogDebug("[RunManagerAsync] Running manager for UserId: {UserId}", UserId);
                _managerTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ManagerCancelationSource = new CancellationTokenSource(RunGroupSubscribeTolerance);
                var checkOk = await _subscriptionManager.RunCheckAsync(
                    UserId,
                    RequestedSubscriptions,
                    ClientId,
                    AccessToken,
                    SessionId,
                    ManagerCancelationSource,
                    _logger
                    );
                await RunManagerNextActionAsync(checkOk);
            }
        }

        private async Task RunManagerNextActionAsync(bool checkOk)
        {
            if (checkOk)
            {
                //repeat after 30 minutes
                _logger.LogDebug("[RunManagerAsync] Manager check successful for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.RunningAwait);
            }
            else
            {
                _logger.LogDebug("[RunManagerAsync] Manager check failed for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.Fail);
            }
        }

        /// <summary>
        /// Schedules the next run of the subscription manager.
        /// </summary>
        protected override async Task AwaitManagerAsync()
        {
            _managerTimer.Change(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        /// <summary>
        /// Stops the subscription manager.
        /// </summary>
        protected async Task StopManagerAsync()
        {
            _managerTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _logger.LogDebug("[StopManagerAsync] Stopping manager for UserId: {UserId}", UserId);
            await ManagerCancelationSource.CancelAsync();
        }

        /// <summary>
        /// Runs the WebSocket connection.
        /// </summary>
        protected override async Task RunWebsocketAsync()
        {
            _logger.LogDebug("[RunWebsocketAsync] Running WebSocket for UserId: {UserId}", UserId);
            if (Socket.IsRunning)
            {
                _logger.LogInformation("[EventSubClient] - [UserSequencer] Socket already active");
                return;
            }
            Socket = new WebsocketClient(Url);
            Socket.MessageReceived.Select(msg => Observable.FromAsync(() => SocketOnMessageReceivedAsync(this, msg.Text))).Concat().Subscribe();
            Socket.DisconnectionHappened.Select(disconnectInfo => Observable.FromAsync(() => OnServerSideTerminationAsync(this, disconnectInfo))).Concat().Subscribe();
            await Socket.Start();
            await RunWebsocketNextActionAsync();
        }

        private async Task RunWebsocketNextActionAsync()
        {
            if (Socket.IsRunning)
            {
                _logger.LogDebug("[RunWebsocketAsync] WebSocket started successfully for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.WebsocketSuccess);
            }
            else
            {
                _logger.LogDebug("[RunWebsocketAsync] WebSocket failed to start for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.WebsocketFail);
            }
        }

        /// <summary>
        /// Tests for intended or unintended disconnect
        /// Handles server-side termination events.
        /// </summary>
        /// <param name="userSequencer">Event sender.</param>
        /// <param name="disconnectInfo">Disconnection information.</param>
        /// <returns></returns>
        private async Task OnServerSideTerminationAsync(UserSequencer userSequencer, DisconnectionInfo disconnectInfo)
        {
            _watchdog.Stop();

            if (disconnectInfo.Type == DisconnectionType.ByUser)
            {
                _logger.LogInformation("[EventSubClient] - [UserSequencer] Socket correctly disconnected");
                return;
            }
            //this is case, when we transition to watchdog reconnect and twitch servers request disconnect at same time.
            if (StateMachine.IsInState(UserState.ReconnectingFromWatchdog))
            {
                _logger.LogInformation("[EventSubClient] - [UserSequencer] Watchdog triggered Reconnect. Socket disconnected.");
                return;
            }
            _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Socket Disconnected from outside", disconnectInfo);
            await StateMachine.FireAsync(UserActions.WebsocketFail);
        }

        /// <summary>
        /// Handles message processing
        /// </summary>
        /// <param name="userSequencer">Event sender.</param>
        /// <param name="text">Message</param>
        /// <returns></returns>
        private async Task SocketOnMessageReceivedAsync(UserSequencer userSequencer, string? text)
        {
            _logger.LogDebug("[SocketOnMessageReceivedAsync] Message received: {Text}", text);
            if (!string.IsNullOrEmpty(text))
            {
                await ParseWebSocketMessageAsync(text);
            }
        }

        private async Task<Task> ParseWebSocketMessageAsync(string e)
        {
            _logger.LogDebug("[ParseWebSocketMessageAsync] Parsing message: {Message}", e);
            try
            {
                WebSocketMessage message;
                await OnRawMessageRecievedAsync(this, e);
                try
                {
                    message = await MessageProcessing.DeserializeMessageAsync(e);
                }
                catch (JsonException ex)
                {
                    // Log the parsing error and return immediately
                    _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Error while parsing WebSocket message: ", e, ex);
                    return Task.CompletedTask;
                }

                if (_replayProtection.IsDuplicate(message.Metadata.MessageId) ||
                    !_replayProtection.IsUpToDate(message.Metadata.MessageTimestamp))
                {
                    _logger.LogDebug("[ParseWebSocketMessageAsync] Duplicate or outdated message: {MessageId}", message.Metadata.MessageId);
                    return Task.CompletedTask;
                }

                return message switch
                {
                    WebSocketWelcomeMessage welcomeMessage => WelcomeMessageProcessingAsync(welcomeMessage),
                    WebSocketKeepAliveMessage => KeepAliveMessageProcessingAsync(),
                    WebSocketPingMessage => PingMessageProcessingAsync(),
                    WebSocketNotificationMessage notificationMessage => NotificationMessageProcessingAsync(notificationMessage),
                    WebSocketReconnectMessage reconnectMessage => ReconnectMessageProcessingAsync(reconnectMessage),
                    WebSocketRevocationMessage revocationMessage => RevocationMessageProcessingAsync(revocationMessage),
                    _ => throw new JsonSerializationException($"Unsupported message_type: {message}")
                };
            }
            catch (Exception ex)
            {
                // Catch any other unexpected exceptions and log them
                _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Unexpected error while processing WebSocket message: ", ex);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Procedure for Initial testing of access token
        /// </summary>
        /// <returns></returns>
        protected override async Task InitialAccessTokenAsync()
        {
            {
                _logger.LogDebug("[InitialAccessTokenAsync] Validating initial access token for UserId: {UserId}", UserId);
                using (CancellationTokenSource cts = new CancellationTokenSource(AccessTokenValidationTolerance))
                {
                    var validationResult = await _subscriptionManager.ApiTryValidateAsync(AccessToken, UserId, _logger, cts);
                    await InicialAccessTokenNextActionAsync(validationResult);
                }
            }
        }

        private async Task InicialAccessTokenNextActionAsync(bool validationResult)
        {
            if (validationResult)
            {
                _logger.LogDebug("[InitialAccessTokenAsync] Initial access token validated for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.AccessSuccess);
            }
            else
            {
                _logger.LogDebug("[InitialAccessTokenAsync] Initial access token validation failed for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.AccessFailed);
            }
        }

        /// <summary>
        /// Awaits access token refresh
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        protected override async Task NewAccessTokenRequestAsync()
        {
            using (var cls = new CancellationTokenSource(NewAccessTokenRequestDelay))
            {
                _logger.LogDebug("[NewAccessTokenRequestAsync] Requesting new access token for UserId: {UserId}", UserId);
                await _awaitRefresh.WaitAsync(cls.Token);
                if (LastAccessViolationArgs != null)
                {
                    var invalidToken = AccessToken;
                    await AccessTokenRequestedEvent.TryInvoke(this, LastAccessViolationArgs);
                    _logger.LogDebugDetails("[EventSubClient] - [UserSequencer] AccessToken refreshed requested," +
                        " Old token, new token, time of request", invalidToken, AccessToken, LastAccessViolationArgs);
                    var NewToken = AccessToken;
                    await NewAccessTokenNextActionAsync(invalidToken, NewToken);
                }
                else
                {
                    _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Request for New Access token didnt contain valid exception", LastAccessViolationArgs);
                    await StateMachine.FireAsync(UserActions.AwaitNewTokenFailed);
                }
            }
        }

        private async Task NewAccessTokenNextActionAsync(string invalidToken, string NewToken)
        {
            if (invalidToken == NewToken)
            {
                _logger.LogDebug("[NewAccessTokenRequestAsync] New token is the same as the invalid token for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.AwaitNewTokenFailed);
            }
            switch (StateMachine.State)
            {
                case UserState.AwaitNewTokenAfterFailedTest:
                    _logger.LogDebug($"[NewAccessTokenRequestAsync] Returning to Test after Access Token renew {invalidToken}");
                    await StateMachine.FireAsync(UserActions.NewTokenProvidedReturnToInitialTest);
                    break;

                case UserState.AwaitNewTokenAfterFailedHandShake:
                    _logger.LogDebug($"[NewAccessTokenRequestAsync] Returning to Handshake after Access Token renew {invalidToken}");
                    await StateMachine.FireAsync(UserActions.NewTokenProvidedReturnToHandShake);
                    break;

                case UserState.AwaitNewTokenAfterFailedRun:
                    _logger.LogDebug($"[NewAccessTokenRequestAsync] Returning to Run after Access Token renew {invalidToken}");
                    await StateMachine.FireAsync(UserActions.NewTokenProvidedReturnToRunning);
                    break;

                default:
                    _logger.LogError("[NewAccessTokenRequestAsync] Unexpected state: {State}", StateMachine.State);
                    throw new InvalidOperationException("NewAccessTokenRequest run into invalid state");
            };
        }

        /// <summary>
        /// Welcome message parsing
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task WelcomeMessageProcessingAsync(WebSocketWelcomeMessage message)
        {
            _logger.LogDebug("[WelcomeMessageProcessingAsync] Processing welcome message for UserId: {UserId}", UserId);
            if (message?.Payload?.Session?.Id != null)
            {
                SessionId = message.Payload.Session.Id;
                _logger.LogDebugDetails("[EventSubClient] - [UserSequencer] Welcome message detected, Session captured", message, DateTime.Now);
            }
            else
            {
                _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Session key invalid", message, DateTime.Now);
            }

            // keep alive in sec + 10% tolerance
            if (message?.Payload?.Session.KeepAliveTimeoutSeconds != null)
            {
                var _keepAlive = (message.Payload.Session.KeepAliveTimeoutSeconds.Value * 1000 + 100);
                _awaitMessage.Set();
                _watchdog.Start(_keepAlive);
                _logger.LogDebugDetails("[EventSubClient] - [UserSequencer] Welcome message proccesed", message, DateTime.Now);
            }
            else
            {
                _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Welcome message detected, but did not contain keep alive.", message, DateTime.Now);
            }
        }

        /// <summary>
        /// Message processing
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task NotificationMessageProcessingAsync(WebSocketNotificationMessage message)
        {
            _watchdog.Reset();
            if (message.Payload != null)
            {
                await OnNotificationMessageAsync(this, message.Payload);
            }
            _logger.LogDebugDetails("[EventSubClient] - [UserSequencer] Notification message detected", message, DateTime.Now);
        }

        /// <summary>
        /// Reconnect procedure
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task ReconnectMessageProcessingAsync(WebSocketReconnectMessage message)
        {
            if (StateMachine.CanFire(UserActions.ReconnectRequested))
            {
                _logger.LogDebug("[ReconnectMessageProcessingAsync] Processing reconnect message for UserId: {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.ReconnectRequested);
            }
            else if (StateMachine.State == UserState.ReconnectingFromWatchdog)
            {
                //This is a fix for twitch triggering watchdog before sending reconnect message.
                return;
            }

            _watchdog.Stop();

            if (message?.Payload?.Session.ReconnectUrl != null)
            {
                if (Uri.TryCreate(message.Payload.Session.ReconnectUrl, new UriCreationOptions() { DangerousDisablePathAndQueryCanonicalization = false }, out var Url))
                {
                    Socket.Url = Url;
                    await Socket.ReconnectOrFail();
                    Socket.MessageReceived.Select(msg => Observable.FromAsync(() => SocketOnMessageReceivedAsync(this, msg.Text))).Concat().Subscribe();
                    Socket.DisconnectionHappened.Select(disconnectInfo => Observable.FromAsync(() => OnServerSideTerminationAsync(this, disconnectInfo))).Concat().Subscribe();
                    if (!Socket.IsRunning)
                    {
                        _logger.LogInformationDetails("[EventSubClient] - [UserSequencer] connection lost during reconnect", message, Socket);
                        await StateMachine.FireAsync(UserActions.ReconnectFail);
                        return;
                    }
                }
                else
                {
                    _logger.LogInformationDetails("[EventSubClient] - [UserSequencer] Didn't recieve valid Url during Reconnect", message, Socket);
                    await StateMachine.FireAsync(UserActions.ReconnectFail);
                    return;
                }
            }
            if (message.Payload.Session.KeepAliveTimeoutSeconds.HasValue)
            {
                _watchdog.Start(message.Payload.Session.KeepAliveTimeoutSeconds.Value);
            }
            else
            {
                _watchdog.Start(30);
                _logger.LogInformationDetails("[EventSubClient] - [UserSequencer] Reconnect keep alive value not provided, trying to insert 30s", message, Socket);
            }

            if (!string.IsNullOrEmpty(message?.Payload?.Session.Id))
            {
                SessionId = message.Payload.Session.Id;
            }
            else
            {
                _logger.LogInformationDetails("[EventSubClient] - [UserSequencer] Provided invalid session. Terminiting", message, Socket);
                await StateMachine.FireAsync(UserActions.ReconnectFail);
            }
            await StateMachine.FireAsync(UserActions.ReconnectSuccess);
        }

        /// <summary>
        /// When subscription sieses to exist. We attempt to recover it outside of standard check.
        /// This may be for changes of accesses to subscriptions
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task RevocationMessageProcessingAsync(WebSocketRevocationMessage e)
        {
            _logger.LogDebug("[RevocationMessageProcessingAsync] Processing revocation message for UserId: {UserId}", UserId);
            if (RequestedSubscriptions == null || ClientId == null || AccessToken == null)
            {
                _logger.LogInformation("[EventSubClient] - [SubscriptionManager] Revocation Resolver got subscriptions, clientId or accessToken as Null");
                return;
            }
            foreach (var sub in RequestedSubscriptions.Where(sub => sub.Type == e?.Payload?.Subscription?.Type && sub.Version == e?.Payload?.Subscription?.Version))
            {
                using (var cls = new CancellationTokenSource(RevocationResubscribeTolerance))
                {
                    if (!await _subscriptionManager.ApiTrySubscribeAsync(ClientId, AccessToken, sub, UserId, _logger, cls))
                    {
                        _logger.LogInformation("[EventSubClient] - [SubscriptionManager] Failed to subscribe subscription during revocation");
                        return;
                    }
                }
                _logger.LogInformationDetails("[EventSubClient] - [SubscriptionManager] Refreshed sub due revocation: " + sub.Type + "caused by ", e);
            }
            _logger.LogDebugDetails("[EventSubClient] - [UserSequencer] Revocation message detected", e);
        }

        /// <summary>
        /// Ping response
        /// </summary>
        /// <returns></returns>
        private Task PingMessageProcessingAsync()
        {
            _logger.LogDebug("[PingMessageProcessingAsync] Ping message detected");
            Socket.Send("Pong");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Keep alive processing
        /// </summary>
        /// <returns></returns>
        private Task KeepAliveMessageProcessingAsync()
        {
            _logger.LogDebug("[KeepAliveMessageProcessingAsync] KeepAlive message detected");
            _watchdog.Reset();
            _logger.LogDebugDetails("[EventSubClient] - [UserSequencer] KeepAlive message detected", DateTime.Now);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Triggers when server didn't respond in time.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task OnWatchdogTimeoutAsync(object sender, string e)
        {
            if (StateMachine.CanFire(UserActions.ReconnectFromWatchdog))
            {
                await StateMachine.FireAsync(UserActions.ReconnectFromWatchdog);
                return;
            }
            else if (StateMachine.State == UserState.Reconnecting)
            {
                //This is solution for case when we get reconnect message, but we are too slow and trigger watchdog anyway.
                return;
            }
            //This is case when we are not in valid state for watchdog reconnection, for example handshake and etc.
            //Eventually this case will be scares, but for now it will need recovery
            await Socket.Stop(WebSocketCloseStatus.NormalClosure, "Server didn't respond in time");
            _logger.LogWarningDetails("[EventSubClient] - [UserSequencer] Server didn't respond in time and program was not in state of safe reconnect recovery", sender, e, DateTime.Now);
            if (OnOutsideDisconnectAsync != null)
            {
                await OnOutsideDisconnectAsync.TryInvoke(this, e);
            }
            _watchdog.Stop();
            _watchdog.OnWatchdogTimeout -= OnWatchdogTimeoutAsync;
            Socket.Dispose();
            await StateMachine.FireAsync(UserActions.Fail);
        }

        protected override async Task StopProcedureAsync()
        {
            _logger.LogDebug("[StopProcedureAsync] Stopping procedure for UserId: {UserId}", UserId);
            await StopManagerAsync();
            using (var cls = new CancellationTokenSource(StopGroupUnsubscribeTolerance))
            {
                await _subscriptionManager.ClearAsync(ClientId, AccessToken, UserId, _logger, cls);
            }
            _watchdog.Stop();
            await Socket.Stop(WebSocketCloseStatus.NormalClosure, "Closing");
            await StateMachine.FireAsync(UserActions.Dispose);
        }

        protected override async Task FailProcedureAsync()
        {
            _logger.LogDebug("[FailProcedureAsync] Failing procedure for UserId: {UserId}", UserId);
            await StopManagerAsync();
            _watchdog.Stop();
            if (Socket.IsRunning)
            {
                await Socket.Stop(WebSocketCloseStatus.NormalClosure, "Closing");
            }
            await StateMachine.FireAsync(UserActions.Dispose);
        }

        protected override async Task ReconnectingAfterWatchdogFailAsync()
        {
            await StopManagerAsync();
            using (var cls = new CancellationTokenSource(StopGroupUnsubscribeTolerance))
            {
                await _subscriptionManager.ClearAsync(ClientId, AccessToken, UserId, _logger, cls);
            }
            await Socket.Stop(WebSocketCloseStatus.NormalClosure, "Server didn't respond in time");
            _logger.LogDebugDetails("[EventSubClient] - [UserSequencer] Server didn't respond in time", DateTime.Now);
            _watchdog.Stop();
            Socket.Dispose();
            //Reinicialize state machine
            await StateMachine.FireAsync(UserActions.AccessTesting);
        }

        protected override void UnhandeledState(UserState state, UserActions actions)
        {
            _logger.LogWarning($"State machine run into invalid state {state} while attempting to switch with action {actions}"
                + "Please report this error to the developer of library.");
        }
    }
}