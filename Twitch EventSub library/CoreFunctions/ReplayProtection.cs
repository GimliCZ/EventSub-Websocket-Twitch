using System.Collections.Concurrent;
using System.Globalization;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Thread-safe replay protection. Shared singleton across all shards.
/// Tracks the last N message IDs atomically to detect duplicates from
/// parallel shard delivery. Timestamp validation follows Twitch spec: reject
/// messages older than 10 minutes.
/// </summary>
public class ReplayProtection
{
    private static readonly string _format = "MM/dd/yyyy HH:mm:ss";
    private readonly int _maxSize;
    // Key = messageId, Value = insertion-order counter for eviction
    private readonly ConcurrentDictionary<string, long> _seen = new();
    private long _counter;
    private readonly object _evictionLock = new();

    public ReplayProtection(int messagesToRemember)
    {
        _maxSize = messagesToRemember;
    }

    /// <summary>
    /// Returns true if this message ID has been seen before (duplicate).
    /// Thread-safe: concurrent calls with the same ID return true for all but the first.
    /// </summary>
    public bool IsDuplicate(string messageId)
    {
        // Evict oldest entries BEFORE adding so that a previously-evicted ID
        // is no longer in the dictionary when TryAdd is called.
        if (_seen.Count >= _maxSize)
        {
            lock (_evictionLock)
            {
                while (_seen.Count >= _maxSize)
                {
                    var oldest = _seen.OrderBy(kv => kv.Value).First();
                    _seen.TryRemove(oldest.Key, out _);
                }
            }
        }

        // TryAdd returns true only for the first caller with this key — atomic.
        long order = Interlocked.Increment(ref _counter);
        if (!_seen.TryAdd(messageId, order))
            return true;

        return false;
    }

    /// <summary>Returns true if the timestamp is within the last 10 minutes (Twitch spec).</summary>
    public bool IsUpToDate(string timestamp)
    {
        var messageTime = ParseDateTimeString(timestamp);
        return (DateTime.UtcNow - messageTime) < TimeSpan.FromMinutes(10);
    }

    public static DateTime ParseDateTimeString(string timestamp)
    {
        if (DateTime.TryParse(timestamp, null, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToUniversalTime();
        if (DateTime.TryParseExact(timestamp, _format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
            return dt2.ToUniversalTime();
        throw new Exception("[EventSubClient] - [ReplayProtection] Parsed Invalid date");
    }
}
