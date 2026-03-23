---
name: Twitch EventSub Subscription Types
description: Full list of EventSub subscription type categories, conditions, and transport notes from the reference page
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub/eventsub-reference

## Subscription Structure
All subscriptions require:
- `type` (string) — subscription type name
- `version` (string) — typically `"1"`, some have `"2"`
- `condition` (object) — type-specific parameters
- `transport` (object) — `method`, plus `session_id`/`conduit_id`/`callback`

## Subscription Categories

### Automod
- `automod.message.hold` v1, v2
- `automod.message.update` v1, v2
- `automod.settings.update` v1
- `automod.terms.update` v1

### Channel
- `channel.update` v1
- `channel.follow` v2
- `channel.ad_break.begin` v1
- `channel.bits.use` v1
- `channel.ban` / `channel.unban` v1
- `channel.unban_request.create` / `channel.unban_request.resolve` v1
- `channel.moderator.add` / `channel.moderator.remove` v1
- `channel.vip.add` / `channel.vip.remove` v1
- `channel.warning.acknowledge` / `channel.warning.send` v1
- `channel.raid` v1

### Chat
- `channel.chat.clear` / `channel.chat.clear_user_messages` v1
- `channel.chat.message` v1
- `channel.chat.message_delete` v1
- `channel.chat.notification` v1
- `channel.chat.settings.update` v1
- `channel.chat.user_message_hold` / `channel.chat.user_message_update` v1
- `channel.shared_chat.begin` / `channel.shared_chat.update` / `channel.shared_chat.end` v1
- `channel.suspicious_user.message` / `channel.suspicious_user.update` v1

### Subscriptions & Monetization
- `channel.subscribe` / `channel.subscription.end` / `channel.subscription.gift` / `channel.subscription.message` v1
- `channel.cheer` v1
- `channel.points.automatic_reward_redemption.add` v1, v2
- `channel.points.custom_reward.add/update/remove` v1
- `channel.points.custom_reward_redemption.add/update` v1
- `channel.charity_campaign.donate/progress/start/stop` v1

### Polls / Predictions / Hype
- `channel.poll.begin/progress/end` v1
- `channel.prediction.begin/progress/lock/end` v1
- `channel.hype_train.begin/progress/end` v1

### Goals & Shield
- `channel.goal.begin/progress/end` v1
- `channel.shield_mode.begin/end` v1
- `channel.shoutout.create/receive` v1

### Stream
- `stream.online` / `stream.offline` v1

### User
- `user.update` v1
- `user.whisper.message` v1

### Conduit-specific
- `conduit.shard.disabled` v1 — fires when a shard becomes disabled; subscribe to this for shard health monitoring

### Beta (Guest Star)
- `channel.guest_star_session.begin/end` beta
- `channel.guest_star_guest.update` beta
- `channel.guest_star_settings.update` beta

## Conditions (common)
- `broadcaster_user_id` — most channel events
- `user_id` — user-scoped events (whispers, user.update)
- `moderator_user_id` — moderation events
- `reward_id` — optional, narrows channel points to a specific reward
- `client_id` — app-level events

## Notes
- Maximum 1 value per condition field (no bulk subscriptions per request)
- `reward_id` is optional — omitting it subscribes to all rewards
- Webhook-only: `drop.entitlement.grant`, `extension.bits_transaction.create`, `user.authorization.grant/revoke`
