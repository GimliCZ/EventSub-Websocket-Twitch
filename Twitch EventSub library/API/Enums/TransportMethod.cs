namespace Twitch.EventSub.API.Enums
{
    // Values are lowercase to match Twitch API transport method strings directly.
    // Use nameof(TransportMethod.websocket) or method.ToString() for serialization.
    public enum TransportMethod
    {
        websocket,
        webhook,
        conduit
    }
}
