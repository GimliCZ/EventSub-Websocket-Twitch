namespace Twitch.EventSub.CoreFunctions;

/// <summary>Internal: associates a ShardSequencer with its assigned user IDs.</summary>
internal class ShardContext
{
    public ShardSequencer Sequencer { get; }
    public HashSet<string> UserIds { get; } = new();

    /// <summary>Conduit replica this shard belongs to.</summary>
    public int ReplicaIndex { get; init; }

    /// <summary>Last session id this shard reported, used to compute old→new transitions for the conduit.</summary>
    public string? LastSession { get; set; }

    /// <summary>Disposable returned by MessagePipeline.Attach for this shard's stream.</summary>
    public IDisposable? PipelineAttachment { get; set; }

    /// <summary>
    /// True when this shard was opened to reactivate a disabled conduit slot. Recovery shards must NOT
    /// trigger a conduit Add on their first welcome (the orchestrator PATCHes the existing slot instead).
    /// Subsequent reconnects of a recovery shard forward as an Update of that slot.
    /// </summary>
    public bool IsRecovery { get; init; }

    public ShardContext(ShardSequencer sequencer)
    {
        Sequencer = sequencer;
    }
}
