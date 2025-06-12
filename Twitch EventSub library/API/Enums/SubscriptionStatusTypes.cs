namespace Twitch.EventSub.API.Enums
{
    public enum SubscriptionStatusTypes
    {
        Enabled,
        NotificationFailuresExceeded,
        AuthorizationRevoked,
        ModeratorRemoved,
        UserRemoved,
        VersionRemoved,
        WebsocketDisconnected,
        WebsocketFailedPingPong,
        WebsocketReceivedInboundTraffic,
        WebsocketConnectionUnused,
        WebsocketInternalError,
        WebsocketNetworkTimeout,
        WebsocketNetworkError,

        //My addition - describes all possible states
        Empty
    }
}