using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Newtonsoft.Json;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.PingMessage;
using Twitch.EventSub.Messages.ReconnectMessage;
using Twitch.EventSub.Messages.RevocationMessage;
using Twitch.EventSub.Messages.WelcomeMessage;

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
        private const int WelcomeMessageDelayTolerance = 10_000; // [ms] — spec requires 10 seconds from connect to Welcome
        private const int NewAccessTokenRequestDelay = 1000;//[ms]
        private const int NumberOfRetries = 3;
        private readonly AsyncAutoResetEvent _awaitMessage = new(false);
        private readonly AsyncAutoResetEvent _awaitRefresh = new(false);
        private readonly ILogger _logger;
        private readonly Timer _managerTimer;
        private IShardBinding? _shardBinding;
        private static readonly TimeSpan ReconnectGraceTimeout = TimeSpan.FromSeconds(30);
        private int _keepAliveMs = 10_100; // default: 10s + 100ms tolerance, overwritten by Welcome message
        private readonly IConduitOrchestrator _conduitOrchestrator;
        private readonly string _appAccessToken;
        protected override ILogger? Logger => _logger;

        /// <summary>
        /// Reports whether the shard binding is active (has a valid session ID).
        /// </summary>
        public bool IsConnected => !string.IsNullOrEmpty(_shardBinding?.SessionId);
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
        public UserSequencer(string id, string access, List<CreateSubscriptionRequest> requestedSubscriptions, string clientId, ILogger logger, TwitchApi twitchApi, IConduitOrchestrator conduitOrchestrator, string appAccessToken, string apiTestingUrl = null, string socketTestingUrl = null) : base(id, access, requestedSubscriptions)
        {
            _logger = logger;
            ClientId = clientId;
            _conduitOrchestrator = conduitOrchestrator;
            _appAccessToken = appAccessToken;
            _watchdog = new Watchdog(logger);
            _replayProtection = new ReplayProtection(10);
            _subscriptionManager = new SubscriptionManager(twitchApi, apiTestingUrl);
            _logger.LogDebug("[UserSequencer] Initialized with UserId: {UserId}, ClientId: {ClientId}", id, clientId);
            _managerTimer = new Timer(_ => OnManagerTimerEnlapsedAsync(), null, Timeout.Infinite, Timeout.Infinite);
            _watchdog.OnWatchdogTimeout -= OnWatchdogTimeoutAsync;
            _watchdog.OnWatchdogTimeout += OnWatchdogTimeoutAsync;
        }

        public void SetShardBinding(IShardBinding binding)
        {
            _shardBinding = binding;
            _shardBinding.OnShardLost += async (_, _) =>
            {
                if (StateMachine.CanFire(UserActions.WebsocketFail))
                {
                    await StateMachine.FireAsync(UserActions.WebsocketFail);
                }
            };
            _shardBinding.OnSessionIdChanged += (_, newId) =>
            {
                SessionId = newId;
            };
            // Subscribe to message stream — process all messages routed to this user
            _shardBinding.UserMessages.Subscribe(async msg =>
            {
                try { await ProcessWebSocketMessageAsync(msg); }
                catch (Exception ex) { _logger.LogError(ex, "[UserSequencer] Error processing message for {UserId}", UserId); }
            });
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
                    _appAccessToken,
                    _conduitOrchestrator.ConduitId,
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
                _logger.LogErrorDetails("[EventSubClient] - [UserSequencer] Welcome message didn't come in time. Exception message: " + ex.Message, ex, DateTime.Now);
                await StateMachine.FireAsync(UserActions.WelcomeMessageFail);
            }
        }

        /// <summary>
        /// Runs the subscription manager
        /// </summary>
        protected override async Task RunManagerAsync()
        {
            try
            {
                _logger.LogDebug("[RunManagerAsync] Running manager for UserId: {UserId}", UserId);
                _managerTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ManagerCancelationSource = new CancellationTokenSource(RunGroupSubscribeTolerance);
                var checkOk = await _subscriptionManager.RunCheckAsync(
                    UserId,
                    RequestedSubscriptions,
                    ClientId,
                    _appAccessToken,
                    _conduitOrchestrator.ConduitId,
                    ManagerCancelationSource,
                    _logger
                    );
                await RunManagerNextActionAsync(checkOk);
            }
            catch (TaskCanceledException)
            {
                //NOOP
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
        /// Awaits a new session ID from the shard binding after a reconnect.
        /// </summary>
        protected override async Task ReconnectingEntryAsync()
        {
            if (_shardBinding == null) { await StateMachine.FireAsync(UserActions.ReconnectFail); return; }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<string>? handler = null;
            handler = (_, newSessionId) =>
            {
                _shardBinding.OnSessionIdChanged -= handler;  // always unsubscribe
                tcs.TrySetResult(newSessionId);
            };
            _shardBinding.OnSessionIdChanged += handler;

            using var cts = new CancellationTokenSource(ReconnectGraceTimeout);
            cts.Token.Register(() =>
            {
                _shardBinding.OnSessionIdChanged -= handler;  // unsubscribe on timeout too
                tcs.TrySetCanceled();
            });

            try
            {
                var newSession = await tcs.Task;
                SessionId = newSession;
                await StateMachine.FireAsync(UserActions.ReconnectSuccess);
            }
            catch
            {
                await StateMachine.FireAsync(UserActions.ReconnectFail);
            }
        }

        /// <summary>
        /// Awaits the shard (WebSocket) to become ready via IShardBinding.
        /// </summary>
        protected override async Task AwaitShardReadyAsync()
        {
            if (_shardBinding?.SessionId is { Length: > 0 })
            {
                SessionId = _shardBinding.SessionId;
                await StateMachine.FireAsync(UserActions.WebsocketSuccess);
            }
            else
            {
                _logger.LogWarning("[UserSequencer] AwaitShardReadyAsync: no binding or empty session for {UserId}", UserId);
                await StateMachine.FireAsync(UserActions.WebsocketFail);
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

        private Task ProcessWebSocketMessageAsync(WebSocketMessage message)
        {
            if (message?.Metadata == null) return Task.CompletedTask;
            if (_replayProtection.IsDuplicate(message.Metadata.MessageId) ||
                !_replayProtection.IsUpToDate(message.Metadata.MessageTimestamp))
            {
                _logger.LogDebug("[UserSequencer] Duplicate or outdated message: {MessageId}", message.Metadata.MessageId);
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
                _ => Task.CompletedTask
            };
        }

        /// <summary>
        /// Procedure for Initial testing of access token
        /// </summary>
        /// <returns></returns>
        protected override async Task InitialAccessTokenAsync()
        {
            try
            {
                _logger.LogDebug("[InitialAccessTokenAsync] Validating initial access token for UserId: {UserId}", UserId);
                using (CancellationTokenSource cts = new CancellationTokenSource(AccessTokenValidationTolerance))
                {
                    var validationResult = await _subscriptionManager.ApiTryValidateAsync(AccessToken, UserId, _logger, cts);
                    await InicialAccessTokenNextActionAsync(validationResult);
                }
            }
            catch (TaskCanceledException) 
            {
                //NOOP
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
            try
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
            catch(TaskCanceledException)
            {
                //NOOP
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
                _keepAliveMs = (message.Payload.Session.KeepAliveTimeoutSeconds.Value * 1000 + 100);
                _awaitMessage.Set();
                _watchdog.Start(_keepAliveMs);
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
                return;
            }

            _watchdog.Stop();

            // Reconnect message always has keepalive_timeout_seconds: null per spec.
            // Reuse the value negotiated during the Welcome message.
            _watchdog.Start(_keepAliveMs);

            // Session ID will be updated via IShardBinding.OnSessionIdChanged when shard completes reconnect
            // No socket operations needed here — ShardSequencer handles the WebSocket reconnect
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
        /// Ping response — protocol-level ping/pong is handled automatically by Websocket.Client.
        /// </summary>
        private Task PingMessageProcessingAsync()
        {
            _logger.LogDebug("[PingMessageProcessingAsync] Ping message detected");
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
            _logger.LogWarning("[UserSequencer] Watchdog fallback for {UserId} — no valid state for reconnect recovery", UserId);
            if (OnOutsideDisconnectAsync != null)
            {
                await OnOutsideDisconnectAsync.TryInvoke(this, e);
            }
            _watchdog.Stop();
            _watchdog.OnWatchdogTimeout -= OnWatchdogTimeoutAsync;
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
            await StateMachine.FireAsync(UserActions.Dispose);
        }

        protected override async Task FailProcedureAsync()
        {
            _logger.LogDebug("[FailProcedureAsync] Failing procedure for UserId: {UserId}", UserId);
            await StopManagerAsync();
            _watchdog.Stop();
            await StateMachine.FireAsync(UserActions.Dispose);
        }

        protected override async Task ReconnectingAfterWatchdogFailAsync()
        {
            _logger.LogWarning("[UserSequencer] Watchdog triggered for {UserId} — signalling shard lost", UserId);
            if (StateMachine.CanFire(UserActions.AccessTesting))
            {
                await StateMachine.FireAsync(UserActions.AccessTesting);
            }
        }

        protected override void UnhandeledState(UserState state, UserActions actions)
        {
            _logger.LogWarning($"State machine run into invalid state {state} while attempting to switch with action {actions}"
                + "Please report this error to the developer of library.");
        }
    }
}