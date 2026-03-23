using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Interfaces;
using Twitch.EventSub.User;

namespace Twitch.EventSub
{
    /// <summary>
    /// The EventSubClient class manages the interaction with the Twitch EventSub websocket service,
    /// allowing the addition, updating, deletion, and management of user event subscriptions.
    /// Implements <see cref="IHostedService"/> for .NET DI hosting integration and
    /// <see cref="IAsyncDisposable"/> for clean teardown.
    /// </summary>
    public class EventSubClient : IEventSubClient, IHostedService, IAsyncDisposable
    {
        private readonly string _clientId;
        private readonly string _appAccessToken;
        private readonly ConcurrentDictionary<string, EventProvider> _eventDictionary;
        private readonly ILogger _logger;
        private readonly TwitchApi _twitchApi;
        private readonly IConduitOrchestrator _conduitOrchestrator;
        private readonly IShardManager _shardManager;
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSubClient"/> class.
        /// </summary>
        /// <param name="options">Configuration options containing the client ID.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="twitchApi">The TwitchApi singleton instance.</param>
        /// <param name="conduitOrchestrator">Manages conduit lifecycle with the Twitch API.</param>
        /// <param name="shardManager">Manages WebSocket shard allocation for users.</param>
        public EventSubClient(
            IOptions<EventSubClientOptions> options,
            ILogger<EventSubClient> logger,
            TwitchApi twitchApi,
            IConduitOrchestrator conduitOrchestrator,
            IShardManager shardManager)
        {
            _clientId = options.Value.ClientId;
            _appAccessToken = options.Value.AppAccessToken;
            _eventDictionary = new ConcurrentDictionary<string, EventProvider>();
            _logger = logger;
            _twitchApi = twitchApi;
            _conduitOrchestrator = conduitOrchestrator;
            _shardManager = shardManager;
            _logger.LogDebug("EventSubClient instantiated with clientId: {ClientId}", _clientId);
        }

        /// <summary>
        /// Indexer to get the event provider for a specific user.
        /// </summary>
        /// <param name="key">The user ID.</param>
        /// <returns>The event provider associated with the user ID.</returns>
        public IEventProvider? this[string key] => GetUserEventProvider(key);

        /// <summary>
        /// Gets the event provider for a specific user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The event provider associated with the user ID.</returns>
        public IEventProvider? GetUserEventProvider(string userId)
        {
            _eventDictionary.TryGetValue(userId, out var provider);
            return provider;
        }

        /// <summary>
        /// Adds a new user to the event subscription service, prepares Event Provider.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="listOfSubs">The list of subscription types.</param>
        /// <param name="allowRecovery">Allow internal recovery attempts.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        public async Task<bool> AddUserAsync(
            string userId,
            string accessToken,
            List<SubscriptionTypes> listOfSubs,
            bool allowRecovery)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogErrorDetails("AddUser Failed due null or empty key", userId);
                return false;
            }
            if (_eventDictionary.ContainsKey(userId))
            {
                _logger.LogErrorDetails("AddUser Failed due key being already in dictionary", userId);
                return false;
            }

            var eventProvider = new EventProvider(userId, accessToken, listOfSubs, _clientId, _logger, allowRecovery, _twitchApi, _conduitOrchestrator, _appAccessToken);
            _logger.LogDebug("Attempting to add user");
            return _eventDictionary.TryAdd(userId, eventProvider);
        }

        /// <summary>
        /// Updates the subscription details for an existing user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="accessToken">The new access token.</param>
        /// <param name="listOfSubs">The new list of subscription types.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        public bool UpdateUser(string userId, string accessToken, List<SubscriptionTypes> listOfSubs)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogErrorDetails("UpdateUser Failed due null or empty key", userId);
                return false;
            }
            if (_eventDictionary.TryGetValue(userId, out var eventProvider))
            {
                _logger.LogDebug("Attempting to update user");
                return eventProvider.Update(accessToken, listOfSubs);
            }
            else
            {
                _logger.LogErrorDetails("UpdateUser Failed due key being already in dictionary", userId);
                return false;
            }
        }

        /// <summary>
        /// Deletes an existing user from the event subscription service.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        public async Task<bool> DeleteUserAsync(string userId)
        {
            _logger.LogDebug("Attempting to delete user with userId: {UserId}", userId);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogError("DeleteUser failed due to null or empty userId");
                return false;
            }
            if (_eventDictionary.TryGetValue(userId, out var sequencer))
            {
                await sequencer.StopAsync();
                if (_eventDictionary.TryRemove(userId, out _))
                {
                    return true;
                }
            }
            _logger.LogInformation("DeleteUser failed because userId does not exist: {UserId}", userId);
            return false;
        }

        /// <summary>
        /// Starts the event subscription for a specific user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        public async Task<bool> StartAsync(string userId)
        {
            _logger.LogDebug("Attempting to start user with userId: {UserId}", userId);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogError("StartAsync failed due to null or empty userId");
                return false;
            }
            _eventDictionary.TryGetValue(userId, out var provider);
            if (provider is null)
            {
                return false;
            }
            _logger.LogDebug("StartAsync succeeded for userId: {UserId}", userId);
            await provider.StartAsync();
            return true;
        }

        /// <summary>
        /// Stops the event subscription for a specific user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        public Task<bool> StopAsync(string userId)
        {
            _logger.LogDebug("Attempting to stop user with userId: {UserId}", userId);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogError("StopAsync failed due to null or empty userId");
                return Task.FromResult(false);
            }
            _eventDictionary.TryGetValue(userId, out var provider);
            if (provider is null)
            {
                return Task.FromResult(false);
            }
            _logger.LogDebug("StopAsync succeeded for userId: {UserId}", userId);
            return provider.StopAsync();
        }

        /// <summary>
        /// Checks if a specific user is connected to the event subscription service.
        /// This part may be used to detect failed user instances and recover them
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>True if the user is connected, false otherwise.</returns>
        public bool IsConnected(string userId)
        {
            _logger.LogDebug("Checking if user is connected with userId: {UserId}", userId);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogError("IsConnected check failed due to null or empty userId");
                return false;
            }
            _eventDictionary.TryGetValue(userId, out var provider);
            if (provider is null)
            {
                return false;
            }
            return provider.IsConnected;
        }

        // ── IHostedService ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by the .NET host on startup. Initializes the conduit and subscribes to shard session updates.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("EventSubClient starting — initializing conduit");
            await _conduitOrchestrator.InitializeAsync(cancellationToken);
            _shardManager.OnSessionIdUpdated += OnShardSessionIdUpdated;
        }

        /// <summary>
        /// Called by the .NET host on shutdown. Tears down the conduit and disposes the shard manager.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;
            _shardManager.OnSessionIdUpdated -= OnShardSessionIdUpdated;
            _logger.LogInformation("EventSubClient stopping — tearing down conduit");
            await _conduitOrchestrator.TeardownAsync(cancellationToken);
            await _shardManager.DisposeAsync();
        }

        // ── IAsyncDisposable ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private async void OnShardSessionIdUpdated(object? sender, SessionIdUpdatedArgs args)
        {
            try
            {
                if (args.NewSessionId == null)
                {
                    await _conduitOrchestrator.RemoveShardAsync(args.ShardId, CancellationToken.None);
                }
                else if (args.OldSessionId == null)
                    await _conduitOrchestrator.AddShardAsync(args.ShardId, args.NewSessionId, CancellationToken.None);
                else
                    await _conduitOrchestrator.UpdateShardAsync(args.ShardId, args.OldSessionId, args.NewSessionId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OnShardSessionIdUpdated failed for shard {ShardId}", args.ShardId);
            }
        }
    }
}
