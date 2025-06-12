using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.API.Providers;
using Twitch.EventSub.CoreFunctions;

namespace Twitch.EventSub.API
{
    public static class TwitchApi
    {
        private const string BaseUrl = "https://api.twitch.tv/helix/eventsub/subscriptions";

        private const string ValidateUrl = "https://id.twitch.tv/oauth2/validate";

        /// <summary>
        /// Function sends filled CreateSubscriptionRequest to twitch for processing
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <param name="request"></param>
        /// <param name="clSource"></param>
        /// <param name="logger"></param>
        /// <returns>True on success, false on failure</returns>
        /// <exception cref="InvalidAccessTokenException"></exception>
        /// <exception cref="Exception">This state means that accessToken is not set up properly for given request</exception>
        public static async Task<bool> SubscribeAsync(string? clientId, string? accessToken, CreateSubscriptionRequest request, CancellationTokenSource clSource, ILogger logger, string? url = null)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                try
                {
                    string requestBody = JsonConvert.SerializeObject(request);
                    var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(url ?? BaseUrl, content, clSource.Token).ConfigureAwait(false);
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Accepted:
                            return true;

                        case HttpStatusCode.Unauthorized:
                            throw new InvalidAccessTokenException("Subscribe failed due" + await response.Content.ReadAsStreamAsync(clSource.Token) + response.ReasonPhrase);
                        case HttpStatusCode.Forbidden:
                            throw new Exception("Subscribe - Invalid Scopes");
                        default:
                            logger.LogWarningDetails("[EventSubClient] - [TwitchApi] - Subscribe got non-standard status code", requestBody, content, response);
                            return false;
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogErrorDetails($"[EventSubClient] - [TwitchApi] - SubscribeAsync returned exception", ex, request);
                    return false;
                }
            }
        }

        /// <summary>
        /// Function unsubscribes subs
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <param name="subscriptionId"></param>
        /// <returns>True on success, false on failure</returns>
        /// <exception cref="InvalidAccessTokenException"></exception>
        public static async Task<bool> UnSubscribeAsync(string? clientId, string? accessToken, string subscriptionId, CancellationTokenSource clSource, ILogger logger, string? url = null)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                try
                {
                    var urlSol = $"{url ?? BaseUrl}?id={subscriptionId}";
                    var response = await httpClient.DeleteAsync(urlSol, clSource.Token).ConfigureAwait(false);

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.NoContent: return true;
                        case HttpStatusCode.Unauthorized:
                            throw new InvalidAccessTokenException("Unsubscribe failed due" + await response.Content.ReadAsStringAsync(clSource.Token) + response.ReasonPhrase);
                        default:
                            logger.LogWarningDetails("[EventSubClient] - [TwitchApi] - UnSubscribeAsync got non-standard status code:", response);
                            return false;
                    };
                }
                catch (HttpRequestException ex)
                {
                    logger.LogErrorDetails($"[EventSubClient] - [TwitchApi] - UnsubscribeAsync returned exception", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets a list of subs
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <param name="statusSelector"></param>
        /// <param name="after"></param>
        /// <returns cref="GetSubscriptionsResponse"> Provides segment of subscriptions, content MAY BE NULL</returns>
        /// <exception cref="InvalidAccessTokenException"></exception>
        private static async Task<GetSubscriptionsResponse?> GetSubscriptionsAsync(string? clientId, string? accessToken, SubscriptionStatusTypes statusSelector, CancellationTokenSource clSource, ILogger logger, string? after = null, string? url = null)
        {
            var status = StatusProvider.GetStatusString(statusSelector);

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);

                try
                {
                    var queryBuilder = new StringBuilder(url ?? BaseUrl);

                    if (!string.IsNullOrEmpty(status))
                        queryBuilder.Append($"?status={WebUtility.UrlEncode(status)}");

                    if (!string.IsNullOrEmpty(after))
                        queryBuilder.Append($"&after={WebUtility.UrlEncode(after)}");

                    var response = await httpClient.GetAsync(queryBuilder.ToString(), clSource.Token).ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync(clSource.Token);
                    if (string.IsNullOrEmpty(body))
                    {
                        body = string.Empty;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK: return JsonConvert.DeserializeObject<GetSubscriptionsResponse>(body);
                        case HttpStatusCode.Unauthorized: throw new InvalidAccessTokenException("GetSubscriptions failed due" + body + response.ReasonPhrase);
                        default:
                            logger.LogWarningDetails("[EventSubClient] - [TwitchApi] - GetSubscriptions got non-standard status code", queryBuilder, response);
                            return default;
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogErrorDetails($"[EventSubClient] - [TwitchApi] - GetSubscriptions returned exception", ex, status);
                    return default;
                }
            }
        }

        /// <summary>
        /// Provides entire list of subscriptions, handles pagination.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <param name="statusSelector"></param>
        /// <returns> list of subscriptions</returns>
        /// <exception cref="InvalidAccessTokenException">May provide exception from GetSubscriptionsAsync</exception>
        public static async Task<List<GetSubscriptionsResponse>> GetAllSubscriptionsAsync(string? clientId, string? accessToken, CancellationTokenSource clSource, ILogger logger, SubscriptionStatusTypes statusSelector = SubscriptionStatusTypes.Enabled, string? url = null)
        {
            var allSubscriptions = new List<GetSubscriptionsResponse>();
            string? afterCursor = null;
            int totalPossibleIterations = Int32.MaxValue;

            for (int i = 0; i < totalPossibleIterations; i++)
            {
                var response = await GetSubscriptionsAsync(clientId, accessToken, statusSelector, clSource, logger, afterCursor, url).ConfigureAwait(false);
                if (response != null)
                {
                    allSubscriptions.Add(response);
                    if (afterCursor == null)
                    {
                        totalPossibleIterations = response.Total;
                    }
                    afterCursor = response.Cursor;
                }
                else
                {
                    logger.LogInformation("[EventSubClient] - [TwitchApi] Response returned null cause of invalid userId or filter parameter");
                    break;
                }

                if (string.IsNullOrEmpty(afterCursor))
                {
                    break;
                }
            }
            if (allSubscriptions.Count == 0)
            {
                logger.LogInformation("[EventSubClient] - [TwitchApi] List of subscriptions returned EMPTY!");
            }

            return allSubscriptions;
        }

        /// <summary>
        /// Validates the provided Twitch access token.
        /// </summary>
        /// <param name="accessToken">The access token to validate.</param>
        /// <param name="clSource">CancellationTokenSource for the request.</param>
        /// <param name="logger">ILogger for logging.</param>
        /// <returns>True if the token is valid, false if not.</returns>
        /// <exception cref="InvalidAccessTokenException"></exception>
        public static async Task<bool> ValidateTokenAsync(string? accessToken, CancellationTokenSource clSource, ILogger logger, string? url = null)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
                try
                {
                    var response = await httpClient.GetAsync(url ?? ValidateUrl, clSource.Token).ConfigureAwait(false);
                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.OK:
                            logger.LogDebug("[EventSubClient] - [TwitchApi] Validation of Token Successfull {StatusCode}", response.StatusCode);
                            return true;

                        case System.Net.HttpStatusCode.Unauthorized:
                            var errorMessage = await response.Content.ReadAsStringAsync(clSource.Token);
                            throw new InvalidAccessTokenException($"[EventSubClient] - [TwitchApi] Validation of token failed: {errorMessage} {response.ReasonPhrase}");
                        default:
                            logger.LogWarning("[EventSubClient] - [TwitchApi] ValidateTokenAsync got non-standard status code: {StatusCode}", response.StatusCode);
                            return false;
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError(ex, "[EventSubClient] - [TwitchApi] ValidateTokenAsync encountered an exception.");
                    return false;
                }
            }
        }
    }
}