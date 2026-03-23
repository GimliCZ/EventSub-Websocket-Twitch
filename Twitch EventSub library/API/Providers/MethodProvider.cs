using Twitch.EventSub.API.Enums;

namespace Twitch.EventSub.API.Providers
{
    public static class MethodProvider
    {
        public static TransportMethod GetMethod(string value)
        {
            return value switch
            {
                nameof(TransportMethod.websocket) => TransportMethod.websocket,
                nameof(TransportMethod.webhook) => TransportMethod.webhook,
                nameof(TransportMethod.conduit) => TransportMethod.conduit,
                _ => throw new ArgumentException($"Unknown transport method: {value}")
            };
        }
    }
}
