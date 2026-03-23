---
name: Twitch EventSub WebSocket Protocol
description: WebSocket message types, session lifecycle, reconnect flow, keepalive behavior, close codes, and per-connection limits
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub/handling-websocket-events

## WebSocket Endpoint
`wss://eventsub.wss.twitch.tv/ws`

## Message Types
1. **session_welcome** — first message after connect; contains `session_id` required for subscriptions
2. **session_keepalive** — sent when no notification within `keepalive_timeout_seconds`
3. **notification** — event delivery
4. **session_reconnect** — server-initiated reconnect with new URL (30s notice)
5. **revocation** — subscription cancelled with reason code
6. **close** — connection terminated (codes 4000–4007)

## Session Lifecycle
- After receiving `session_welcome`, you have **10 seconds** to create at least one subscription
- Failure to subscribe in time → close code `4003` (connection unused)

## Reconnect Flow
1. Receive `session_reconnect` message containing new URL
2. Connect to new URL **without** closing the old connection
3. Wait for `session_welcome` on new connection
4. Only then close the old connection
- Prevents event loss during server maintenance

## Keepalive
- Configurable: `keepalive_timeout_seconds` range **10–600** (default 10)
- If no notification or keepalive received within the window → assume connection lost

## Close Codes
| Code | Meaning | Action |
|------|---------|--------|
| 4000 | Internal server error | Reconnect |
| 4001 | Client sent inbound traffic | Do NOT reconnect (library bug) |
| 4002 | Client failed ping-pong | Reconnect |
| 4003 | Connection unused (no subs in time) | Reconnect |
| 4004 | Reconnect grace period expired | Force fresh connect |
| 4005 | Network timeout | Reconnect |
| 4006 | Network error | Reconnect |
| 4007 | Invalid reconnect | Reconnect |

## Per-Connection Limits (WebSocket, per user token)
- Max **3** WebSocket connections with enabled subscriptions
- Max **300** enabled subscriptions per connection
- Max **10** total subscription cost per connection
