# Spec B — Conduit Redundancy, Shard Recovery & Cross-Conduit Dedup

**Date:** 2026-06-03
**Branch:** `develop/conduit`
**Status:** design, awaiting review. No code changed yet.
**Builds on:** Spec A (`2026-05-31-conduit-correctness-spec-a-design.md`, implemented, 155/155 green, live-verified). Spec A left these explicitly for Spec B; this is that work.

## 1. Purpose

Make the conduit transport resilient and redundant, the reason conduits exist:

- **Multi-conduit (B1):** `ConduitOrchestrator` owns N independent conduit replicas instead of one.
- **Shard recovery (B4):** subscribe to `conduit.shard.disabled`, route it to the orchestrator, reactivate the disabled shard with a fresh session.
- **Redundancy (B2):** `RedundancyFactor` (1–3) runs the same subscriptions across N conduits via N user instances per identity, so losing a whole conduit still delivers.
- **Cross-conduit dedup (B3):** collapse the N redundant copies of one event to a single user-facing delivery.

Authoritative facts (Twitch docs, in `docs/twitch-eventsub-reference/`):
- Up to **5** conduits per client; up to **3** subscriptions per identical type+condition → redundancy factor caps at **3**.
- Delivery is **at-least-once**; dedup on `message_id`. Redundant copies via different conduits are **different subscriptions** → **different `message_id`s**, so cross-conduit dedup needs an event-content key, not `message_id`.
- `conduit.shard.disabled` (v1) condition = `client_id` (+ `conduit_id`); reactivate by PATCHing the shard with a new `session_id`.
- Subscriptions are conduit-scoped (app token, `{method:"conduit", conduit_id}`); each replica is a full independent copy.

## 2. Topology (decided)

Per-conduit replicas; user replicated across conduits.

```
identity X, RedundancyFactor = 2:
  conduit A (replica 0): shard A1 ← userX#0 ; subs(X) created with conduit_id = A
  conduit B (replica 1): shard B1 ← userX#1 ; subs(X) created with conduit_id = B
  real event → arrives via A and via B → shared dedup gate keeps exactly 1
  conduit A fully down → B still delivers (degraded, not lost)
```

## 3. Ownership chain (extends Spec A)

```
EventSubClient (library)
  └─ ConduitOrchestrator (owns IReadOnlyList<ConduitReplica>; single writer to conduit API)
       ConduitReplica { Index, ConduitId, ShardMap, TwitchShardCount }   ← Spec A's single-conduit state, ×N
       └─ ShardManager (allocates shards per replica)
            └─ EventProvider (one per identity; holds N UserSequencers)
                 └─ UserSequencer #i bound to replica i's shard
                      └─ SubscriptionManager (subs on replica i's conduit_id)
                           └─ MessagePipeline → shared two-layer dedup → one user-facing delivery
```

## 4. Components & changes

### Phase B1 — Multi-conduit orchestration

**`API/ConduitReplica.cs` (new).** Holds per-replica state currently inline in `ConduitOrchestrator`:
```
internal sealed class ConduitReplica
{
    public int Index { get; }
    public string ConduitId { get; set; }
    public Dictionary<string,(string TwitchIndex, string SessionId)> ShardMap { get; } = new();
    public int TwitchShardCount { get; set; }
}
```
The shard-slot math (expand on add; swap-last + scale-down on remove) moves here as instance methods operating on this replica's map, so the orchestrator just dispatches to the owning replica.

**`ConduitOrchestrator` (reshaped).**
- Field: `private readonly List<ConduitReplica> _replicas = new();` exposed as `IReadOnlyList<ConduitReplica>`. Single `SemaphoreSlim` continues to serialize all conduit-API writes (preserves Spec A single-writer property).
- `InitializeAsync`: ensure exactly `RedundancyFactor` conduits exist. Reuse existing conduits by id (up to N); create the shortfall; if existing count > `MaxConduits` throw. **Orphan reconciliation (fixes audit #11):** for each reused conduit, call `GetAllConduitGetShardsAsync` and rebuild `ShardMap`/`TwitchShardCount` from Twitch's actual shard list instead of blindly resetting to 1. Then create the `conduit.shard.disabled` subscription on each replica (see B4).
- Shard ops become replica-addressed: `AddShardAsync(int replicaIndex, string shardId, string sessionId, ct)`, `UpdateShardAsync(int replicaIndex, …)`, `RemoveShardAsync(int replicaIndex, string shardId, ct)`. Each resolves `_replicas[replicaIndex]` and applies the slot math there.
- `TeardownAsync`: delete all N conduits; clear replicas.
- New: `HandleShardDisabledAsync(string conduitId, string shardId, CancellationToken ct)` (B4).

**`IConduitOrchestrator`.** Replace `string ConduitId { get; }` with `IReadOnlyList<string> ConduitIds { get; }` and `string ConduitIdAt(int replicaIndex)`. Shard methods gain the `int replicaIndex` parameter. (All callers updated; `EventSubClient.OnShardSessionIdUpdated` passes the replica index from the `ShardManager` event.)

**`ShardManager` (per-replica).** Add a replica dimension so shard→conduit ownership is explicit:
- `GetOrCreateShardForUserAsync(string userId, int replicaIndex, CancellationToken ct)` — shards are pooled within a replica, never across.
- `SessionIdUpdatedArgs` gains `int ReplicaIndex`; `OnSessionIdUpdated` carries it so the orchestrator routes to the right replica.
- `ReleaseUserFromShardAsync(string userId, int replicaIndex, ct)`.

### Phase B4 — Shard health & recovery

**`SubsRegister/Register.cs` fix.** `RegConduitShardDisabled` condition must be `ClientId` + `ConduitId` (currently only `ConduitId`). **`CreateSubscriptionRequestExtension.SetSubscriptionType`** must set both `Condition.ClientId` and `Condition.ConduitId` for this type — today it maps `ConduitId → condition.ConduitId = userId` (wrong value) and never sets ClientId. Add a code path so the orchestrator can build this subscription with the real client id + conduit id (not a user id).

**`MessagePipeline` (platform routing).** Add `void RegisterPlatformHandler(Func<WebSocketNotificationMessage, Task> handler)`. Detection rule is **type-based and unambiguous**: if `notification.Payload.Subscription.Type == "conduit.shard.disabled"`, route to the platform handler (if registered) and do not user-route. Everything else routes by condition as today. (Type-based avoids guessing from condition shape and keeps the rule a single string compare.)

**`ConduitOrchestrator.HandleShardDisabledAsync`.** Registered as the platform handler. On event: find replica by `conduit_id`; ask its `ShardManager` to open a fresh shard session; PATCH the disabled slot (`UpdateConduitShardSessionAsync`) with the new session to reactivate (Twitch's documented recovery). Log precisely. Failures retry via the existing resilience pipeline.

**Bootstrap.** Orchestrator creates the `conduit.shard.disabled` subscription on each replica during `InitializeAsync`, using the app token and `{client_id, conduit_id}` condition.

### Phase B2 — Redundancy wiring

**`EventSubClientOptions`.** Add `[Range(1,3)] public int RedundancyFactor { get; set; } = 1;`. **Validation (fail-fast):** a `ValidateOnStart`/`IValidateOptions` check requires `RedundancyFactor <= MaxConduits` (and MaxConduits ≤ 5); throw `OptionsValidationException` otherwise, before any network call.

**`EventProvider` (N instances per identity).** Today holds one `UserSequencer`. Change to hold `RedundancyFactor` sequencers:
- On create: build N `UserSequencer`s; the i-th gets a shard binding from `ShardManager.GetOrCreateShardForUserAsync(userId, i, ct)` and a `SubscriptionManager` targeting `ConduitOrchestrator.ConduitIdAt(i)`.
- Register all N with the `MessagePipeline` (same user-facing handler), so each replica's stream feeds the shared dedup gate.
- `IsConnected` = any replica connected. `StartAsync`/`StopAsync` fan out to all N. `DeleteUserAsync` releases all N shards (each via its replica index). Recovery timer (allowRecovery) operates per-sequencer.
- Token refresh: one refresh updates the token for all N sequencers of the identity.

**`EventSubClient.AddUserAsync`** unchanged signature; internally drives the N-instance creation through `EventProvider`.

### Phase B3 — Two-layer dedup

**`CoreFunctions/EventKey.cs` (new).** `static string Compute(WebSocketNotificationMessage)` = stable hash of `subscription_type | version | condition (canonical json) | event (canonical json)`. Canonical = Newtonsoft serialize with sorted keys; hash = SHA256 hex. Identical real events across conduits → identical key; distinct events → distinct key.

**`ReplayProtection` (extended in place).** Add `bool IsDuplicateEvent(string eventKey)` mirroring `IsDuplicate(messageId)` — same eviction/window logic, separate window, both sized by `DedupWindowSize`. Thread-safe like the existing layer.

**`UserSequencer.ProcessWebSocketMessageAsync` (notification path only).** After the existing message_id + timestamp gate, for notifications also compute `EventKey.Compute(notification)` and check `IsDuplicateEvent`; if seen, drop (this is the redundant cross-conduit copy). Raw callback still fires for every frame before any dedup (Spec A behavior preserved). Non-notification frames are not event-keyed.

## 5. Data flow (N=2)

```
startup: ensure 2 conduits [A,B]; reconcile orphan shards; create shard.disabled sub on each
AddUserAsync(X): userX#0→shard on A, subs(X) conduit_id=A ; userX#1→shard on B, subs(X) conduit_id=B

event for X:
  via A: ShardInbound → pipeline → userX#0 → raw fires; msgId(A) new; eventKey new → DELIVER
  via B: ShardInbound → pipeline → userX#1 → raw fires; msgId(B) new; eventKey SEEN → DROP
  ⇒ consumer sees exactly one typed On*EventAsync

conduit.shard.disabled(conduit=A, shard=k):
  pipeline platform-route → Orchestrator.HandleShardDisabledAsync
  → ShardManager opens fresh session on A → PATCH slot k → reactivated (B unaffected)
```

## 6. Error handling
- Startup replica shortfall that can't be met → fail fast; teardown any conduits created this run.
- One replica's user instance failing (token/handshake) degrades redundancy to N−1; logged; other replicas keep delivering. Per-replica all-or-nothing handshake retained (Spec A decision).
- Recovery PATCH failure → resilience-pipeline retry; persistent failure logged; 72h all-disabled → Twitch auto-deletes that conduit → re-created next init.
- Dedup gate is the final safety net: even double delivery collapses by event-key.
- Raw-message callback fires regardless of dedup (unchanged).

## 7. Testing (offline-heavy + live smoke at N=1 and N=2)

**Logic (xUnit):** `ConduitReplica` shard math (add/expand, swap-last/scale-down); orchestrator dispatches shard ops to the owning replica; orphan reconciliation rebuilds maps from a faked `GetAllConduitGetShardsAsync`; `EventKey.Compute` stability (same event → same key) + distinctness (different event/condition → different key); `ReplayProtection.IsDuplicateEvent`; options validation (RedundancyFactor ≤ MaxConduits; >3 rejected); `conduit.shard.disabled` condition built with client_id+conduit_id.

**Synthetic (fakes, no network):** 2 replicas + 1 identity; same event pushed through both replica streams → exactly one user-facing delivery, raw fired twice (once per replica) but typed event once; `conduit.shard.disabled` faked → orchestrator issues a reactivation PATCH against a fake conduit API (assert the PATCH args).

**Fuzz (seeded):** random interleavings across N replicas/users of {distinct events, cross-conduit duplicates, control frames, ghost-user notifs} → invariants: each distinct event delivered exactly once; no cross-identity leak; per-shard order preserved; dedup windows bounded by `DedupWindowSize`; no unhandled exception. Seed printed on failure.

**Live (harness):** `RedundancyFactor=1` run = regression (must match Spec A: events deliver, raw fires, clean teardown). `RedundancyFactor=2` run = observe in the log a redundant copy being dropped by the event-key layer (add a debug log line "deduped redundant cross-conduit copy {eventKey}"); confirm teardown deletes both conduits. Harness gains a `RedundancyFactor` knob (env or arg).

All via `dotnet test`; existing 155 stay green.

## 8. Build sequence (phased, each green)
1. **B1a:** `ConduitReplica` + move shard math (+ logic tests). Orchestrator still N=1 internally.
2. **B1b:** orchestrator holds list; `InitializeAsync` ensures N + orphan reconcile; `IConduitOrchestrator` ConduitIds; ShardManager replica dimension; update callers + Phase4/Phase6 tests.
3. **B4:** Register/SetSubscriptionType fix; pipeline platform handler; `HandleShardDisabledAsync`; bootstrap subscription (+ logic/synthetic tests). Valuable even at N=1.
4. **B2:** `RedundancyFactor` + validation; `EventProvider` N instances; EventSubClient wiring (+ tests).
5. **B3:** `EventKey` + `ReplayProtection.IsDuplicateEvent` + UserSequencer notification dedup (+ logic/synthetic/fuzz).
6. Full `dotnet test` green; live smoke N=1 then N=2.

## 9. Out of scope
- Webhook shards (websocket only).
- Dynamic RedundancyFactor change at runtime (set at startup).
- Per-type explicit dedup id extractors (full-payload hash chosen; revisit only if false-collapse observed).
- Cross-process/distributed dedup (single-process gate only).

## 10. Risks
- **Event-key false collapse:** two genuinely distinct events with byte-identical type+condition+event payload within the window would dedup to one. Realistically only repeatable-state events; accepted (hybrid extractors are the fallback if observed). Documented.
- **Constructor/interface blast radius:** `IConduitOrchestrator`, `ShardManager`, `SessionIdUpdatedArgs`, `EventProvider`, `EventSubClient` all change; Phase2/4/6/7 tests updated alongside.
- **Orphan reconciliation** depends on `GetAllConduitGetShardsAsync` correctness (Spec A didn't exercise it heavily) — covered by logic tests with a faked shard list.
- **Live N=2 doubles conduit usage** on the test client (2 of 5) — within limits.
