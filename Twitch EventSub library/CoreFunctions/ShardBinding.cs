namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Internal implementation of IShardBinding created by ShardManager per user.
/// Lifecycle-only: exposes the shard's stream and session changes. Message routing is
/// performed by the MessagePipeline, not here.
/// </summary>
internal class ShardBinding : IShardBinding
{
    private readonly ShardSequencer _sequencer;
    private readonly string _userId;
    private readonly ShardManager _manager;
    // Named handler fields for unsubscription
    private readonly EventHandler<ShardCloseArgs> _closedHandler;
    private readonly EventHandler<SessionIdUpdatedArgs> _sessionHandler;

    public string ShardId => _sequencer.ShardId;
    public string SessionId => _sequencer.SessionId ?? string.Empty;

    public IObservable<ShardInbound> ShardStream => _sequencer.Messages;
    public int? NegotiatedKeepaliveSeconds => _sequencer.NegotiatedKeepaliveSeconds;

    public event EventHandler? OnShardLost;
    public event EventHandler<string>? OnSessionIdChanged;

    public ShardBinding(ShardSequencer sequencer, string userId, ShardManager manager)
    {
        _sequencer = sequencer;
        _userId = userId;
        _manager = manager;

        _closedHandler = (_, _) => OnShardLost?.Invoke(this, EventArgs.Empty);
        _sessionHandler = (_, args) =>
        {
            if (args.ShardId == _sequencer.ShardId && args.NewSessionId != null)
                OnSessionIdChanged?.Invoke(this, args.NewSessionId);
        };

        _sequencer.OnClosed += _closedHandler;
        _manager.OnSessionIdUpdated += _sessionHandler;
    }

    public void Dispose()
    {
        _sequencer.OnClosed -= _closedHandler;
        _manager.OnSessionIdUpdated -= _sessionHandler;
    }
}
