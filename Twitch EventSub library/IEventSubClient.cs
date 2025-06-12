using Twitch.EventSub.API.Enums;
using Twitch.EventSub.Interfaces;

namespace Twitch.EventSub
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSubClient"/> class.
    /// </summary>
    /// <param name="clientId">The client ID.</param>
    /// <param name="logger">The logger instance.</param>
    public interface IEventSubClient
    {
        /// <summary>
        /// Indexer to get the event provider for a specific user.
        /// </summary>
        /// <param name="key">The user ID.</param>
        /// <returns>The event provider associated with the user ID.</returns>
        IEventProvider? this[string key] { get; }

        /// <summary>
        /// Adds a new user to the event subscription service, prepares Event Provider.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="listOfSubs">The list of subscription types.</param>
        /// <param name="allowRecovery">Allow internal recovery attempts.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<bool> AddUserAsync(string userId, string accessToken, List<SubscriptionTypes> listOfSubs, bool allowRecovery);

        /// <summary>
        /// Deletes an existing user from the event subscription service.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<bool> DeleteUserAsync(string userId);

        /// <summary>
        /// Gets the event provider for a specific user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The event provider associated with the user ID.</returns>
        IEventProvider? GetUserEventProvider(string userId);

        /// <summary>
        /// Checks if a specific user is connected to the event subscription service.
        /// This part may be used to detect failed user instances and recover them
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>True if the user is connected, false otherwise.</returns>
        bool IsConnected(string userId);

        /// <summary>
        /// Starts the event subscription for a specific user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<bool> StartAsync(string userId);

        /// <summary>
        /// Stops the event subscription for a specific user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<bool> StopAsync(string userId);

        /// <summary>
        /// Updates the subscription details for an existing user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="accessToken">The new access token.</param>
        /// <param name="listOfSubs">The new list of subscription types.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        bool UpdateUser(string userId, string accessToken, List<SubscriptionTypes> listOfSubs);
    }
}