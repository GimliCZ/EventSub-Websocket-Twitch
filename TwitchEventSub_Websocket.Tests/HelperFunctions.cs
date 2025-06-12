using Newtonsoft.Json;
using System.Text.Json;
using Twitch.EventSub.Messages.SharedContents;

namespace TwitchEventSub_Websocket.Tests
{
    public class HelperFunctions
    {
        public static Task<string> LoadJsonAsync(string additionalPath, string fileName)
        {
            // Get the path to the current directory where the test is running
            var directory = Directory.GetCurrentDirectory();
            var filePath = Path.Combine(directory, additionalPath, fileName);
            return File.ReadAllTextAsync(filePath);
        }

        public static async Task<string> LoadNotificationAsync(string subscriptionType, string version, string additionalPath, string filename)
        {
            // Arrange
            var message = await LoadJsonAsync(additionalPath, filename);

            using var payloadDoc = JsonDocument.Parse(message);
            var payloadRoot = payloadDoc.RootElement.Clone();
            var metadataObj = new WebSocketMessageMetadata()
            {
                MessageId = "befa7b53-d79d-478f-86b9-120f112b044e",
                MessageType = "notification",
                MessageTimestamp = "2022-11-16T10:11:12.464757833Z",
                SubscriptionType = subscriptionType,
                SubscriptionVersion = version
            };
            var metadataString = JsonConvert.SerializeObject(metadataObj);
            // Create the final JSON structure directly
            var finalJson = new
            {
                metadata = metadataObj,
                payload = JsonConvert.DeserializeObject(message) // Deserialize to dynamic object
            };

            // Serialize the final JSON to a string using JsonConvert
            return JsonConvert.SerializeObject(finalJson, Formatting.Indented);
        }
    }
}