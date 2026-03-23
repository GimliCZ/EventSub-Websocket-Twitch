namespace Twitch.EventSub.CoreFunctions;

public class ShardCloseArgs : EventArgs
{
    public string ShardId { get; init; } = string.Empty;
    public int? CloseCode { get; init; }
    public string? Reason { get; init; }
}
