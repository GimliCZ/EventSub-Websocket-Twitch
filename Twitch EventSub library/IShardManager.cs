using Twitch.EventSub.CoreFunctions;

namespace Twitch.EventSub;

public interface IShardManager : IAsyncDisposable
{
    Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, CancellationToken ct);
    Task ReleaseUserFromShardAsync(string userId, CancellationToken ct);
    IReadOnlyList<(string ShardId, string? SessionId)> ActiveSessionIds { get; }
    event EventHandler<SessionIdUpdatedArgs> OnSessionIdUpdated;
}
