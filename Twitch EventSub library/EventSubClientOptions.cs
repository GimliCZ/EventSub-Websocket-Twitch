using Microsoft.Extensions.Options;

namespace Twitch.EventSub
{
    public record EventSubClientOptions
    {
        /// <summary>
        /// Your Twitch application client ID.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;
    }
}