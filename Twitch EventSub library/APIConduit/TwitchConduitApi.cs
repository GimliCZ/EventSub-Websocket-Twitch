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
    public class TwitchApiConduit : ITwitchConduitApi
    {
        private const string ConduitUrl = "https://api.twitch.tv/helix/eventsub/conduits";
        private const string ConduitShardsUrl = "https://api.twitch.tv/helix/eventsub/conduits/shards";

        private readonly IHttpClientFactory _httpClientFactory;

        public TwitchApiConduit(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ConduitCreateResponse> ConduitCreatorAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, int inicialSize = 1)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.TwitchApiConduit);
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

        public async Task<ConduitUpdateResponse> ConduitUpdateAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId, int size)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.TwitchApiConduit);
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
        public async Task<bool> ConduitDeleteAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.TwitchApiConduit);
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
        public async Task<ConduitGetShardsResponse> ConduitGetShardsAsync(string accessToken, string clientId, CancellationTokenSource clSource, ILogger logger, string conduitId, SubscriptionStatusTypes status = SubscriptionStatusTypes.Empty, string after = null)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.TwitchApiConduit);
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
        public async Task<List<ConduitShard>> GetAllConduitGetShardsAsync(string clientId, string accessToken, string conduitId, CancellationTokenSource clSource, ILogger logger, SubscriptionStatusTypes statusSelector = SubscriptionStatusTypes.Enabled)
        {
            var allSubscriptions = new List<ConduitShard>();
            string? afterCursor = null;
            const int totalPossibleIterations = EventSubClientOptions.TwitchMaxShardsPerConduit;

            for (int i = 0; i < totalPossibleIterations; i++)
            {
                var response = await ConduitGetShardsAsync(accessToken, clientId, clSource, logger, conduitId, statusSelector, afterCursor);
                if (response != null)
                {
                    allSubscriptions.AddRange(response.Data);
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

        // --- ITwitchConduitApi implementation ---

        public async Task<List<string>> GetConduitIdsAsync(string appAccessToken, string clientId, CancellationToken ct)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.TwitchApiConduit);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appAccessToken);
            httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);

            try
            {
                var response = await httpClient.GetAsync(ConduitUrl, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        var parsed = JsonConvert.DeserializeObject<ConduitCreateResponse>(body);
                        return parsed?.Data?.Select(d => d.Id).ToList() ?? new List<string>();
                    case HttpStatusCode.Unauthorized:
                        throw new InvalidAccessTokenException("GetConduitIds failed: " + response.ReasonPhrase);
                    default:
                        return new List<string>();
                }
            }
            catch (HttpRequestException)
            {
                return new List<string>();
            }
        }

        public async Task<string> CreateConduitAsync(string appAccessToken, string clientId, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var response = await ConduitCreatorAsync(appAccessToken, clientId, cts, NullLogger.Instance, inicialSize: 1);
            return response?.Data?.FirstOrDefault()?.Id
                ?? throw new InvalidOperationException("ConduitCreatorAsync returned null conduit");
        }

        public async Task UpdateConduitShardCountAsync(string conduitId, int shardCount, string appAccessToken, string clientId, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await ConduitUpdateAsync(appAccessToken, clientId, cts, NullLogger.Instance, conduitId, shardCount);
        }

        public async Task UpdateConduitShardSessionAsync(string conduitId, string twitchShardIndex, string sessionId, string appAccessToken, string clientId, CancellationToken ct)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.TwitchApiConduit);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appAccessToken);
            httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);

            var request = new ConduitUpdateShardRequest
            {
                ConduitId = conduitId,
                Shards = new List<ShardUpdateItem>
                {
                    new ShardUpdateItem
                    {
                        Id = twitchShardIndex,
                        Transport = new Transport
                        {
                            Method = nameof(TransportMethod.websocket),
                            SessionId = sessionId
                        }
                    }
                }
            };

            HttpContent content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PatchAsync(ConduitShardsUrl, content, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Accepted:
                    case HttpStatusCode.OK:
                        return;
                    case HttpStatusCode.Unauthorized:
                        throw new InvalidAccessTokenException("UpdateConduitShardSession failed: " + response.ReasonPhrase);
                    default:
                        throw new InvalidOperationException($"UpdateConduitShardSession got unexpected status {response.StatusCode}: {body}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("UpdateConduitShardSession HTTP request failed", ex);
            }
        }

        public async Task DeleteConduitAsync(string conduitId, string appAccessToken, string clientId, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await ConduitDeleteAsync(appAccessToken, clientId, cts, NullLogger.Instance, conduitId);
        }
    }
}
