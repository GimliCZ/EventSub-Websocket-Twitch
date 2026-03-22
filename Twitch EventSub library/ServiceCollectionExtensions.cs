using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Twitch.EventSub.API;
using Twitch.EventSub.APIConduit;
using Twitch.EventSub.CoreFunctions;

namespace Twitch.EventSub
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all TwitchEventSub services: named HttpClients with resilience and enrichment,
        /// API singletons, logging (if not already registered), and <see cref="IEventSubClient"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Action to configure <see cref="EventSubClientOptions"/> (e.g. ClientId).</param>
        public static IServiceCollection AddTwitchEventSub(
            this IServiceCollection services,
            Action<EventSubClientOptions> configure)
        {
            services.Configure(configure);
            services.AddTwitchEventSubHttpClients();
            services.AddTwitchEventSubClient();
            return services;
        }

        /// <summary>
        /// Registers the named HttpClients with standard resilience pipelines and enrichment,
        /// and API singletons required by TwitchEventSub.
        /// Named clients: <see cref="HttpClientNames.TwitchApi"/> and <see cref="HttpClientNames.TwitchApiConduit"/>.
        /// Use this when you want to configure the HttpClients yourself before calling
        /// <see cref="AddTwitchEventSubClient"/>.
        /// </summary>
        public static IServiceCollection AddTwitchEventSubHttpClients(this IServiceCollection services)
        {
            services.AddResilienceEnricher();

            services.AddHttpClient(HttpClientNames.TwitchApi, client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddStandardResilienceHandler(options =>
            {
                // Twitch EventSub API — tune retry/timeout to respect Twitch rate limits
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddHttpClient(HttpClientNames.TwitchApiConduit, client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddSingleton<TwitchApi>();
            services.AddSingleton<TwitchApiConduit>();

            return services;
        }

        /// <summary>
        /// Registers only the <see cref="IEventSubClient"/> singleton.
        /// Registers logging if <see cref="ILoggerFactory"/> has not already been registered.
        /// Use this when you have already registered named HttpClients and API singletons
        /// via <see cref="AddTwitchEventSubHttpClients"/> or your own configuration.
        /// Requires <see cref="EventSubClientOptions"/> to be configured via
        /// <c>services.Configure&lt;EventSubClientOptions&gt;(...)</c> before calling this.
        /// </summary>
        public static IServiceCollection AddTwitchEventSubClient(this IServiceCollection services)
        {
            if (!services.Any(d => d.ServiceType == typeof(ILoggerFactory)))
            {
                services.AddLogging();
            }

            services.AddSingleton<IEventSubClient, EventSubClient>();

            return services;
        }
    }
}