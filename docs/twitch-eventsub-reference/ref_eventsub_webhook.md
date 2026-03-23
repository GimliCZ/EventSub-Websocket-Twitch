---
name: Twitch EventSub Webhook Handling
description: Webhook setup, required headers, signature verification, challenge response, revocation, retry logic, and constraints
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub/handling-webhook-events/

## Requirements
- Callback must use **SSL on port 443**
- Secret: ASCII string, **10–100 characters**, use cryptographic random

## Request Headers (every request)
| Header | Purpose |
|--------|---------|
| `Twitch-Eventsub-Message-Id` | Unique message identifier (use for dedup) |
| `Twitch-Eventsub-Message-Retry` | Retry attempt counter |
| `Twitch-Eventsub-Message-Type` | `notification` / `webhook_callback_verification` / `revocation` |
| `Twitch-Eventsub-Message-Signature` | `sha256=<hmac>` — must be verified |
| `Twitch-Eventsub-Message-Timestamp` | RFC3339 with nanosecond precision |
| `Twitch-Eventsub-Subscription-Type` | Subscription type string |
| `Twitch-Eventsub-Subscription-Version` | Schema version |

## Signature Verification (mandatory before processing)
1. Concatenate: `message_id + timestamp + raw_body` (exact order)
2. HMAC-SHA256 using your secret as key
3. Compare to `Twitch-Eventsub-Message-Signature` using **time-safe comparison**
4. Reject with 4xx if mismatch

## Message Types & Responses
| Type | Action | Response |
|------|--------|----------|
| `webhook_callback_verification` | Return raw challenge string | HTTP 200, `Content-Type: text/plain` |
| `notification` | Process event | HTTP 204 |
| `revocation` | Log/handle | HTTP 2xx |

## Duplicate Handling
- Delivery is **at-least-once** — deduplicate on `Twitch-Eventsub-Message-Id`
- Replay window: reject if `message_timestamp` older than 10 minutes

## Revocation Reasons
- `user_removed` — subject no longer exists
- `authorization_revoked` — token revoked or password changed
- `notification_failures_exceeded` — handler timed out too often
- `version_removed` — subscription type/version no longer supported

## Processing Notes
- Respond within a few seconds or face timeout
- Do async work **after** responding (write to temp storage first if needed)
- Repeated timeouts → subscription revocation

## Local Testing
```
twitch event verify-subscription subscribe -F http://localhost:8080/eventsub/ -s [SECRET]
twitch event trigger subscribe -F http://localhost:8080/eventsub/ -s [SECRET]
```
