using Microsoft.Extensions.Logging;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.SharedContents;

namespace Twitch.EventSub.User
{
    /// <summary>
    /// Implemenation of API with protections and integrations to rest of library
    /// </summary>
    public class SubscriptionManager
    {
        private readonly string _url;
        private readonly TwitchApi _twitchApi;

        public SubscriptionManager(TwitchApi twitchApi, string url = null)
        {
            _url = url;
            _twitchApi = twitchApi;
        }

        /// <summary>
        /// Returns the subset of conduit subscriptions owned by this user, identified by condition:
        /// broadcaster_user_id, user_id, or moderator_user_id equal to the user id.
        /// </summary>
        public static List<WebSocketSubscription> OwnedSlice(IEnumerable<WebSocketSubscription> all, string userId)
        {
            return all.Where(s =>
                s?.Condition != null &&
                (s.Condition.BroadcasterUserId == userId ||
                 s.Condition.UserId == userId ||
                 s.Condition.ModeratorUserId == userId)).ToList();
        }

        /// <summary>
        /// Conduit-scoped owned slice: this user's subscriptions that live on the given conduit only.
        /// Under redundancy the same user+condition sub exists on multiple conduits; each replica must
        /// reconcile ONLY its own conduit's copies, or replicas would delete each other's subscriptions.
        /// </summary>
        public static List<WebSocketSubscription> OwnedSlice(IEnumerable<WebSocketSubscription> all, string userId, string conduitId)
        {
            return OwnedSlice(all, userId)
                .Where(s => s.Transport?.ConduitId == conduitId)
                .ToList();
        }

        /// <summary>Exact per-user subscription accounting from the last reconciliation pass.</summary>
        public sealed class ReconcileReport
        {
            public ReconcileReport(string userId, int ownedCount, int created, int removed)
            {
                UserId = userId;
                OwnedCount = ownedCount;
                Created = created;
                Removed = removed;
            }
            public string UserId { get; }
            public int OwnedCount { get; }
            public int Created { get; }
            public int Removed { get; }
        }

        /// <summary>The most recent reconciliation report for this user (null until first RunCheckAsync).</summary>
        public ReconcileReport? LastReport { get; private set; }

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
        /// <param name="appAccessToken">App Access Token</param>
        /// <param name="conduitId">Conduit ID</param>
        /// <param name="clSource">Cancelation Source</param>
        /// <param name="logger">Logger instance</param>
        /// <returns>Return true if all operations succeed</returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<bool> RunCheckAsync(string userId, List<CreateSubscriptionRequest> requestedSubscriptions, string clientId, string appAccessToken, string conduitId, CancellationTokenSource clSource, ILogger logger)
        {
            foreach (var typeListOfSub in requestedSubscriptions)
            {
                typeListOfSub.Transport.Method = nameof(TransportMethod.conduit);
                typeListOfSub.Transport.ConduitId = conduitId;
                typeListOfSub.Transport.SessionId = null;
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
            if (clientId == null || appAccessToken == null)
            {
                throw new ArgumentNullException(nameof(clientId) + nameof(appAccessToken));
            }
            var allSubscriptions = await ApiTryGetAllSubscriptionsAsync(clientId, appAccessToken, userId, clSource, logger, SubscriptionStatusTypes.Empty);
            //Yes we can get null from subscription function, if something goes horribly wrong.
            if (allSubscriptions == null)
            {
                logger.LogInformation("[EventSubClient] - [SubscriptionManager] Subscription function returned null, skipping check");
                return false;
            }
            foreach (var getSubscriptionsResponse in allSubscriptions)
            {
                foreach (var subscription in OwnedSlice(getSubscriptionsResponse.Data, userId, conduitId))
                {
                    if (subscription.Status != "enabled" ||
                        DateTime.UtcNow - ReplayProtection.ParseDateTimeString(subscription.CreatedAt) > TimeSpan.FromHours(1))
                    {
                        if (!await ApiTryUnSubscribeAsync(clientId, appAccessToken, subscription.Id, userId, logger, clSource))
                        {
                            logger.LogInformation("[EventSubClient] - [SubscriptionManager] Failed to unsubscribe during check" + subscription.Type);
                            return false;
                        }
                        logger.LogInformation("[EventSubClient] - [SubscriptionManager] Cleared subscription:" + subscription.Type);
                    }
                }
            }

            //Rerun subscription search to get all active current session subs
            allSubscriptions = await ApiTryGetAllSubscriptionsAsync(clientId, appAccessToken, userId, clSource, logger, SubscriptionStatusTypes.Empty);
            //Yes we can get null from subscription function, if something goes horribly wrong.
            if (allSubscriptions == null)
            {
                logger.LogInformation("[EventSubClient] - [SubscriptionManager] Subscription function returned null, skipping check");
                return false;
            }
            int created = 0;
            int removed = 0;
            foreach (var getSubscriptionsResponse in allSubscriptions)
            {
                var activeSubscriptions = OwnedSlice(getSubscriptionsResponse.Data, userId, conduitId);

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
                        if (!await ApiTryUnSubscribeAsync(clientId, appAccessToken, extraSubscription.Id, userId, logger, clSource))
                        {
                            logger.LogInformation("[EventSubClient] - [SubscriptionManager] Failed to unsubscribe active subscription during check" + extraSubscription.Type);
                            return false;
                        }
                        removed++;
                        logger.LogInformation("[EventSubClient] - [SubscriptionManager] Removed extra sub: " + extraSubscription.Type);
                    }
                }

                if (missingSubscriptions.Any())
                {
                    // Perform your logic here for missing subscriptions
                    foreach (var missingSubscription in missingSubscriptions)
                    {
                        if (!await ApiTrySubscribeAsync(clientId, appAccessToken, missingSubscription, userId, logger, clSource))
                        {
                            logger.LogInformation("[EventSubClient] - [SubscriptionManager] Failed to subscribe subscription during check");
                            return false;
                        }
                        created++;
                        logger.LogInformation("[EventSubClient] - [SubscriptionManager] Added extra sub: " + missingSubscription.Type);
                    }
                }
            }
            var ownedCount = OwnedSlice(allSubscriptions.SelectMany(r => r.Data), userId, conduitId).Count;
            LastReport = new ReconcileReport(userId, ownedCount, created, removed);
            logger.LogInformation("[EventSubClient] - [SubscriptionManager] user {U}: owned={O} created={C} removed={R}", userId, ownedCount, created, removed);
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
            Task<bool> TryValidateAsync() => _twitchApi.ValidateTokenAsync(accessToken, clSource, logger, _url);
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
            Task<bool> TrySubscribeAsync() => _twitchApi.SubscribeAsync(clientId, accessToken, create, clSource, logger, _url);
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
            Task<bool> TryUnSubscribeAsync() => _twitchApi.UnSubscribeAsync(clientId, accessToken, subId, clSource, logger, _url);
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
            Task<List<GetSubscriptionsResponse>> TryGetAllSubscriptionsAsync() => _twitchApi.GetAllSubscriptionsAsync(clientId, accessToken, clSource, logger, statusSelector, _url);
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