---
name: Twitch EventSub Managing Subscriptions
description: API endpoints and parameters for creating, listing, filtering, and deleting EventSub subscriptions, plus cost model and limits
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub/manage-subscriptions/

## Create Subscription
POST `https://api.twitch.tv/helix/eventsub/subscriptions`

Required fields:
- `type` — event type string (e.g. `"channel.follow"`)
- `version` — schema version string (e.g. `"2"`)
- `condition` — object with type-specific parameters
- `transport` — `{ method, session_id }` or `{ method, callback, secret }` or `{ method, conduit_id }`

Auth:
- **Webhook** → app access token
- **WebSocket** → user access token
- **Conduit** → app access token

## List Subscriptions
GET `https://api.twitch.tv/helix/eventsub/subscriptions`

Filters (mutually exclusive):
- `?type=channel.follow` — filter by event type
- `?status=enabled` — filter by status

Status values: `enabled`, `webhook_callback_verification_pending`, `webhook_callback_verification_failed`, `notification_failures_exceeded`, `authorization_revoked`, `moderator_removed`, `user_removed`, `version_removed`, `beta_maintenance`, and various `websocket_*` statuses.

Results are **paginated**, sorted by creation date.

## Delete Subscription
DELETE `https://api.twitch.tv/helix/eventsub/subscriptions?id={subscription_id}`

**Important**: Delete all subscriptions before deleting the application.

## Cost Model & Limits
- Max **3** subscriptions per identical type+condition combination
- Free cost: user-authorized subscriptions
- Charged cost: 1 point per user-specified subscription without authorization
- Default `max_total_cost`: **10,000 points**

### WebSocket-specific (per user token)
- Max **3** concurrent connections with enabled subscriptions
- Max **300** enabled subscriptions per connection
- Max **10** total cost across all subscriptions on a connection
