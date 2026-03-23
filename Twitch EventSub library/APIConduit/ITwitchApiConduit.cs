using Microsoft.Extensions.Logging;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.APIConduit.Models.Responses;
using Twitch.EventSub.APIConduit.Models.Shared;

namespace Twitch.EventSub.APIConduit
{
    public interface ITwitchApiConduit
    {
        Task<ConduitCreateResponse> ConduitCreatorAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, int inicialSize = 1);
        Task<ConduitUpdateResponse> ConduitUpdateAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId, int size);
        Task<bool> ConduitDeleteAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId);
        Task<ConduitGetShardsResponse> ConduitGetShardsAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId, SubscriptionStatusTypes status = SubscriptionStatusTypes.Empty, string after = null);
        Task<List<ConduitShard>> GetAllConduitGetShardsAsync(string clientId, string accessToken, string conduitId, CancellationTokenSource clSource, ILogger logger, SubscriptionStatusTypes statusSelector = SubscriptionStatusTypes.Enabled);
    }
}
