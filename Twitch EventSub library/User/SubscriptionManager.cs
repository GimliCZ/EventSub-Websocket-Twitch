using Microsoft.Extensions.Logging;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;

namespace Twitch.EventSub.User
{
    /// <summary>
    /// Implemenation of API with protections and integrations to rest of library
    /// </summary>
    public class SubscriptionManager
    {
        private readonly string _url;

        public SubscriptionManager(string url = null)
        {
            _url = url;
        }

        /// <summary>
        /// Event relaying access token refresh from API
        /// </summary>
        public event AsyncEventHandler<RefreshRequestArgs> OnRefreshTokenRequestAsync;

        /// <summary>
        /// Procedure refreshing subriptions
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="requestedSubscriptions">Requested Subscriptions</param>
        /// <param name="clientId">Client ID</param>
        /// <param name="accessToken">Access Token</param>
        /// <param name="sessionId">Session Id</param>
        /// <param name="clSource">Cancelation Source</param>
        /// <param name="logger">Logger instance</param>
        /// <returns>Return true if all operations succeed</returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<bool> RunCheckAsync(string userId, List<CreateSubscriptionRequest> requestedSubscriptions, string clientId, string accessToken, string sessionId, CancellationTokenSource clSource, ILogger logger)
        {
            foreach (var typeListOfSub in requestedSubscriptions)
            {
                typeListOfSub.Transport.SessionId = sessionId;
            }

            //test for dupes
            // DOC: _requestedSubscriptions is always non null type
            if (requestedSubscriptions!
                .GroupBy(reqSub => new
                {
                    reqSub.Type
                }).Any(group => group.Count() > 1))
            {
                throw new Exception("[EventSubClient] - [SubscriptionManager] - List contains dupes");
            }

            //remove old connections, old sessions and all subscriptions with error status
            if (clientId == null || accessToken == null)
            {
                throw new ArgumentNullException(nameof(clientId) + nameof(accessToken));
            }
            var allSubscriptions = await ApiTryGetAllSubscriptionsAsync(clientId, accessToken, userId, clSource, logger, SubscriptionStatusTypes.Empty);
            //Yes we can get null from subscription function, if something goes horribly wrong.
            if (allSubscriptions == null)
            {
                logger.LogInformation("[EventSubClient] - [SubscriptionManager] Subscription function returned null, skipping check");
                return false;
            }
            foreach (var getSubscriptionsResponse in allSubscriptions)
            {
                foreach (var subscription in getSubscriptionsResponse.Data)
                {
                    if (subscription.Transport.SessionId != sessionId || subscription.Status != "enabled" ||
                        DateTime.UtcNow - ReplayProtection.ParseDateTimeString(subscription.CreatedAt) > TimeSpan.FromHours(1))
                    {
                        if (!await ApiTryUnSubscribeAsync(clientId, accessToken, subscription.Id, userId, logger, clSource))
                        {
                            logger.LogInformation("[EventSubClient] - [SubscriptionManager] Failed to unsubscribe during check" + subscription.Type);
                            return false;
                        }
                        logger.LogInformation("[EventSubClient] - [SubscriptionManager] Cleared subscription:" + subscription.Type);
                    }
                }
            }

            //Rerun subscription search to get all active current session subs
            allSubscriptions = await ApiTryGetAllSubscriptionsAsync(clientId, accessToken, userId, clSource, logger, SubscriptionStatusTypes.Empty);
            //Yes we can get null from subscription function, if something goes horribly wrong.
            if (allSubscriptions == null)
            {
                logger.LogInformation("[EventSubClient] - [SubscriptionManager] Subscription function returned null, skipping check");
                return false;
            }
            foreach (var getSubscriptionsResponse in allSubscriptions)
            {
                var activeSubscriptions = getSubscriptionsResponse.Data;

                // Find subscriptions that are extra (present in activeSubscriptions but not in _requestedSubscriptions)
                var extraSubscriptions = activeSubscriptions
                .Where(subscription => !requestedSubscriptions!.Any(reqSub =>
                reqSub.Type == subscription.Type && reqSub.Version == subscription.Version)).ToList();

                // Find subscriptions that are missing (present in _requestedSubscriptions but not in activeSubscriptions)
                var missingSubscriptions = requestedSubscriptions!
                .Where(reqSub => !activeSubscriptions.Any(subscription =>
                subscription.Type == reqSub.Type && subscription.Version == reqSub.Version)).ToList();

                // Handle extra and missing subscriptions
                if (extraSubscriptions.Any())
                {
                    // Perform your logic here for extra subscriptions
                    foreach (var extraSubscription in extraSubscriptions)
                    {
                        if (await ApiTryUnSubscribeAsync(clientId, accessToken, extraSubscription.Id, userId, logger, clSource))
                        {
                            logger.LogInformation("[EventSubClient] - [SubscriptionManager] Failed to unsubscribe active subscription during check" + extraSubscription.Type);
                            return false;
                        }
                        logger.LogInformation("[EventSubClient] - [SubscriptionManager] Removed extra sub: " + extraSubscription.Type);
                    }
                }

                if (missingSubscriptions.Any())
                {
                    // Perform your logic here for missing subscriptions
                    foreach (var missingSubscription in missingSubscriptions)
                    {
                        if (!await ApiTrySubscribeAsync(clientId, accessToken, missingSubscription, userId, logger, clSource))
                        {
                            logger.LogInformation("[EventSubClient] - [SubscriptionManager] Failed to subscribe subscription during check");
                            return false;
                        }
                        logger.LogInformation("[EventSubClient] - [SubscriptionManager] Added extra sub: " + missingSubscription.Type);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Clearing procedure
        /// </summary>
        /// <param name="clientId">Client ID</param>
        /// <param name="accessToken">Access Token</param>
        /// <param name="userId">User Id</param>
        /// <param name="logger">Logger Instance</param>
        /// <param name="clSource">Cancelation token source</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task ClearAsync(string clientId, string accessToken, string userId, ILogger logger, CancellationTokenSource clSource)
        {
            if (clientId == null)
            {
                throw new ArgumentNullException(nameof(clientId));
            }
            if (accessToken == null)
            {
                throw new ArgumentNullException(nameof(accessToken));
            }
            var allSubscriptions = await ApiTryGetAllSubscriptionsAsync(clientId, accessToken, userId, clSource, logger, SubscriptionStatusTypes.Empty);
            if (allSubscriptions is null)
            {
                return;
            }
            foreach (var getSubscriptionsResponse in allSubscriptions)
            {
                if (getSubscriptionsResponse is null || getSubscriptionsResponse.Data is null)
                {
                    logger.LogInformation("[EventSubClient] - [SubscriptionManager] Retrieved null Subscription Response");
                    continue;
                }

                foreach (var subscription in getSubscriptionsResponse.Data)
                {
                    if (subscription is null)
                    {
                        logger.LogInformation("[EventSubClient] - [SubscriptionManager] Retrieved null Subscription");
                        continue;
                    }

                    if (!await ApiTryUnSubscribeAsync(clientId, accessToken, subscription.Id, userId, logger, clSource))
                    {
                        logger.LogWarningDetails("[EventSubClient] - [SubscriptionManager] Failed to unsubscribe during clear", subscription);
                        continue;
                    }
                    logger.LogInformation("[EventSubClient] - [SubscriptionManager] Sub cleared: " + subscription.Type);
                }
            }
        }

        /// <summary>
        /// Token Validation call hiden behind access token invalid protection
        /// </summary>
        /// <param name="accessToken">Access token</param>
        /// <param name="userId">User Id</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="clSource">Cancelation token source</param>
        /// <returns>Returns true if token valid</returns>
        public Task<bool> ApiTryValidateAsync(
            string accessToken,
            string userId,
            ILogger logger,
            CancellationTokenSource clSource)
        {
            Task<bool> TryValidateAsync() => TwitchApi.ValidateTokenAsync(accessToken, clSource, logger, _url);
            return TryFuncAsync(TryValidateAsync, logger, userId);
        }

        /// <summary>
        /// Subscription call hiden behind access token invalid protection
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accessToken"></param>
        /// <param name="create"></param>
        /// <param name="userId"></param>
        /// <param name="logger"></param>
        /// <param name="clSource"></param>
        /// <returns></returns>
        public Task<bool> ApiTrySubscribeAsync(
            string clientId,
            string accessToken,
            CreateSubscriptionRequest create,
            string userId,
            ILogger logger,
            CancellationTokenSource clSource)
        {
            Task<bool> TrySubscribeAsync() => TwitchApi.SubscribeAsync(clientId, accessToken, create, clSource, logger, _url);
            return TryFuncAsync(TrySubscribeAsync, logger, userId);
        }

        /// <summary>
        /// UnSubscribe call hiden behind access token invalid protection
        /// </summary>
        /// <param name="clientId">Client ID</param>
        /// <param name="accessToken">Access Token</param>
        /// <param name="subId">Identifier of subscription</param>
        /// <param name="userId">User Id</param>
        /// <param name="logger">Logger Instance</param>
        /// <param name="clSource">Cancelation Token Source</param>
        /// <returns>Returns true, if unsubscribe was successfull</returns>
        private Task<bool> ApiTryUnSubscribeAsync(string clientId, string accessToken, string subId, string userId, ILogger logger, CancellationTokenSource clSource)
        {
            Task<bool> TryUnSubscribeAsync() => TwitchApi.UnSubscribeAsync(clientId, accessToken, subId, clSource, logger, _url);
            return TryFuncAsync(TryUnSubscribeAsync, logger, userId);
        }

        /// <summary>
        /// Group subscription Request hiden behind access token invalid protection
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="accessToken">Access Token</param>
        /// <param name="userId">User Id</param>
        /// <param name="clSource">Cancelation Token Source</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="statusSelector">Filtration of status</param>
        /// <returns>Returns all subscriptions requested by filter, on fail returns null</returns>
        private Task<List<GetSubscriptionsResponse>?> ApiTryGetAllSubscriptionsAsync(string clientId, string accessToken, string userId, CancellationTokenSource clSource, ILogger logger, SubscriptionStatusTypes statusSelector)
        {
            Task<List<GetSubscriptionsResponse>> TryGetAllSubscriptionsAsync() => TwitchApi.GetAllSubscriptionsAsync(clientId, accessToken, clSource, logger, statusSelector, _url);
            return TryFuncAsync(TryGetAllSubscriptionsAsync, logger, userId);
        }

        /// <summary>
        /// This should catch any AccessToken exception and calls outside for changes.
        /// Then it calls function again.
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="apiCallAction"></param>
        /// <param name="logger"></param>
        /// <param name="UserId">User ID</param>
        /// <returns></returns>
        private async Task<TType?> TryFuncAsync<TType>(Func<Task<TType>> apiCallAction, ILogger logger, string UserId)
        {
            try
            {
                return await apiCallAction();
            }
            catch (InvalidAccessTokenException ex)
            {
                //procedure must run UpdateOnFly function for proper change
                logger.LogInformationDetails("[EventSubClient] - [SubscriptionManager] Invalid Access token detected, requesting change.", ex);
                await OnRefreshTokenRequestAsync.TryInvoke(this, new RefreshRequestArgs { UserId = UserId, DateTime = DateTime.Now });
            }
            catch (TaskCanceledException)
            {
                logger.LogWarning($"[EventSubClient] - [SubscriptionManager] Task cancelled before completion. Try to increase cancelation token");
            }
            catch (Exception ex)
            {
                logger.LogInformationDetails("[EventSubClient] - [SubscriptionManager] Api call failed due to:", ex);
            }
            //This is expected behavior. If we get null or false, we handle it in higher part of function
            logger.LogInformationDetails("[EventSubClient] - [SubscriptionManager] Try Func Async returned Default value.", apiCallAction.Method.Name);
            return default;
        }
    }
}