using Twitch.EventSub.Messages;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// A single inbound shard frame, carrying both the original JSON string (for the raw-message
/// callback) and the parsed message (for routing and FSM handling).
/// </summary>
public sealed record ShardInbound(string Raw, WebSocketMessage Parsed);
