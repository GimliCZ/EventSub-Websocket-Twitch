using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.API.Providers;
using Twitch.EventSub.APIConduit.Models.Requests;
using Twitch.EventSub.APIConduit.Models.Responses;
using Twitch.EventSub.APIConduit.Models.Shared;

namespace Twitch.EventSub.APIConduit
{
    public static class TwitchApiConduit
    {
        private const string ConduitUrl = "https://api.twitch.tv/helix/eventsub/conduits";
        private const string ConduitShardsUrl = "https://api.twitch.tv/helix/eventsub/conduits/shards";
        public static async Task<ConduitCreateResponse> ConduitCreatorAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, int inicialSize = 1)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                var conduitCreate = new ConduitCreateRequest { ShardCount = inicialSize };
                HttpContent content = new StringContent(JsonConvert.SerializeObject(conduitCreate), Encoding.UTF8, "application/json");

                try
                {
                    var queryBuilder = new StringBuilder(ConduitUrl);
                    var response = await httpClient.PostAsync(ConduitUrl, content, clSource.Token);
                    var body = await response.Content.ReadAsStringAsync(clSource.Token);
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        body = string.Empty;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK: return JsonConvert.DeserializeObject<ConduitCreateResponse>(body);
                        case HttpStatusCode.Unauthorized: throw new InvalidAccessTokenException("ConduitCreator failed due" + body + response.ReasonPhrase);
                        case HttpStatusCode.TooManyRequests: throw new InvalidOperationException("Conduit returned limit reached response, this is critical fault and should not happen.");
                        default:
                            logger.LogWarning("[EventSubClient] - [TwitchApiConduit] - ConduitCreator got non-standard status code", queryBuilder, response);
                            return new ConduitCreateResponse();
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError($"[EventSubClient] - [TwitchApiConduit] - ConduitCreator returned exception", ex, conduitCreate);
                    return new ConduitCreateResponse();
                }
            }
        }

        public static async Task<ConduitUpdateResponse> ConduitUpdateAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId, int size)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                var conduitCreate = new ConduitUpdateRequest { Id = conduitId, ShardCount = size };
                HttpContent content = new StringContent(JsonConvert.SerializeObject(conduitCreate), Encoding.UTF8, "application/json");

                try
                {
                    var response = await httpClient.PatchAsync(ConduitUrl, content, clSource.Token);
                    var body = await response.Content.ReadAsStringAsync(clSource.Token);
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        body = string.Empty;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK: return JsonConvert.DeserializeObject<ConduitUpdateResponse>(body) ?? new ConduitUpdateResponse();
                        case HttpStatusCode.Unauthorized: throw new InvalidAccessTokenException("ConduitUpdate failed due" + body + response.ReasonPhrase);
                        default:
                            logger.LogWarning("[EventSubClient] - [TwitchApiConduit] - ConduitUpdate got non-standard status code", response);
                            return new ConduitUpdateResponse();
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError($"[EventSubClient] - [TwitchApiConduit] - ConduitUpdate returned exception", ex, conduitCreate);
                    return new ConduitUpdateResponse();
                }
            }
        }
        public static async Task<bool> ConduitDeleteAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                var url = $"{ConduitUrl}?id={conduitId}";
                try
                {

                    var response = await httpClient.DeleteAsync(url, clSource.Token);
                    var body = await response.Content.ReadAsStringAsync(clSource.Token);
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        body = string.Empty;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK: return true;
                        case HttpStatusCode.Unauthorized: throw new InvalidAccessTokenException("ConduitDelete failed due" + body + response.ReasonPhrase);
                        default:
                            logger.LogWarning("[EventSubClient] - [TwitchApiConduit] - ConduitDelete got non-standard status code", response);
                            return default;
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError($"[EventSubClient] - [TwitchApiConduit] - ConduitDelete returned exception", ex, url);
                    return default;
                }
            }
        }
        public static async Task<ConduitGetShardsResponse> ConduitGetShardsAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId, SubscriptionStatusTypes status = SubscriptionStatusTypes.Empty, string after = null)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                var url = $"{ConduitShardsUrl}?conduit_id={conduitId}";
                if (status != SubscriptionStatusTypes.Empty)
                {
                    url += $"?status={StatusProvider.GetStatusString(status)}";
                }
                if (after != null)
                {
                    url += $"?after={after}";
                }
                try
                {

                    var response = await httpClient.GetAsync(url, clSource.Token);
                    var body = await response.Content.ReadAsStringAsync(clSource.Token);
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        body = string.Empty;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK: return JsonConvert.DeserializeObject<ConduitGetShardsResponse>(body) ?? new ConduitGetShardsResponse();
                        case HttpStatusCode.Unauthorized: throw new InvalidAccessTokenException("ConduitDelete failed due" + body + response.ReasonPhrase);
                        default:
                            logger.LogWarning("[EventSubClient] - [TwitchApiConduit] - ConduitDelete got non-standard status code", response);
                            return default;
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError($"[EventSubClient] - [TwitchApiConduit] - ConduitDelete returned exception", ex, url);
                    return default;
                }
            }
        }
        public static async Task<List<ConduitShard>> GetAllConduitGetShardsAsync(string clientId, string accessToken, string conduitId, CancellationTokenSource clSource, ILogger logger, SubscriptionStatusTypes statusSelector = SubscriptionStatusTypes.Enabled)
        {
            var allSubscriptions = new List<ConduitShard>();
            string? afterCursor = null;
            const int totalPossibleIterations = 20000;

            for (int i = 0; i < totalPossibleIterations; i++)
            {
                var response = await ConduitGetShardsAsync(accessToken, clientId, clSource, logger, conduitId, statusSelector, afterCursor);
                if (response != null)
                {
                    allSubscriptions.Concat(response.Data);
                    if (string.IsNullOrWhiteSpace(response.Pagination.Cursor))
                    {
                        break;
                    }
                    afterCursor = response.Pagination.Cursor;
                }
                else
                {
                    logger.LogInformation("[EventSubClient] - [TwitchApi] Response returned null cause of invalid userId or filter parameter");
                    break;
                }
            }
            if (allSubscriptions.Count == 0)
            {
                logger.LogInformation("[EventSubClient] - [TwitchApi] List of subscriptions returned EMPTY!");
            }

            return allSubscriptions;
        }
    }
}
