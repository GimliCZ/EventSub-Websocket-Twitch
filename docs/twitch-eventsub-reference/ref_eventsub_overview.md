---
name: Twitch EventSub Overview
description: Core concepts, transport methods, duplicate handling, replay protection, and general constraints from the Twitch EventSub documentation
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub

## Transport Methods
- `websocket` — per-user token, up to 3 connections, 300 subscriptions, cost ≤ 10
- `webhook` — server-to-server HTTP callbacks
- `conduit` — app-level, sharded, high-scale (up to 5 conduits × 20,000 shards)

## Delivery Guarantee
- **At-least-once**: same event may arrive multiple times
- Deduplicate using `message_id`

## Replay Attack Prevention
- Reject events where `message_timestamp` is older than **10 minutes**
- Track seen `message_id` values to drop duplicates

## Subscription Cost Model
- Each subscription has a cost; conduit subscriptions use the same cost model as regular EventSub
- WebSocket: total cost ≤ 10 per connection

## All subscriptions are available to all transports unless explicitly noted as exceptions.
