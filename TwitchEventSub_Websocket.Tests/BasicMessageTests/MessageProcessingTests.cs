using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.PingMessage;
using Twitch.EventSub.Messages.ReconnectMessage;
using Twitch.EventSub.Messages.RevocationMessage;
using Twitch.EventSub.Messages.WelcomeMessage;
using Twitch.EventSub.User;

namespace TwitchEventSub_Websocket.Tests.BasicMessageTests
{
    public class MessageProcessingTests
    {
        private static readonly string AddPath = Path.Combine("BasicMessageTests", "BasicMessages");


        [Fact]
        public async Task DeserializeMessageAsync_WhenGivenSessionWelcomeMessage_ReturnsWebSocketWelcomeMessage()
        {
            // Arrange
            var message = await HelperFunctions.LoadJsonAsync(AddPath, "SessionWelcomeMessage.json");

            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketWelcomeMessage>(result);
            var welcomeMessage = (WebSocketWelcomeMessage)result;
            Assert.Equal("session_welcome", welcomeMessage.Metadata.MessageType);
            Assert.Equal("96a3f3b5-5dec-4eed-908e-e11ee657416c", welcomeMessage.Metadata.MessageId);
            Assert.Equal("AQoQILE98gtqShGmLD7AM6yJThAB", welcomeMessage?.Payload?.Session?.Id);
        }

        [Fact]
        public async Task DeserializeMessageAsync_WhenGivenSessionKeepaliveMessage_ReturnsWebSocketKeepaliveMessage()
        {
            // Arrange
            var message = await HelperFunctions.LoadJsonAsync(AddPath, "SessionKeepaliveMessage.json");

            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketKeepAliveMessage>(result);
            var keepaliveMessage = (WebSocketKeepAliveMessage)result;
            Assert.Equal("session_keepalive", keepaliveMessage.Metadata.MessageType);
            Assert.Equal("84c1e79a-2a4b-4c13-ba0b-4312293e9308", keepaliveMessage.Metadata.MessageId);
        }

        [Fact]
        public async Task DeserializeMessageAsync_WhenGivenNotificationMessage_ReturnsWebSocketNotificationMessage()
        {
            // Arrange
            var message = await HelperFunctions.LoadJsonAsync(AddPath, "NotificationMessage.json");

            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            Assert.Equal("notification", notificationMessage.Metadata.MessageType);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("channel.follow", notificationMessage?.Payload?.Subscription?.Type);
        }

        [Fact]
        public async Task DeserializeMessageAsync_WhenGivenPingMessage_ReturnsWebSocketPingMessage()
        {
            // Arrange
            var message = await HelperFunctions.LoadJsonAsync(AddPath, "PingMessage.json");

            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketPingMessage>(result);
            var pingMessage = (WebSocketPingMessage)result;
            Assert.Equal("ping", pingMessage.Metadata.MessageType);
            Assert.Equal("84c1e79a-2a4b-4c13-ba0b-4312293e9308", pingMessage.Metadata.MessageId);
        }

        [Fact]
        public async Task DeserializeMessageAsync_WhenGivenSessionReconnectMessage_ReturnsWebSocketReconnectMessage()
        {
            // Arrange
            var message = await HelperFunctions.LoadJsonAsync(AddPath, "SessionReconnectMessage.json");

            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketReconnectMessage>(result);
            var reconnectMessage = (WebSocketReconnectMessage)result;
            Assert.Equal("session_reconnect", reconnectMessage.Metadata.MessageType);
            Assert.Equal("84c1e79a-2a4b-4c13-ba0b-4312293e9308", reconnectMessage.Metadata.MessageId);
            Assert.Equal("wss://eventsub.wss.twitch.tv?...", reconnectMessage?.Payload?.Session?.ReconnectUrl);
        }

        [Fact]
        public async Task DeserializeMessageAsync_WhenGivenRevocationMessage_ReturnsWebSocketRevocationMessage()
        {
            // Arrange
            var message = await HelperFunctions.LoadJsonAsync(AddPath, "RevocationMessage.json");

            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketRevocationMessage>(result);
            var revocationMessage = (WebSocketRevocationMessage)result;
            Assert.Equal("revocation", revocationMessage.Metadata.MessageType);
            Assert.Equal("84c1e79a-2a4b-4c13-ba0b-4312293e9308", revocationMessage.Metadata.MessageId);
            Assert.Equal("authorization_revoked", revocationMessage?.Payload?.Subscription?.Status);
        }
    }
}