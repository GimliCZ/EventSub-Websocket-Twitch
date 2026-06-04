namespace Twitch.EventSub.API;

/// <summary>
/// Per-conduit replica state and shard-slot bookkeeping. One instance per redundant conduit.
/// Pure in-memory math; the orchestrator performs the actual Twitch API writes.
/// </summary>
internal sealed class ConduitReplica
{
    public int Index { get; }
    public string ConduitId { get; set; }
    public int TwitchShardCount { get; set; }

    private readonly Dictionary<string, (string TwitchIndex, string SessionId)> _shardMap = new();

    /// <summary>
    /// Base offset for next slot reservation. Zero for freshly created conduits (slot 0 is the first free slot).
    /// Set to TwitchShardCount after reconciliation so new shards append beyond existing server-side slots.
    /// </summary>
    private int _nextSlotBase = 0;

    public ConduitReplica(int index, string conduitId)
    {
        Index = index;
        ConduitId = conduitId;
    }

    public IReadOnlyDictionary<string, (string TwitchIndex, string SessionId)> Shards => _shardMap;
    public int ShardCount => _shardMap.Count;

    public bool TryGetShard(string shardId, out (string TwitchIndex, string SessionId) entry) =>
        _shardMap.TryGetValue(shardId, out entry);

    public readonly record struct SlotReservation(string TwitchIndex, int NewShardCount, bool NeedsExpand);

    /// <summary>
    /// Marks that server-side shard slots [0..count-1] are already occupied by prior shards.
    /// Must be called after reconciliation so that new reservations start beyond these slots.
    /// </summary>
    public void SetReconciledSlotBase(int count)
    {
        _nextSlotBase = count;
    }

    /// <summary>Computes the next slot for a new shard; does not mutate the map.</summary>
    public SlotReservation ReserveShardSlot(string shardId)
    {
        // _nextSlotBase is 0 for freshly created conduits (slot 0 is unoccupied).
        // After reconciliation it equals TwitchShardCount so new shards append beyond existing server-side slots.
        int nextIndex = _nextSlotBase + _shardMap.Count;
        bool needsExpand = nextIndex >= TwitchShardCount;
        int newCount = needsExpand ? nextIndex + 1 : TwitchShardCount;
        return new SlotReservation(nextIndex.ToString(), newCount, needsExpand);
    }

    public void CommitShard(string shardId, string twitchIndex, string sessionId)
    {
        _shardMap[shardId] = (twitchIndex, sessionId);
    }

    /// <summary>
    /// Rebinds a Twitch slot (by its index) to a new internal shard id and session, used when a disabled
    /// slot is reactivated by a replacement shard. Any prior internal entry occupying that slot index is
    /// removed first so the slot is never double-mapped. Does NOT change <see cref="TwitchShardCount"/>.
    /// </summary>
    public void RebindSlot(string twitchIndex, string newInternalShardId, string sessionId)
    {
        var stale = _shardMap.Where(kv => kv.Value.TwitchIndex == twitchIndex).Select(kv => kv.Key).ToList();
        foreach (var key in stale)
            _shardMap.Remove(key);
        _shardMap[newInternalShardId] = (twitchIndex, sessionId);
    }

    public void UpdateShardSession(string shardId, string newSessionId)
    {
        if (_shardMap.TryGetValue(shardId, out var e))
            _shardMap[shardId] = (e.TwitchIndex, newSessionId);
    }

    public readonly record struct RemovalPlan(bool TargetIsLast, string FreedIndex, string? LastShardId, string? LastSessionId);

    /// <summary>Plans a clean scale-down removal (swap target with last slot). Does not mutate.</summary>
    public RemovalPlan PlanRemoval(string shardId)
    {
        if (!_shardMap.TryGetValue(shardId, out var target))
            return new RemovalPlan(true, "-1", null, null);

        string lastIndexStr = (TwitchShardCount - 1).ToString();
        var lastEntry = _shardMap.FirstOrDefault(kv => kv.Value.TwitchIndex == lastIndexStr);
        bool targetIsLast = target.TwitchIndex == lastIndexStr;
        return new RemovalPlan(targetIsLast, target.TwitchIndex, lastEntry.Key, lastEntry.Value.SessionId);
    }

    /// <summary>Applies the planned removal to the in-memory map and shard count.</summary>
    public void ApplyRemoval(string shardId, RemovalPlan plan)
    {
        if (!plan.TargetIsLast && plan.LastShardId != null)
            _shardMap[plan.LastShardId] = (plan.FreedIndex, plan.LastSessionId!);

        _shardMap.Remove(shardId);
        if (TwitchShardCount > 1) TwitchShardCount -= 1;
    }
}
