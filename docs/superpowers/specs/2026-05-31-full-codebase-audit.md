# Full Codebase Audit — develop/conduit

**Date:** 2026-05-31
**Scope:** Every behavioral source file in `Twitch EventSub library` + harness, read manually; interactions traced. No code changed (audit only).
**Baseline:** Builds clean, 142/142 unit tests pass, conduit transport verified live end-to-end (channel.update, stream.online/offline delivered + routed).

Findings are ranked by severity. Each notes the file, the interaction, and impact.

---

## STATUS UPDATE (2026-05-31, after Spec A implementation — uncommitted, 155/155 tests green)

Spec A (`docs/superpowers/specs/2026-05-31-conduit-correctness-spec-a-design.md`, plan `…/plans/2026-05-31-conduit-correctness-spec-a.md`) implemented these:
- **#1 multi-user sub deletion — FIXED.** `SubscriptionManager.OwnedSlice` + `RunCheckAsync` now reconciles only the user's condition slice. Exact usage via `ReconcileReport`/`LastReport`.
- **#2 raw messages — FIXED.** `ShardInbound(Raw,Parsed)` carries the JSON shard→user; `UserSequencer.ProcessWebSocketMessageAsync` fires `OnRawMessageAsync` for every frame. (Live `raw:` re-verify still pending a harness run.)
- **#3 pagination — FIXED.** `SubscriptionPagination.Cursor` + cursor-driven loop.
- **#4 keepalive config — FIXED.** `ShardSequencer.NegotiatedKeepaliveSeconds`; user arms watchdog from negotiated/`KeepaliveTimeoutSeconds`. Added `DedupWindowSize`.
- **#5 ordered processing — FIXED.** `.Concat()` on both shard-internal handling and the new `MessagePipeline`.
- **#8 header races — FIXED.** `TwitchApi` + `TwitchApiConduit` use per-request `HttpRequestMessage`.
- **#9 EventRouter dead code — REMOVED.** Replaced by `MessagePipeline` (the real production router; `ShardBinding` is now lifecycle-only).
- Shared single dedup gate via DI `ReplayProtection` sized by `DedupWindowSize`.
- New suites: logic (Phase7), synthetic (`SyntheticScenarioTests`), fuzz (`FuzzPipelineTests`).

**Retracted earlier claims:** #10 watchdog (byte-identical to master — correct), and the "must downcast to EventProvider" item (`IEventProvider` already exposes all events).

**Deferred to Spec B:** #6 `conduit.shard.disabled` + shard recovery; multi-conduit redundancy + N user instances; event-key (cross-conduit) dedup. #7 all-or-nothing handshake kept by design (per owner).

## SPEC B IMPLEMENTED (2026-06-03 — uncommitted, 180/180 tests green)
Spec `2026-06-03-conduit-redundancy-spec-b-design.md`, plan `…/plans/2026-06-03-conduit-redundancy-spec-b.md`, executed subagent-driven (14 tasks), no commits.
- **#6 shard.disabled + recovery — IMPLEMENTED.** `conduit.shard.disabled` sub created per replica (condition client_id+conduit_id, fixed Register + new `SetConduitShardDisabled` builder); `MessagePipeline.RegisterPlatformHandler` type-routes it to `ConduitOrchestrator.HandleShardDisabledAsync`, which opens a fresh session (via `IShardManager.OpenReplacementShardAsync` seam) and PATCHes the disabled slot.
- **Multi-conduit redundancy — IMPLEMENTED.** `ConduitOrchestrator` owns N `ConduitReplica`s (replica-addressed shard ops; `ConduitIds`/`ConduitIdAt`); `ShardManager` allocates per replica (`SessionIdUpdatedArgs.ReplicaIndex`); `EventProvider` runs N `UserSequencer`s per identity (one per replica, each on its own conduit id), one processing path (sequencer[0] forwards), N=1 byte-equivalent to before; `EventSubClientOptions.RedundancyFactor` (1–3, validated ≤ MaxConduits).
- **Event-key dedup — IMPLEMENTED.** `EventKey.Compute` = SHA256(type|version|condition|event), transport/metadata excluded; `ReplayProtection.IsDuplicateEvent` (second independent window); UserSequencer notification path drops redundant cross-conduit copies.
- **Orphan reconcile (audit #11) — FIXED.** Reuse path rebuilds shard count from `GetAllConduitGetShardsAsync` instead of resetting to 1 (`ConduitReplica._nextSlotBase`).
- Tests: Phase8Tests/ (logic, synthetic, fuzz). Harness gained `HARNESS_REDUNDANCY` knob.
- NOT YET DONE: live smoke at N=1 (regression) and N=2 (observe "deduped redundant cross-conduit copy"). Known limitation: a recovered replacement shard lingers (zero-user) until ShardManager dispose — documented, acceptable.
- Pre-existing gap noted: the `AddTwitchEventSub(Action<IServiceProvider,EventSubClientOptions>)` overload has no options validation chain at all (separate from Spec B).

## SPEC B POST-IMPLEMENTATION AUDIT (2026-06-03, manual code read of the actual diffs)
- **SEV-1 FOUND + FIXED: replicas deleted each other's subscriptions.** `SubscriptionManager.OwnedSlice(all,userId)` scoped by condition only; under redundancy the same user+condition sub exists on every conduit, so replica i's `RunCheckAsync(conduitId=i)` saw replica j's copies in its slice and the cleanup loop (`Transport.ConduitId != conduitId → unsubscribe`) deleted them — replicas thrash, mutually destroying subs each cycle. Fuzz/synthetic tests missed it (they drove the dedup gate directly, not RunCheckAsync over a multi-conduit pool); a live N=2 run would have hit it immediately. FIX: added conduit-scoped overload `OwnedSlice(all,userId,conduitId)` (= condition match AND `Transport.ConduitId==conduitId`); `RunCheckAsync` now uses it for the cleanup loop, the extra/missing diff, and the owned-count. New test `SubscriptionScopingTests.OwnedSlice_WithConduitId_ExcludesOtherConduitsCopies`. 181/181 green.
- **SEV-2 FIXED (2026-06-03): double slot assignment on shard recovery.** Was: `OpenReplacementShardAsync`'s `OnSessionAssigned → NotifySessionIdUpdated` fired with OldSessionId==null on the replacement's first welcome → `EventSubClient` mapped it to `AddShardAsync` → a NEW slot + shard_count growth, while `HandleShardDisabledAsync` separately PATCHed the disabled slot — one session on two slots. FIX: the recovery opener now SUPPRESSES the first welcome (`if (old == null) return;`) so it raises no Add; `HandleShardDisabledAsync` is the sole conduit writer and PATCHes the existing disabled slot once (verified: exactly one UpdateConduitShardSessionAsync, zero UpdateConduitShardCountAsync via `ShardRecoveryNoDoubleSlotTests.HandleShardDisabled_DoesNotExpandShardCount_NoSecondSlot`). New `OpenReplacementShardDetailedAsync` returns (session, internalShardId); `ConduitReplica.RebindSlot` maps the disabled twitch slot → the new internal shard, so SUBSEQUENT reconnects forward as an Update of that same slot (never a new Add). `ShardContext.IsRecovery` flag added. 187/187 green. NOTE: still test-covered only — include in the next live recovery smoke.
- Lingering-shard: the replacement shard IS tracked in `_shards` (disposed at ShardManager.DisposeAsync — not leaked). The OLD disabled shard is not eagerly reaped by the recovery path (ShardManager indexes by internal id, not twitch slot id) — minor, documented; Twitch closes the disabled WS anyway so it follows the normal close path.

---

## Corrections to earlier claims

- **RETRACTED — "must downcast to EventProvider":** `IEventProvider` (Interfaces/IEventProvider.cs lines 41–196) **does** expose every `On*EventAsync` event. The harness downcast is unnecessary; `client[userId]` typed as `IEventProvider` is enough. Not an API gap.
- **RETRACTED — "allowRecovery:false retry loop":** measurement artifact of my monitor scripts (fresh dedup set per poll re-printed old lines). Real runs show one lifecycle. Not a bug.

---

## SEV-1 — Functional bugs (break real scenarios)

### 1. Multi-user on one conduit: each user deletes the others' subscriptions
**Files:** `User/SubscriptionManager.cs` RunCheckAsync (43–142); driven per-user from `UserSequencer.RunHandshakeAsync`/`RunManagerAsync`.
**Interaction:** `RunCheckAsync` calls `GetAllSubscriptionsAsync` (returns *all* subscriptions for the client_id — every user, every broadcaster on the conduit), then computes `extraSubscriptions = active where no requested sub has matching Type+Version` and **unsubscribes them**. A user only knows *its own* `RequestedSubscriptions`. So if user A requests `channel.update` and user B requests `channel.follow`, when A's 30-min check runs it sees B's `channel.follow` as "extra" and **deletes it**. Matching is by Type+Version only — broadcaster/user condition is ignored.
**Impact:** Conduit is designed for many users on shared shards (MaxUsersPerShard=1000), but two users with differing sub-type sets actively tear down each other's subscriptions every check cycle. Single-user (the live test) never exposed this. **This makes multi-user conduit — the whole point of the rewrite — non-functional.**
**Fix direction:** Filter the conduit's subscription list by this user's condition (broadcaster_user_id/user_id) before computing extras, or centralize subscription reconciliation at the client level instead of per-user.

### 2. `OnRawMessageAsync` never fires in conduit mode (raw string discarded)
**Files:** `CoreFunctions/ShardSequencer.cs` (DriveFromMessageAsync 172–205, SubscribeToClient 212+), `User/UserSequencer.cs` (ProcessWebSocketMessageAsync 425, the live handler; ParseWebSocketMessageAsync 436 the dead one).
**Interaction:** The shard deserializes `msg.Text` → publishes only the typed `WebSocketMessage` to `_messages`; the raw JSON string is dropped at the shard. The user's live path `ProcessWebSocketMessageAsync` never calls `OnRawMessageRecievedAsync` (only the unreachable socket-era `ParseWebSocketMessageAsync` did — sole caller, line 442).
**Impact:** `OnRawMessageAsync` is dead for all consumers. (Protocol ping/pong is internal to Websocket.Client and never a text frame, so that subset would never surface regardless — but keepalive/notification/welcome/reconnect should.)
**Fix direction:** Carry the raw string alongside the parsed message from shard → user (e.g. a small record/tuple on the Subject), and invoke the raw callback in `ProcessWebSocketMessageAsync`.

### 3. Subscription-list pagination is broken (only first page read)
**File:** `API/Models/GetSubscriptionsResponse.cs` (Cursor 23–24, Pagination 20–21); consumed by `TwitchApi.GetAllSubscriptionsAsync` (168–203).
**Interaction:** Twitch returns the paging cursor at `pagination.cursor`, but the model binds a top-level `cursor` (which Twitch does not send) and types `pagination` as opaque `object`. So `response.Cursor` is always null → the pagination loop breaks after page 1. The loop also seeds `totalPossibleIterations = response.Total` (total subscription count, used as an iteration cap) which is a confused metric but moot since the cursor never advances.
**Impact:** Any client_id with >1 page (~100+) of subscriptions silently processes only the first page. RunCheckAsync then thinks the rest are missing and re-creates duplicates, or fails to clean up. Latent until subscription count grows (multi-user makes this fast).
**Fix direction:** Bind `pagination.cursor` (mirror the working `ConduitPagination` model); drive the loop purely off cursor presence.

### 4. Conduit-mode keepalive timeout is hardcoded; config silently ignored
**Files:** `User/UserSequencer.cs` (`_keepAliveMs = 10_100` line 41; AwaitWelcomeMessageAsync short-circuit), `EventSubClientOptions.KeepaliveTimeoutSeconds`, `ShardManager.BuildWebSocketUri`.
**Interaction:** The shard requests `keepalive_timeout_seconds=N` in the WS URL and the welcome echoes it, but the welcome is consumed by the shard before the user subscribes (Subject doesn't replay), so `WelcomeMessageProcessingAsync` (which would set `_keepAliveMs` from the negotiated value) never runs for conduit users. The watchdog always arms at 10,100 ms.
**Impact:** If an operator sets `KeepaliveTimeoutSeconds > 10`, the user-side watchdog fires early every cycle → spurious `ReconnectFromWatchdog`. The option is silently non-functional. Safe only at the default 10 s (and even then only a 100 ms margin).
**Fix direction:** Have `ShardSequencer` capture the welcome's `keepalive_timeout_seconds` and expose it; user arms its watchdog from that (or from options).

---

## SEV-2 — Robustness / correctness under load

### 5. Rx message handling is fire-and-forget → out-of-order processing
**Files:** `ShardSequencer.SubscribeToClient` (`client.MessageReceived.Subscribe(async msg => …)` 214) and `UserSequencer.SetShardBinding` (`_shardBinding.UserMessages.Subscribe(async msg => …)` 92).
**Interaction:** Both use `Subscribe(async …)` — the async lambda is *not* awaited by Rx, so concurrent frames run their continuations interleaved. The deleted master code deliberately serialized with `.Select(x => Observable.FromAsync(...)).Concat()`. The state machine (welcome→active, reconnect) and ReplayProtection assume ordered, non-reentrant delivery.
**Impact:** Under burst load, `_messages.OnNext` can publish out of order, and the shard FSM can take a reconnect trigger while a welcome continuation is mid-flight. Rare at low volume (didn't surface live), real at scale.
**Fix direction:** Restore `Observable.FromAsync(...).Concat()` serialization on both subscriptions.

### 6. `conduit.shard.disabled` is dropped; no shard-recovery logic
**Files:** `ShardBinding.IsForUser` (now broadcasts non-notifications but filters notifications by broadcaster/user id), `Register.RegConduitShardDisabled` condition = `ConduitId` only.
**Interaction:** The disabled-shard notification's condition carries `conduit_id`, not a broadcaster/user id, so `IsForUser` returns false → no user receives it, `OnConduitShardDisabledEventAsync` never fires, and nothing provisions a replacement shard.
**Impact:** When Twitch disables a shard (the documented failure mode conduits exist to survive), the library neither surfaces it nor recovers. Conduit resilience is unimplemented. (Was explicitly out of Phase-6 scope — flagging as the largest missing *feature*.)
**Fix direction:** Route platform/conduit-scoped notifications to `ConduitOrchestrator` (a "category C" path), and add replacement-shard provisioning.

### 7. All-or-nothing handshake
**File:** `SubscriptionManager.RunCheckAsync` returns false on the first failed subscribe → `UserSequencer` HandShakeFail → user disposed.
**Interaction/Impact:** One bad subscription (e.g. one missing scope, as seen with `channel.chat.message` needing `user:bot`) kills the entire user and all its other (valid) subscriptions. Observed live. Arguably by design, but brittle for mixed sub sets.
**Fix direction:** Consider tolerating partial success (subscribe what's valid, surface the failures) or validating scopes up front.

### 8. `TwitchApi` mutates shared HttpClient default headers per call
**File:** `API/TwitchApi.cs` every method does `httpClient.DefaultRequestHeaders.Authorization = …; .Add("Client-Id", …)` on a client from `IHttpClientFactory`. Same in `TwitchApiConduit`.
**Interaction:** `AddHttpClient` pools `HttpClientHandler`s; the `HttpClient` objects are short-lived but `.Add("Client-Id", …)` appends without clearing — and concurrent calls on the same named client race on `DefaultRequestHeaders`. With many users hitting the API in parallel (multi-user), headers can interleave or duplicate-add throw (`Client-Id already added`).
**Impact:** Latent header races / `InvalidOperationException` under concurrency. Single-user never hit it.
**Fix direction:** Build per-request `HttpRequestMessage` with its own headers instead of mutating `DefaultRequestHeaders`.

---

## SEV-3 — Latent / cleanup

### 9. `EventRouter` / `IEventRouter` is dead code
DI-registered in `ServiceCollectionExtensions` (109) but consumed by nothing in production (ShardBinding does the routing). Only Phase5 tests use it. Remove or wire intentionally. Carries its own `ReplayProtection(100)` singleton that does nothing.

### 10. Watchdog `Reset()` arms a one-shot, not a period
**File:** `Watchdog.cs` — `Start` uses `new Timer(cb, null, timeout, timeout)` (periodic), but `Reset` calls `Change(_timeout, Timeout.Infinite)` (one-shot). After the first `Reset`, the watchdog no longer re-arms periodically — it fires once and, because `OnTimerElapsed` sets `_isRunning=false` and `Change(Infinite,Infinite)`, won't fire again. In practice each keepalive calls `Reset`, so it mostly works, but the semantics are inconsistent and a missed-reset window behaves differently than `Start` implies.

### 11. ConduitOrchestrator shard-index bookkeeping is fragile
**File:** `API/ConduitOrchestrator.cs` AddShardAsync uses `nextIndex = _shardMap.Count`; RemoveShardAsync swaps last-into-freed and decrements `_twitchShardCount`. Logic is internally consistent under the serializing `_lock`, but: (a) `EventSubClient.OnShardSessionIdUpdated` is `async void` — exceptions there are swallowed by the catch but ordering across rapid add/remove relies entirely on the semaphore; (b) on `InitializeAsync` reuse-path it resets shard_count=1 without reconciling shards that may still exist server-side from a crashed prior run.

### 12. `RunCheckAsync` deletes every subscription older than 1 hour each cycle
**File:** `SubscriptionManager.RunCheckAsync` first loop: `DateTime.UtcNow - CreatedAt > 1h` → unsubscribe + later re-create. Combined with the 30-min manager cadence, every subscription is force-recycled hourly. Probably intentional (guards against staleness) but it doubles API traffic and, with broken pagination (#3) and multi-user (#1), compounds.

### 13. Minor / cosmetic
- `WebSocketNotificationCondition.cs` is an unused legacy type (routing uses `API.Models.Condition`).
- `ParseWebSocketMessageAsync` (UserSequencer 436–) is entirely dead (socket era) — remove with the raw-message fix.
- `MethodProvider`, `StatusProvider` fine; `GetSubscriptionsResponse.Pagination` typed as `object` should be removed once #3 fixed.
- `_keepAliveMs` comment says "10% tolerance" but code adds flat +100 ms.
- Czech/locale build output is fine but noisy; unrelated to code.
- Duplicate transport model: `API.Models.Transport` vs `SharedContents.WebSocketTransport` (one uses string dates, one DateTime?) — two near-identical types, drift risk.

---

## Interaction map (verified)

```
EventSubClient (IHostedService)
  ├─ StartAsync → ConduitOrchestrator.InitializeAsync → ITwitchConduitApi (GET/POST conduits)
  ├─ AddUserAsync → new EventProvider(…, IShardManager)
  │     └─ StartAsync → ShardManager.GetOrCreateShardForUserAsync
  │            ├─ CreateShard → new ShardSequencer
  │            ├─ sub OnSessionAssigned → NotifySessionIdUpdated → EventSubClient.OnShardSessionIdUpdated
  │            │        → ConduitOrchestrator.Add/Update/RemoveShardAsync → ITwitchConduitApi PATCH shards
  │            └─ ConnectShardAsync → ShardSequencer.ConnectAsync (Connect trigger BEFORE Start — race fix)
  │     └─ SetShardBinding → UserSequencer.SetShardBinding (binding survives recovery re-create)
  │            └─ UserMessages.Subscribe → ProcessWebSocketMessageAsync  [#2 no raw, #5 unordered]
  ├─ UserSequencer FSM: InitialAccessTest→Websocket(AwaitShardReady waits for session)
  │     →WellcomeMessage(short-circuits on shard session [#4])→HandShake(RunCheck [#1,#3,#7,#12])→Running→Awaiting
  └─ StopAsync → ConduitOrchestrator.TeardownAsync (DELETE conduit)

ShardSequencer.SubscribeToClient → DriveFromMessageAsync:
   active  → _messages.OnNext + drive FSM (welcome/reconnect)
   pending → only welcome completes reconnect
DisconnectionHappened ≥4000 → HandleCloseCodeAsync (4001 terminate, 4004 force-fresh, else reconnect)

ShardBinding.IsForUser: notifications filtered by broadcaster/user id;
   control msgs (welcome/keepalive/reconnect) + revocation broadcast to all users on shard.
   conduit.shard.disabled (condition=conduit_id) → dropped [#6]
```

## Suggested fix order (when we move from audit to action)
1. **#1 multi-user sub deletion** — without it the conduit rewrite doesn't deliver its core value.
2. **#3 pagination** — silent data loss, compounds #1.
3. **#2 raw message** + **#4 keepalive** — correctness of public API / config.
4. **#5 Rx ordering** + **#8 header races** — concurrency hardening.
5. **#6 shard.disabled recovery** — biggest missing feature.
6. **#9–#13** cleanup.

All findings recorded; no fixes applied. Tests still 142/142 green, nothing committed.
