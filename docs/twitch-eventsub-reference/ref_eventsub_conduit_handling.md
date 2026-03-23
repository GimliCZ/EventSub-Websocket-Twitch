---
name: Twitch EventSub Conduit Event Handling
description: Full conduit setup sequence, shard assignment, WebSocket timing, scaling, failure handling, and auto-deletion rule
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub/handling-conduit-events/

## What is a Conduit?
A wrapper that separates subscriptions from underlying transports, load-balancing notifications across shards. Owned by the app (app access token). A shard = one WebSocket session or Webhook callback.

## Setup Sequence (4 steps)
1. **Create conduit** — POST with `shard_count`; shard IDs are sequential starting at 0
2. **Assign transports** — PATCH `/helix/eventsub/conduits/shards` with WebSocket `session_id` or Webhook callback
3. **Verify shards** — for Webhook shards, respond to challenge with HTTP 200 + raw challenge value
4. **Add subscriptions** — POST to subscriptions API using `conduit_id` as transport

## Critical Timing
- After WebSocket Welcome message: **10 seconds** to associate the session with a shard
- Same window applies as regular WebSocket connections

## Scaling
- **Scale up**: PATCH `/helix/eventsub/conduits` with higher `shard_count`
- **Scale down**: PATCH with lower `shard_count` — highest-numbered shards are disabled first (e.g., 100→50 disables shards 50–99)

## Limits (subject to change)
- Max **5** enabled conduits per client
- Max **20,000** shards per conduit
- Standard EventSub cost model applies

## Disabled Shard Behavior
- EventSub retries notification on another shard if target shard is disabled
- If both target and fallback are disabled → notification **dropped**
- Webhook shards: only disabled after extended outage; failed callbacks are NOT retried on another shard

## Handling Disabled Shards
- Subscribe to `conduit.shard.disabled` (v1) for real-time monitoring
- **Reactivate**: PATCH shard with new WebSocket `session_id` or valid Webhook URL
- **Remove**: swap failing shard with last shard via PATCH, then reduce `shard_count` by 1

## Auto-Deletion Rule
- If **all shards disabled** for **72 hours** → Twitch auto-deletes the conduit
- All associated subscriptions are also removed
- Allows recovery from full outages without recreating subscriptions if recovered within 72h
