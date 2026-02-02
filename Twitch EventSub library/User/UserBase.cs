using Twitch.EventSub.API.Models;
using Websocket.Client;

namespace Twitch.EventSub.User
{
    public abstract class UserBase
    {
        public enum UserActions
        {
            AccessTesting,
            AccessFailed,
            AccessSuccess,
            WebsocketFail,
            WebsocketSuccess,
            WelcomeMessageSuccess,
            WelcomeMessageFail,
            HandShakeFail,
            HandShakeAccessFail,
            HandShakeSuccess,
            RunningAwait,
            RunningProceed,
            RunningAccessFail,
            ReconnectFromWatchdog,
            ReconnectRequested,
            NewTokenProvidedReturnToInitialTest,
            NewTokenProvidedReturnToHandShake,
            NewTokenProvidedReturnToRunning,
            AwaitNewTokenFailed,
            ReconnectSuccess,
            ReconnectFail,
            Stop,
            Fail,
            Dispose
        }

        public enum UserState
        {
            //Initial condition is registred, since we add to list
            Registred,

            InitialAccessTest,
            Websocket,
            WellcomeMessage,
            HandShake,
            Running,
            Awaiting,
            Reconnecting,
            ReconnectingFromWatchdog,
            AwaitNewTokenAfterFailedTest,
            AwaitNewTokenAfterFailedHandShake,
            AwaitNewTokenAfterFailedRun,
            Stoping,
            Failing,
            Disposed
        }

        public const string DefaultWebSocketUrl = "wss://eventsub.wss.twitch.tv/ws";

        public List<CreateSubscriptionRequest> RequestedSubscriptions;

        public UserBase(string id, string access, List<CreateSubscriptionRequest> requestedSubscriptions, string url = null)
        {
            State = UserState.Registred;
            Url = new Uri(url ?? DefaultWebSocketUrl);
            Socket = new WebsocketClient(Url);
            Socket.IsReconnectionEnabled = false;
            UserId = id;
            AccessToken = access;
            StateMachine = new Stateless.StateMachine<UserState, UserActions>(() => State, s => State = s);
            StateMachineCofiguration(StateMachine);
            ManagerCancelationSource = new CancellationTokenSource();
            RequestedSubscriptions = requestedSubscriptions;
            //string graph = UmlDotGraph.Format(StateMachine.GetInfo());
            //Console.WriteLine(graph);
        }

        public Uri Url { get; set; }
        public UserState State { get; set; }
        public WebsocketClient Socket { get; set; }
        public string UserId { get; protected set; }
        public string SessionId { get; set; }
        public string Conduit { get; set; }
        public string AccessToken { get; protected set; }
        public Stateless.StateMachine<UserState, UserActions> StateMachine { get; set; }
        public CancellationTokenSource ManagerCancelationSource { get; set; }

        //TODO: Make global
        public string ClientId { get; set; }

        public RefreshRequestArgs? LastAccessViolationArgs { get; set; }

        internal event EventHandler<string?> OnDispose;

        public bool IsDisposed()
        {
            return StateMachine.State == UserState.Disposed;
        }

        public async Task StartAsync()
        {
            await StateMachine.ActivateAsync();

            if (StateMachine.State == UserState.Registred)
            {
                await StateMachine.FireAsync(UserActions.AccessTesting);
            }
        }

        public bool Update(string access, List<CreateSubscriptionRequest> requestedSubscriptions)
        {
            if (State == UserState.Awaiting ||
                State == UserState.AwaitNewTokenAfterFailedTest ||
                State == UserState.AwaitNewTokenAfterFailedHandShake ||
                State == UserState.AwaitNewTokenAfterFailedRun)
            {
                AccessToken = access;
                RequestedSubscriptions = requestedSubscriptions;
                return true;
            }
            return false;
        }

        public async Task<bool> StopAsync()
        {
            if (StateMachine.State == UserState.Disposed)
            {
                return true;
            }
            if (StateMachine.CanFire(UserActions.Stop))
            {
                await StateMachine.FireAsync(UserActions.Stop);
                return true;
            }
            return false;
        }

        private void StateMachineCofiguration(Stateless.StateMachine<UserState, UserActions> machine)
        {
            //If User is pressent within list, Try to test his access
            machine.Configure(UserState.Registred)
                .Permit(UserActions.AccessTesting, UserState.InitialAccessTest);
            //If result of testing is good, try to establish connection, else, request new token
            machine.Configure(UserState.InitialAccessTest)
                .OnEntryAsync(InitialAccessTokenAsync)
                .Permit(UserActions.AccessSuccess, UserState.Websocket)
                .Permit(UserActions.AccessFailed, UserState.AwaitNewTokenAfterFailedTest);
            //Try to establish connection, if succeeds, try to complete handshake, else, fail
            machine.Configure(UserState.Websocket)
                .OnEntryAsync(RunWebsocketAsync)
                .Permit(UserActions.WebsocketSuccess, UserState.WellcomeMessage)
                .Permit(UserActions.WebsocketFail, UserState.Failing);
            machine.Configure(UserState.WellcomeMessage).
                OnEntryAsync(AwaitWelcomeMessageAsync)
                .Permit(UserActions.WelcomeMessageSuccess, UserState.HandShake)
                .Permit(UserActions.WelcomeMessageFail, UserState.Failing);
            //If handShake Succeeds, then run, else if we get notification, we lost access, try to reestablish connection
            //Else, fail over
            machine.Configure(UserState.HandShake)
                .OnEntryAsync(RunHandshakeAsync)
                .Permit(UserActions.HandShakeSuccess, UserState.Running)
                .Permit(UserActions.HandShakeAccessFail, UserState.AwaitNewTokenAfterFailedHandShake)
                .Permit(UserActions.HandShakeFail, UserState.Failing)
                .Permit(UserActions.WebsocketFail, UserState.Failing);
            //If running, enable token reestablishment, stop and failover
            machine.Configure(UserState.Running)
                .PermitReentry(UserActions.RunningProceed)
                .OnEntryAsync(RunManagerAsync)
                .Permit(UserActions.RunningAwait, UserState.Awaiting)
                .Permit(UserActions.RunningAccessFail, UserState.AwaitNewTokenAfterFailedRun)
                .Permit(UserActions.ReconnectRequested, UserState.Reconnecting)
                .Permit(UserActions.Stop, UserState.Stoping)
                .Permit(UserActions.Fail, UserState.Failing)
                .Permit(UserActions.HandShakeFail, UserState.Failing)
                .Permit(UserActions.WebsocketFail, UserState.Failing)
                .Permit(UserActions.ReconnectFromWatchdog, UserState.ReconnectingFromWatchdog);
            machine.Configure(UserState.Awaiting)
                .OnEntryAsync(AwaitManagerAsync)
                .Permit(UserActions.RunningProceed, UserState.Running)
                .Permit(UserActions.RunningAccessFail, UserState.AwaitNewTokenAfterFailedRun)
                .Permit(UserActions.ReconnectRequested, UserState.Reconnecting)
                .Permit(UserActions.Stop, UserState.Stoping)
                .Permit(UserActions.Fail, UserState.Failing)
                .Permit(UserActions.HandShakeFail, UserState.Failing)
                .Permit(UserActions.WebsocketFail, UserState.Failing)
                .Permit(UserActions.ReconnectFromWatchdog, UserState.ReconnectingFromWatchdog);
            //If NewAcessToken Requested, Start timer and
            machine.Configure(UserState.AwaitNewTokenAfterFailedTest)
                .OnEntryAsync(NewAccessTokenRequestAsync)
                .Permit(UserActions.NewTokenProvidedReturnToInitialTest, UserState.InitialAccessTest)
                .Permit(UserActions.AwaitNewTokenFailed, UserState.Stoping)
                .Permit(UserActions.AccessFailed, UserState.Stoping)
                .Permit(UserActions.Stop, UserState.Stoping)
                .Permit(UserActions.Fail, UserState.Failing)
                .Permit(UserActions.HandShakeFail, UserState.Failing)
                .Permit(UserActions.WebsocketFail, UserState.Failing);
            machine.Configure(UserState.AwaitNewTokenAfterFailedHandShake)
                .OnEntryAsync(NewAccessTokenRequestAsync)
                .Permit(UserActions.NewTokenProvidedReturnToHandShake, UserState.HandShake)
                .Permit(UserActions.AwaitNewTokenFailed, UserState.Stoping)
                .Permit(UserActions.Stop, UserState.Stoping)
                .Permit(UserActions.Fail, UserState.Failing)
                .Permit(UserActions.HandShakeFail, UserState.Failing)
                .Permit(UserActions.WebsocketFail, UserState.Failing);
            machine.Configure(UserState.AwaitNewTokenAfterFailedRun)
                .OnEntryAsync(NewAccessTokenRequestAsync)
                .Permit(UserActions.NewTokenProvidedReturnToRunning, UserState.Running)
                .Permit(UserActions.AwaitNewTokenFailed, UserState.Stoping)
                .Permit(UserActions.Stop, UserState.Stoping)
                .Permit(UserActions.Fail, UserState.Failing)
                .Permit(UserActions.HandShakeFail, UserState.Failing)
                .Permit(UserActions.WebsocketFail, UserState.Failing);
            machine.Configure(UserState.Reconnecting)
                .Permit(UserActions.ReconnectSuccess, UserState.Running)
                .Permit(UserActions.ReconnectFail, UserState.Failing)
                .Permit(UserActions.Stop, UserState.Stoping)
                .Permit(UserActions.Fail, UserState.Failing)
                .Permit(UserActions.HandShakeFail, UserState.Failing)
                .Permit(UserActions.WebsocketFail, UserState.Failing);
            machine.Configure(UserState.ReconnectingFromWatchdog)
                .OnEntryAsync(ReconnectingAfterWatchdogFailAsync)
                .Permit(UserActions.AccessTesting, UserState.InitialAccessTest);
            machine.Configure(UserState.Stoping)
                .OnEntryAsync(StopProcedureAsync)
                .Permit(UserActions.Dispose, UserState.Disposed);
            machine.Configure(UserState.Failing)
                .OnEntryAsync(FailProcedureAsync)
                .Permit(UserActions.Dispose, UserState.Disposed)
                .Ignore(UserActions.RunningAccessFail)
                .Ignore(UserActions.Fail);
            machine.Configure(UserState.Disposed)
                .OnEntryAsync(DisposeProcedureAsync)
                .Ignore(UserActions.RunningProceed);
            machine.OnUnhandledTrigger(UnhandeledState);
        }

        protected abstract void UnhandeledState(UserState state, UserActions actions);

        protected abstract Task ReconnectingAfterWatchdogFailAsync();

        protected abstract Task FailProcedureAsync();

        protected abstract Task StopProcedureAsync();

        private async Task DisposeProcedureAsync()
        {
            Socket.Dispose();
            await ManagerCancelationSource.CancelAsync();
            await StateMachine.DeactivateAsync();
            OnDispose?.Invoke(this, UserId);
        }

        protected abstract Task AwaitManagerAsync();

        protected abstract Task AwaitWelcomeMessageAsync();

        protected abstract Task InitialAccessTokenAsync();

        protected abstract Task NewAccessTokenRequestAsync();

        protected abstract Task RunManagerAsync();

        protected abstract Task RunHandshakeAsync();

        protected abstract Task RunWebsocketAsync();
    }
}