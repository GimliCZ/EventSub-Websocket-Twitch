---
name: Twitch EventSub Conduits
description: Conduit and shard lifecycle, scaling rules, hard limits, failure scenarios, and disabled shard handling
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub (conduit sections)
See also: https://dev.twitch.tv/docs/eventsub/eventsub-reference

## What is a Conduit?
App-level transport that multiplexes EventSub subscriptions across many WebSocket shards or Webhook callbacks. Unlike WebSocket transport (per user token), conduits are owned by the app (client credentials).

## Hard Limits (subject to change)
- Max **5** enabled conduits per client
- Max **20,000** shards per conduit
- Subscriptions use the same cost model as regular EventSub

## Transport Method String
- `"conduit"` — used in subscription transport field

## Shard Transport Methods
Each shard uses one of:
- `"websocket"` — shard bound to a WebSocket session via `session_id`
- `"webhook"` — shard bound to a callback URL

## Scaling
- Scale **up**: PATCH `/helix/eventsub/conduits` with increased `shard_count`
- Scale **down**: PATCH with decreased `shard_count` — shards with highest IDs (e.g., 50–99 when going from 100→50) are disabled
- Assign/reassign shards: PATCH `/helix/eventsub/conduits/shards`

## Shard Removal Pattern (clean scale-down)
1. Swap the shard to remove with the last shard (PATCH shards)
2. Reduce `shard_count` by 1 (PATCH conduit) — drops the vacated last slot

## Failure & Disabled Shards
- EventSub can disable individual shards (e.g., WebSocket disconnect)
- On notification to disabled shard: Twitch retries on another shard; if that is also disabled → notification **dropped**
- Webhook shards: only disabled after extended outage; failed callback is NOT retried on another shard

## Handling Disabled Shards
- Subscribe to `conduit.shard.disabled` to be notified immediately
- To reactivate: PATCH shard with a new WebSocket `session_id` or valid Webhook callback URL
- To remove: swap + scale down (see above)

## Conduit Deletion Rule
- If **all shards are disabled** for **72 hours** → Twitch auto-deletes the conduit
- Deleting a conduit auto-removes all associated subscriptions

## Recommended Subscription
Always subscribe to `conduit.shard.disabled` (v1) to monitor shard health.
