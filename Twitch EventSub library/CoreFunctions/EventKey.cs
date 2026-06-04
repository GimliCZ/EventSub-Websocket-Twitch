using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Stable content hash of a notification used to collapse redundant cross-conduit deliveries,
/// which carry different message_ids and different conduit transports for the same real event.
/// Key = SHA256( type | version | condition-json | event-json ). Transport/conduit and metadata excluded.
/// </summary>
public static class EventKey
{
    private static readonly JsonSerializerSettings Canonical = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    public static string Compute(WebSocketNotificationMessage notification)
    {
        var sub = notification.Payload?.Subscription;
        var type = sub?.Type ?? notification.Metadata?.SubscriptionType ?? "";
        var version = sub?.Version ?? notification.Metadata?.SubscriptionVersion ?? "";
        var conditionJson = JsonConvert.SerializeObject(sub?.Condition, Canonical);
        var eventJson = JsonConvert.SerializeObject(notification.Payload?.Event, Canonical);

        var raw = $"{type}|{version}|{conditionJson}|{eventJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
