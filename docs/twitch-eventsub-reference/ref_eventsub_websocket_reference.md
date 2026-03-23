---
name: Twitch EventSub WebSocket Message Reference
description: Full message format reference for all 7 WebSocket message types, field definitions, close codes, and protocol constraints
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub/websocket-reference/

## Timestamp Format
All timestamps: **RFC3339 with nanosecond precision** (not milliseconds)

## Message Envelope
Every message has:
```json
{
  "metadata": { "message_id", "message_type", "message_timestamp" },
  "payload": { ... }
}
```

## 1. session_welcome
```json
{
  "payload": {
    "session": {
      "id": "<session_id>",
      "status": "connected",
      "keepalive_timeout_seconds": 10,
      "reconnect_url": null,
      "connected_at": "<timestamp>"
    }
  }
}
```
- Use `session.id` when creating subscriptions
- You have **10 seconds** from welcome to create at least one subscription

## 2. session_keepalive
```json
{ "payload": {} }
```
- Sent when no notification within `keepalive_timeout_seconds`

## 3. session_reconnect
```json
{
  "payload": {
    "session": {
      "id": "<session_id>",
      "status": "reconnecting",
      "reconnect_url": "<use-as-is>",
      "keepalive_timeout_seconds": null
    }
  }
}
```
- Connect to `reconnect_url` **without** closing old connection
- Only close old connection after receiving `session_welcome` on the new one
- Subscriptions transfer automatically

## 4. notification
```json
{
  "metadata": {
    "subscription_type": "channel.follow",
    "subscription_version": "2"
  },
  "payload": {
    "subscription": {
      "id", "status", "type", "version", "cost", "condition", "transport", "created_at"
    },
    "event": { ... }
  }
}
```

## 5. revocation
```json
{
  "payload": {
    "subscription": { "status": "authorization_revoked", ... }
  }
}
```
Status values: `authorization_revoked`, `user_removed`, `version_removed`

## 6. Close Codes
| Code | Reason | Reconnect? |
|------|--------|-----------|
| 4000 | Internal server error | Yes |
| 4001 | Client sent inbound traffic (library bug) | No |
| 4002 | Client failed ping-pong | Yes |
| 4003 | Connection unused (no sub in 10s) | Yes |
| 4004 | Reconnect grace time expired (30s window) | Force fresh |
| 4005 | Network timeout | Yes |
| 4006 | Network error | Yes |
| 4007 | Invalid reconnect URL | Yes |

## Protocol Rules
- Only send **pong** frames in response to ping; no other outbound traffic
- Messages delivered **at-least-once**; deduplicate on `message_id`
- Reconnect window: **30 seconds** after receiving `session_reconnect`
