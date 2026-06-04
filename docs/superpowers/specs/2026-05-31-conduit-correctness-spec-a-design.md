# Spec A — Conduit Correctness Fixes (single-conduit)

**Date:** 2026-05-31
**Branch:** `develop/conduit`
**Status:** design, awaiting review. No code changed yet.
**Companion:** Spec B (later) = N-conduit redundancy + `conduit.shard.disabled` handling + shard recovery + event-key dedup. This spec deliberately stays single-conduit.

## 1. Purpose

The conduit transport works end-to-end for a single user (verified live), but a full codebase audit (`2026-05-31-full-codebase-audit.md`) found correctness bugs that break multi-user, lose data, drop the raw-message API, ignore config, and risk concurrency faults. Spec A fixes those on the single-conduit path and adds three test suites. It does **not** add redundancy.

Authoritative model (from Twitch docs, confirmed in `docs/twitch-eventsub-reference/`):
- Subscriptions are owned by the **conduit** (app token, transport `{method:"conduit", conduit_id}`); there is one flat subscription pool per conduit. A subscription is identified by `id` and targeted by `condition` (`broadcaster_user_id`/`user_id`/`moderator_user_id`).
- A library **user = one unique identity**. It owns the slice of the conduit pool whose condition targets its id.
- Delivery is **at-least-once**; dedup on `message_id`.

## 2. Ownership chain (enforced as call-direction, not a physical rewrite)

```
EventSubClient (library)
  └─ ConduitOrchestrator (conduit: owns shards; single writer to subscription API)
       └─ EventProvider / UserSequencer (user: one unique identity)
            └─ SubscriptionManager (subscriptions: condition-scoped to this user)
                 └─ reconcile + exact usage accounting (management)
                      └─ MessagePipeline → shared dedup → user (messages)
```

Rule: no upward calls, no sibling coupling. Spec A moves the minimum code to honor this; the full physical restructure is not required here.

## 3. Components & changes

### 3.1 `ReplayProtection` — single shared dedup gate
- **Problem:** three instances exist — `UserSequencer` `new ReplayProtection(10)`, `EventRouter` `new ReplayProtection(100)`, and an unused DI singleton `new ReplayProtection(100)`. Nothing shares state.
- **Change:** keep the DI singleton as the **only** gate. Remove the `UserSequencer`-local instance. Inject the singleton into `UserSequencer` (via `EventProvider`) so every user runs the *same* gate instance.
- Window size becomes config: `EventSubClientOptions.DedupWindowSize` (default 100). Constructor keeps taking an int; DI passes the option.
- Keeps `IsDuplicate(messageId)` + `IsUpToDate(timestamp)` semantics unchanged (already thread-safe).
- **Unit:** dedups by message_id across all users; used by per-user processing; depends on nothing.

### 3.2 `MessagePipeline` — new class (`CoreFunctions/MessagePipeline.cs`), messages layer
- **Responsibility:** own ordered delivery + raw-carry + condition routing from a shard to the owning user.
- `Attach(IShardStream shard)` subscribes with `shard.Stream.Select(m => Observable.FromAsync(() => HandleAsync(m))).Concat().Subscribe()` — strict arrival order, non-reentrant (restores the master pattern lost in the conduit rewrite).
- `HandleAsync(ShardInbound)` routes by condition to the registered owning user's handler. (Dedup runs per-user after routing, §3.4, per design decision.)
- Holds a user registry: `RegisterUser(userId, Func<ShardInbound,Task> handler)` / `UnregisterUser(userId)`. Routing key = notification condition `broadcaster_user_id` else `user_id`; control messages (welcome/keepalive/reconnect/revocation) broadcast to all attached users (a shard's connection is shared).
- **Depends on:** shard stream(s), user registry. Not on ReplayProtection (that's per-user).
- **Replaces** the ad-hoc `ShardBinding.UserMessages.Subscribe(async …)` wiring and the dead `EventRouter`.

### 3.3 `ShardSequencer` — carry raw + ordered + expose keepalive (conduit/transport)
- `_messages` Subject type changes `Subject<WebSocketMessage>` → `Subject<ShardInbound>` where
  `record ShardInbound(string Raw, WebSocketMessage Parsed)` (new, `CoreFunctions/ShardInbound.cs`).
- `SubscribeToClient`: **serialize the shard's own message handling** by replacing `client.MessageReceived.Subscribe(async msg => …)` (fire-and-forget; audit #5) with `client.MessageReceived.Select(msg => Observable.FromAsync(() => OnFrameAsync(msg))).Concat().Subscribe()`. `OnFrameAsync` deserializes → builds `ShardInbound(msg.Text, parsed)` → publishes on the Subject → drives the FSM (welcome→Active, reconnect, close≥4000). This guarantees the shard processes frames and advances its state machine strictly in arrival order. The consumer side (MessagePipeline §3.2) ALSO uses `.Concat()`, so ordering is guaranteed on both the producing and consuming ends.
- `DisconnectionHappened` handling likewise serialized / await-correct (close-code path).
- Capture welcome's `keepalive_timeout_seconds` → expose `int? NegotiatedKeepaliveSeconds { get; }`.
- `IShardBinding.UserMessages` becomes `IObservable<ShardInbound>`.

### 3.4 `UserSequencer` — raw callback, dedup, config keepalive (user)
- `ProcessWebSocketMessageAsync(ShardInbound)`:
  1. `await OnRawMessageRecievedAsync(this, inbound.Raw)` — **always**, before any filtering (fix #2; raw fires for welcome/keepalive/notification/reconnect/revocation).
  2. shared `ReplayProtection` gate (message_id + timestamp); drop on duplicate/stale.
  3. typed dispatch (existing switch).
- Delete dead `ParseWebSocketMessageAsync` (sole caller of the old raw hook; socket-era).
- `AwaitWelcomeMessageAsync`: when shard already has a session, arm watchdog from
  `shard.NegotiatedKeepaliveSeconds ?? options.KeepaliveTimeoutSeconds` (×1000 + 100 ms tolerance) instead of hardcoded `10_100` (fix #4). Watchdog class itself unchanged (verified identical to master, correct).
- Remove the `_keepAliveMs = 10_100` magic default in favor of the computed value.

### 3.5 `SubscriptionManager` — condition-scoped reconciliation (subscriptions + management)
- `RunCheckAsync` lists the conduit pool (unchanged call) but **filters to this user's slice** before diffing:
  a sub belongs to this user iff its `condition.BroadcasterUserId == userId` OR `condition.UserId == userId` OR `condition.ModeratorUserId == userId`.
- Extras = (this user's actual slice) not in (this user's requested set). Missing = requested not in slice. **Never deletes subs outside the slice** (fix #1).
- The first-loop "delete anything not on this conduit / not enabled / older than 1h" also restricted to the slice.
- **Exact usage accounting:** expose the per-user owned-slice count + ids via a method/property used in logs and tests; reconcile is purely desired-vs-actual within the slice.
- All-or-nothing on a failed subscribe: **kept** (by design — one failure → `RunCheckAsync` false → HandShakeFail).

### 3.6 `TwitchApi` / `TwitchApiConduit` — per-request headers + cursor (API edge)
- Replace every `httpClient.DefaultRequestHeaders.Authorization = …; .Add("Client-Id", …)` with a per-call `HttpRequestMessage` that sets `Authorization`/`Client-Id` on `request.Headers`, then `SendAsync` (fix #8 — removes shared-header race / `Client-Id already added`).
- **Cursor (fix #3):** `GetSubscriptionsResponse` — replace the bogus top-level `cursor` + opaque `object Pagination` with a typed `Pagination { cursor }` (mirror `ConduitPagination`). `GetAllSubscriptionsAsync` loops while `pagination.cursor` is non-empty; remove the `Total`-based iteration cap.

### 3.7 `EventSubClientOptions` — config (config)
- `KeepaliveTimeoutSeconds` (exists; range 10–600) — now wired to **both** the shard WS URL and the user watchdog.
- Add `DedupWindowSize` (default 100, range ≥1).

### 3.8 Deletions
- `EventRouter.cs`, `IEventRouter.cs`, their DI registration, Phase5 `EventRouterTests` (replace with MessagePipeline tests).
- `UserSequencer.ParseWebSocketMessageAsync` (dead).
- `Messages/NotificationMessage/WebSocketNotificationCondition.cs` (unused legacy; routing uses `API.Models.Condition`).
- The second/third `ReplayProtection` instances.

## 4. Data flow (final)

```
shard socket frame
 → ShardSequencer: parse → ShardInbound(raw, parsed); publish; FSM self-drive
 → MessagePipeline.Attach: .Select(FromAsync(HandleAsync)).Concat()      [ordered by arrival]
      → route by condition → owning UserSequencer handler
 → UserSequencer.ProcessWebSocketMessageAsync:
      → OnRawMessageAsync(raw)            [always, fix #2]
      → shared ReplayProtection gate      [dedup by message_id + timestamp]
      → typed dispatch → EventProvider On*EventAsync
```

Control messages (welcome/keepalive/reconnect/revocation) broadcast to all users on the shard; notifications routed to the one owning identity.

## 5. Error handling
- Pipeline `HandleAsync` wraps per-message work in try/catch, logs, and continues (one bad message never breaks the `.Concat()` chain or the shard).
- Dedup/stale drops are logged at debug, not errors.
- API edge: unchanged status-code handling; `InvalidAccessTokenException` still bubbles to the refresh path.
- Condition-scoping: a notification whose condition matches no registered user is logged and dropped (not an error).

## 6. Testing strategy

All via `dotnet test`; existing 142 stay green. New tests grouped:

### 6.1 Logic tests (deterministic xUnit)
- Per-user condition scoping: user A's `RunCheckAsync` over a pool containing B's sub leaves B's sub untouched; deletes only A's extras.
- Exact usage: owned-slice count equals subs whose condition targets the user.
- Cursor pagination: a 3-page fake response is fully aggregated; single-page still works.
- Raw callback fires for welcome, keepalive, notification, revocation (one assertion each).
- Watchdog armed from configured keepalive (e.g. 30 → 30100 ms), not 10100.
- Dedup: repeat message_id dropped; distinct ids pass; stale timestamp dropped.
- Ordering: messages emitted to a user in the exact order the shard produced them.

### 6.2 Synthetic tests (scripted scenarios, fakes, no network)
- Fake shard emits a recorded sequence: welcome → keepalive×2 → notif(userA) → notif(userB) → reconnect → welcome → notif(userA). Assert: each user gets exactly its slice, in order, deduped; FSM transitions correct; raw fired for every frame.
- Two users on one shard; interleaved notifications; assert no cross-user leakage and correct counts.

### 6.3 Fuzz tests (randomized, seeded)
- Generate random interleavings of {valid notif (random owner), duplicate message_id, malformed JSON, out-of-order arrival, late timestamp, control frames} across M users and K shards.
- Invariants asserted after each run: no cross-user leakage; no message delivered twice (post-dedup); per-shard arrival order preserved; no unhandled exception; dedup window never exceeds `DedupWindowSize`.
- Seed printed on failure for deterministic repro.

## 7. Build sequence (phased, each green)
1. API edge: cursor fix + per-request headers (+ tests). Lowest risk, isolated.
2. Config: `KeepaliveTimeoutSeconds` → watchdog, `DedupWindowSize` (+ options tests).
3. `ShardInbound` + `ShardSequencer`/`IShardBinding` stream type change (+ shard tests updated).
4. `MessagePipeline` + shared dedup; delete `EventRouter` (+ logic/synthetic tests).
5. `UserSequencer`: raw callback + dedup + keepalive wiring; delete dead parse path.
6. `SubscriptionManager`: condition-scoping + exact usage (+ logic tests — the #1 fix).
7. Fuzz suite.
8. Full `dotnet test` green; live re-verify single-user still delivers.

## 8. Out of scope (→ Spec B)
- Multiple conduits / redundancy factor; N user instances per conduit.
- Event-key (cross-conduit) dedup layer — Spec A's gate is message_id-only.
- `conduit.shard.disabled` subscribe + route + shard recovery / reactivation.
- `ConduitOrchestrator` multi-conduit ownership, orphan-shard reconciliation on reuse.

## 9. Risks
- Changing `IShardBinding.UserMessages`/Subject type touches Phase2/Phase3/Phase6 tests — update them in the same phase.
- Condition-scoping must handle subs with only `user_id` (whispers, user.update) and moderator-only conditions — covered by logic tests.
- `.Concat()` serialization must not deadlock on synchronous-completing tasks — use `Observable.FromAsync` (cold) not pre-started tasks.
