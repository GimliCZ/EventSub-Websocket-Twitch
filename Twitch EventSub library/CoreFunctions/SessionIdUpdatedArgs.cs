namespace Twitch.EventSub.CoreFunctions;

public class SessionIdUpdatedArgs : EventArgs
{
    public string ShardId { get; init; } = string.Empty;
    public string? OldSessionId { get; init; }
    /// <summary>Null means shard was removed.</summary>
    public string? NewSessionId { get; init; }
}
