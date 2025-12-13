using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.PingMessage;
using Twitch.EventSub.Messages.ReconnectMessage;
using Twitch.EventSub.Messages.RevocationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Twitch.EventSub.Messages.WelcomeMessage;
using Twitch.EventSub.SubsRegister.Models;

namespace Twitch.EventSub.User
{
    public static class MessageProcessing
    {
        private static WebSocketNotificationPayload CreateNotificationPayload(JToken payload)
        {
            var resultMessage = new WebSocketNotificationPayload
            {
                Subscription = payload["subscription"]?.ToObject<WebSocketSubscription>()
            };

            var eventType = payload["subscription"]?["type"]?.ToObject<string>();
            var version = payload["subscription"]?["version"]?.ToObject<string>();

            if (eventType == null || version == null)
            {
                return resultMessage;
            }
            
            var key = new RegisterKeyVersion(eventType, version);

            if (Twitch.EventSub.SubsRegister.Register.RegisterDictionaryByVersion.TryGetValue(key, out var registryItem))
            {
                var eventTypeObject = registryItem.SpecificObject;
                var eventTypeInstance = payload["event"]?.ToObject(eventTypeObject);

                // Ensure the eventTypeInstance is of the correct type
                if (eventTypeInstance is WebSocketNotificationEvent notificationEvent)
                {
                    resultMessage.Event = notificationEvent;
                }
            }

            return resultMessage;
        }

        public static async Task<WebSocketMessage> DeserializeMessageAsync(string message)
        {
            using (JsonTextReader reader = new JsonTextReader(new StringReader(message)))
            {
                //Bypass Json
                reader.DateParseHandling = DateParseHandling.None;
                reader.SupportMultipleContent = true;
                while (await reader.ReadAsync())
                {
                    JObject jsonObject = JObject.Load(reader);
                    if (!jsonObject.TryGetValue("metadata", out JToken? metadataToken) || !(metadataToken is JObject))
                    {
                        throw new JsonSerializationException($"metadata is missing in the JSON object {message}");
                    }
                    var metadata = metadataToken.ToObject<WebSocketMessageMetadata>();
                    if (metadata == null)
                    {
                        throw new JsonSerializationException();
                    }
                    string messageType = metadata.MessageType;

                    if (!jsonObject.TryGetValue("payload", out JToken? payloadToken) || !(payloadToken is JObject))
                    {
                        throw new JsonSerializationException($"metadata is missing in the JSON object {message}");
                    }

                    return messageType switch
                    {
                        "session_welcome" => new WebSocketWelcomeMessage()
                        {
                            Metadata = metadata,
                            Payload = payloadToken.ToObject<WebSocketWelcomePayload>()
                        },
                        "notification" => new WebSocketNotificationMessage()
                        {
                            Metadata = metadata,
                            Payload = CreateNotificationPayload(payloadToken)
                        },
                        "ping" => new WebSocketPingMessage()
                        {
                            Metadata = metadata
                        },
                        "session_keepalive" => new WebSocketKeepAliveMessage()
                        {
                            Metadata = metadata,
                        },
                        "session_reconnect" => new WebSocketReconnectMessage()
                        {
                            Metadata = metadata,
                            Payload = payloadToken?.ToObject<WebSocketReconnectPayload>()
                        },
                        "revocation" => new WebSocketRevocationMessage()
                        {
                            Metadata = metadata,
                            Payload = payloadToken?.ToObject<WebSocketRevokedSubscriptions>()
                        },
                        _ => throw new JsonSerializationException($"Unsupported message_type: {messageType}")
                    };
                }
                throw new JsonSerializationException($"JSON object was not correctly processed {message}");
            }
        }
    }
}