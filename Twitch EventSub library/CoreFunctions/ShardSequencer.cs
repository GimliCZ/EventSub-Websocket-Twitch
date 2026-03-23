using Microsoft.Extensions.Logging;
using Stateless;
using System.Reactive.Subjects;
using Twitch.EventSub.Messages;
using Twitch.EventSub.User;
using Websocket.Client;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Owns exactly one WebSocket connection (plus a pending one during reconnect).
/// Pure WebSocket lifecycle state machine — no knowledge of users, tokens, or subscriptions.
/// Uses Stateless for structured state transitions and OnTransitioned logging.
/// </summary>
public class ShardSequencer : IAsyncDisposable
{
    public enum ShardState { Disconnected, Connecting, WaitingForWelcome, Active, Reconnecting, Disposing, Disposed }
    public enum ShardTrigger { Connect, WelcomeReceived, ReconnectRequested, NewConnectionWelcome, ForceFresh, Terminate }

    private readonly string _shardId;
    private readonly ILogger _logger;
    private readonly StateMachine<ShardState, ShardTrigger> _machine;
    private readonly Subject<WebSocketMessage> _messages = new();
    private IDisposable? _activeMessageSub;
    private IDisposable? _activeDisconnectSub;
    private IDisposable? _pendingMessageSub;
    private IDisposable? _pendingDisconnectSub;
    private WebsocketClient? _activeClient;
    private WebsocketClient? _pendingClient;

    public string? SessionId { get; private set; }
    public string ShardId => _shardId;
    public ShardState State => _machine.State;
    public IObservable<WebSocketMessage> Messages => _messages;

    public event EventHandler<ShardCloseArgs>? OnClosed;
    public event EventHandler? OnForceFreshConnect;

    public ShardSequencer(string shardId, ILogger logger)
    {
        _shardId = shardId;
        _logger = logger;
        _machine = new StateMachine<ShardState, ShardTrigger>(ShardState.Disconnected);
        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        _machine.Configure(ShardState.Disconnected)
            .Permit(ShardTrigger.Connect, ShardState.WaitingForWelcome);

        _machine.Configure(ShardState.WaitingForWelcome)
            .Permit(ShardTrigger.WelcomeReceived, ShardState.Active)
            .Permit(ShardTrigger.Terminate, ShardState.Disposing);

        _machine.Configure(ShardState.Active)
            .Permit(ShardTrigger.ReconnectRequested, ShardState.Reconnecting)
            .Permit(ShardTrigger.ForceFresh, ShardState.Connecting)
            .Permit(ShardTrigger.Terminate, ShardState.Disposing);

        _machine.Configure(ShardState.Connecting)
            .Permit(ShardTrigger.WelcomeReceived, ShardState.Active)
            .Permit(ShardTrigger.Terminate, ShardState.Disposing);

        _machine.Configure(ShardState.Reconnecting)
            .Permit(ShardTrigger.NewConnectionWelcome, ShardState.Active)
            .Permit(ShardTrigger.ForceFresh, ShardState.Connecting)
            .Permit(ShardTrigger.Terminate, ShardState.Disposing);

        _machine.Configure(ShardState.Disposing)
            .Permit(ShardTrigger.Terminate, ShardState.Disposed);

        _machine.OnTransitioned(t => _logger.LogInformation(
            "Shard {ShardId}: {Source} → {Dest} trigger={Trigger}",
            _shardId, t.Source, t.Destination, t.Trigger));
    }

    public async Task ConnectAsync(Uri uri, CancellationToken ct)
    {
        _activeClient = CreateClient(uri);
        SubscribeToClient(_activeClient, isPending: false);
        await _activeClient.Start();
        await _machine.FireAsync(ShardTrigger.Connect);
    }

    public async Task HandleWelcomeAsync(string sessionId)
    {
        SessionId = sessionId;
        await _machine.FireAsync(ShardTrigger.WelcomeReceived);
        _logger.LogInformation("Shard {ShardId} Welcome session={SessionId}", _shardId, sessionId);
    }

    public async Task HandleReconnectAsync(Uri reconnectUrl, CancellationToken ct)
    {
        await _machine.FireAsync(ShardTrigger.ReconnectRequested);
        _pendingClient = CreateClient(reconnectUrl);
        SubscribeToClient(_pendingClient, isPending: true);
        await _pendingClient.Start();
        // Wait for Welcome on new connection via HandleNewConnectionWelcomeAsync
    }

    public async Task HandleNewConnectionWelcomeAsync(string newSessionId)
    {
        var oldClient = _activeClient;
        var oldMsgSub = _activeMessageSub;
        var oldDiscSub = _activeDisconnectSub;

        _activeClient = _pendingClient;
        _activeMessageSub = _pendingMessageSub;
        _activeDisconnectSub = _pendingDisconnectSub;
        _pendingClient = null;
        _pendingMessageSub = null;
        _pendingDisconnectSub = null;

        SessionId = newSessionId;
        await _machine.FireAsync(ShardTrigger.NewConnectionWelcome);
        _logger.LogInformation("Shard {ShardId} reconnect complete newSession={SessionId}", _shardId, newSessionId);

        // Dispose old client ONLY after new Welcome received (spec-compliant)
        oldMsgSub?.Dispose();
        oldDiscSub?.Dispose();
        if (oldClient != null)
        {
            await oldClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Replaced by new connection");
            oldClient.Dispose();
        }
    }

    public async Task HandleCloseCodeAsync(int code)
    {
        _logger.LogWarning("Shard {ShardId} close code {Code}", _shardId, code);
        switch (code)
        {
            case 4001:
                _logger.LogCritical("Shard {ShardId} close code 4001 (client protocol violation) — NOT reconnecting. This is a library bug.", _shardId);
                OnClosed?.Invoke(this, new ShardCloseArgs { ShardId = _shardId, CloseCode = code, Reason = "ClientProtocolViolation" });
                await _machine.FireAsync(ShardTrigger.Terminate);
                break;
            case 4004:
                _logger.LogWarning("Shard {ShardId} close code 4004 (reconnect grace expired) — forcing fresh connect", _shardId);
                OnForceFreshConnect?.Invoke(this, EventArgs.Empty);
                await _machine.FireAsync(ShardTrigger.ForceFresh);
                break;
            default:
                // 4000, 4002, 4003, 4005, 4006, 4007 — all reconnectable
                await _machine.FireAsync(ShardTrigger.ReconnectRequested);
                OnClosed?.Invoke(this, new ShardCloseArgs { ShardId = _shardId, CloseCode = code });
                break;
        }
    }

    private WebsocketClient CreateClient(Uri uri)
    {
        return new WebsocketClient(uri) { IsReconnectionEnabled = false };
    }

    private void SubscribeToClient(WebsocketClient client, bool isPending)
    {
        var msgSub = client.MessageReceived
            .Subscribe(async msg =>
            {
                if (msg.MessageType == System.Net.WebSockets.WebSocketMessageType.Text && msg.Text != null)
                {
                    try
                    {
                        // Deserialize and publish to the messages Subject
                        var parsed = await MessageProcessing.DeserializeMessageAsync(msg.Text);
                        if (parsed == null) return;

                        _logger.LogDebug("Shard {ShardId} message type={Type} isPending={IsPending}",
                            _shardId, parsed.Metadata?.MessageType, isPending);

                        // Active connection messages go to subscribers; pending only for reconnect Welcome
                        if (!isPending)
                        {
                            _messages.OnNext(parsed);
                        }
                        // isPending messages are handled by the reconnect flow via HandleNewConnectionWelcomeAsync
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Shard {ShardId} failed to deserialize message", _shardId);
                    }
                }
            });

        var discSub = client.DisconnectionHappened
            .Subscribe(info =>
            {
                if (!isPending)
                {
                    _logger.LogWarning("Shard {ShardId} disconnected: {Type}", _shardId, info.Type);
                }
            });

        if (isPending)
        {
            _pendingMessageSub = msgSub;
            _pendingDisconnectSub = discSub;
        }
        else
        {
            _activeMessageSub?.Dispose();
            _activeDisconnectSub?.Dispose();
            _activeMessageSub = msgSub;
            _activeDisconnectSub = discSub;
        }
    }

    // Test helpers — bypass network, advance state machine directly
    internal async Task SimulateActiveForTestAsync()
    {
        await _machine.FireAsync(ShardTrigger.Connect);          // Disconnected → WaitingForWelcome
        SessionId = "test-session";
        await _machine.FireAsync(ShardTrigger.WelcomeReceived);  // WaitingForWelcome → Active
    }

    internal async Task SimulateConnectingForTestAsync()
    {
        await _machine.FireAsync(ShardTrigger.Connect);          // Disconnected → WaitingForWelcome
    }

    internal async Task SimulateReconnectingForTestAsync()
    {
        await _machine.FireAsync(ShardTrigger.ReconnectRequested);  // Active → Reconnecting
    }

    public async ValueTask DisposeAsync()
    {
        if (_machine.State == ShardState.Disposed) return;
        _activeMessageSub?.Dispose();
        _activeDisconnectSub?.Dispose();
        _pendingMessageSub?.Dispose();
        _pendingDisconnectSub?.Dispose();
        _messages.OnCompleted();
        if (_activeClient != null)
        {
            await _activeClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Disposed");
            _activeClient.Dispose();
        }
        if (_pendingClient != null)
        {
            await _pendingClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Disposed");
            _pendingClient.Dispose();
        }
        if (_machine.CanFire(ShardTrigger.Terminate))
        {
            await _machine.FireAsync(ShardTrigger.Terminate);
        }
    }
}
