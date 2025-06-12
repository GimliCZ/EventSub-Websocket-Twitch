using Twitch.EventSub.API.Enums;

namespace Twitch.EventSub.API.Providers
{
    public class StatusProvider
    {
        public static string GetStatusString(SubscriptionStatusTypes status)
        {
            return status switch
            {
                SubscriptionStatusTypes.Enabled => "enabled",
                SubscriptionStatusTypes.NotificationFailuresExceeded => "notification_failures_exceeded",
                SubscriptionStatusTypes.AuthorizationRevoked => "authorization_revoked",
                SubscriptionStatusTypes.ModeratorRemoved => "moderator_removed",
                SubscriptionStatusTypes.UserRemoved => "user_removed",
                SubscriptionStatusTypes.VersionRemoved => "version_removed",
                SubscriptionStatusTypes.WebsocketDisconnected => "websocket_disconnected",
                SubscriptionStatusTypes.WebsocketFailedPingPong => "websocket_failed_ping_pong",
                SubscriptionStatusTypes.WebsocketReceivedInboundTraffic => "websocket_received_inbound_traffic",
                SubscriptionStatusTypes.WebsocketConnectionUnused => "websocket_connection_unused",
                SubscriptionStatusTypes.WebsocketInternalError => "websocket_internal_error",
                SubscriptionStatusTypes.WebsocketNetworkTimeout => "websocket_network_timeout",
                SubscriptionStatusTypes.WebsocketNetworkError => "websocket_network_error",
                SubscriptionStatusTypes.Empty => string.Empty,
                _ => throw new ArgumentException("Invalid subscription status.")
            };
        }
    }
}