---
name: Twitch EventSub Subscription Types Full List
description: Every subscription type with version, required scope, condition parameters, and transport support
type: reference
---

Source: https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/

## Automod
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `automod.message.hold` | v1, v2 | `moderator:manage:automod` | broadcaster_user_id, moderator_user_id |
| `automod.message.update` | v1, v2 | `moderator:manage:automod` | broadcaster_user_id, moderator_user_id |
| `automod.settings.update` | v1 | `moderator:read:automod_settings` | broadcaster_user_id, moderator_user_id |
| `automod.terms.update` | v1 | `moderator:manage:automod` | broadcaster_user_id, moderator_user_id |

## Channel — Core
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `channel.update` | v2 | none | broadcaster_user_id |
| `channel.follow` | v2 | `moderator:read:followers` | broadcaster_user_id, moderator_user_id |
| `channel.ad_break.begin` | v1 | `channel:read:ads` | broadcaster_user_id |
| `channel.bits.use` | v1 | `bits:read` | broadcaster_user_id |
| `channel.ban` | v1 | `channel:moderate` | broadcaster_user_id |
| `channel.unban` | v1 | `channel:moderate` | broadcaster_user_id |
| `channel.unban_request.create` | v1 | `moderator:read:unban_requests` or `moderator:manage:unban_requests` | broadcaster_user_id, moderator_user_id |
| `channel.unban_request.resolve` | v1 | same as above | broadcaster_user_id, moderator_user_id |
| `channel.moderate` | v1, v2 | `channel:moderate` | broadcaster_user_id |
| `channel.moderator.add` | v1 | `moderation:read` | broadcaster_user_id |
| `channel.moderator.remove` | v1 | `moderation:read` | broadcaster_user_id |
| `channel.vip.add` | v1 | `channel:manage:vips` | broadcaster_user_id |
| `channel.vip.remove` | v1 | `channel:manage:vips` | broadcaster_user_id |
| `channel.warning.send` | v1 | `moderation:read` or `channel:moderate` | broadcaster_user_id |
| `channel.warning.acknowledge` | v1 | same | broadcaster_user_id |
| `channel.raid` | v1 | none | from_broadcaster_user_id OR to_broadcaster_user_id |
| `channel.shield_mode.begin` | v1 | `moderation:read` or `channel:moderate` | broadcaster_user_id |
| `channel.shield_mode.end` | v1 | same | broadcaster_user_id |
| `channel.shoutout.create` | v1 | same | broadcaster_user_id |
| `channel.shoutout.receive` | v1 | same | broadcaster_user_id |

## Channel — Chat
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `channel.chat.clear` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.chat.clear_user_messages` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.chat.message` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.chat.message_delete` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.chat.notification` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.chat_settings.update` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.chat.user_message_hold` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.chat.user_message_update` | v1 | `user:read:chat` | broadcaster_user_id, user_id |
| `channel.shared_chat.begin` | v1 | none | broadcaster_user_id |
| `channel.shared_chat.update` | v1 | none | broadcaster_user_id |
| `channel.shared_chat.end` | v1 | none | broadcaster_user_id |
| `channel.suspicious_user.message` | v1 | none | broadcaster_user_id |
| `channel.suspicious_user.update` | v1 | none | broadcaster_user_id |

## Channel — Subscriptions & Bits
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `channel.subscribe` | v1 | `channel:read:subscriptions` | broadcaster_user_id |
| `channel.subscription.end` | v1 | `channel:read:subscriptions` | broadcaster_user_id |
| `channel.subscription.gift` | v1 | `channel:read:subscriptions` | broadcaster_user_id |
| `channel.subscription.message` | v1 | `channel:read:subscriptions` | broadcaster_user_id |
| `channel.cheer` | v1 | `bits:read` | broadcaster_user_id |

## Channel — Points
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `channel.channel_points_automatic_reward_redemption.add` | v1, v2 | `channel:read:redemptions` | broadcaster_user_id |
| `channel.channel_points_custom_reward.add` | v1 | `channel:manage:redemptions` | broadcaster_user_id |
| `channel.channel_points_custom_reward.update` | v1 | `channel:manage:redemptions` | broadcaster_user_id |
| `channel.channel_points_custom_reward.remove` | v1 | `channel:manage:redemptions` | broadcaster_user_id |
| `channel.channel_points_custom_reward_redemption.add` | v1 | `channel:read:redemptions` | broadcaster_user_id, (optional) reward_id |
| `channel.channel_points_custom_reward_redemption.update` | v1 | `channel:manage:redemptions` | broadcaster_user_id, (optional) reward_id |

## Channel — Polls, Predictions, Hype, Goals, Charity
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `channel.poll.begin/progress/end` | v1 | `channel:read:polls` | broadcaster_user_id |
| `channel.prediction.begin/progress/lock/end` | v1 | `channel:read:predictions` | broadcaster_user_id |
| `channel.hype_train.begin/progress/end` | v2 | `channel:read:hype_train` | broadcaster_user_id |
| `channel.goal.begin/progress/end` | v1 | `channel:read:goals` | broadcaster_user_id |
| `channel.charity_campaign.donate/start/progress/stop` | v1 | none | broadcaster_user_id |

## Stream
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `stream.online` | v1 | `user:read:subscriptions` | broadcaster_user_id |
| `stream.offline` | v1 | none | broadcaster_user_id |

## User
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `user.authorization.grant` | v1 | none | client_id |
| `user.authorization.revoke` | v1 | none | client_id |
| `user.update` | v1 | none | user_id |
| `user.whisper.message` | v1 | `user:read:email` or `whispers:read` | user_id |

## Conduit
| Type | Ver | Scope | Conditions |
|------|-----|-------|-----------|
| `conduit.shard.disabled` | v1 | none | conduit_id |

## Beta — Guest Star
| Type | Ver | Notes |
|------|-----|-------|
| `channel.guest_star_session.begin` | beta | Unstable |
| `channel.guest_star_session.end` | beta | Unstable |
| `channel.guest_star_guest.update` | beta | Unstable |
| `channel.guest_star_settings.update` | beta | Unstable |

## Webhook-only
- `drop.entitlement.grant` v1
- `extension.bits_transaction.create` v1

## Beta Notes
- Beta subscriptions are unstable and may change at any time
- Not for production use
- Available for 30 days after GA release unless otherwise noted
