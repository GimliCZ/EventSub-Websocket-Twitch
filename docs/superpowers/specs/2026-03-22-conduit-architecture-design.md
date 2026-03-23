# Conduit Architecture Design — EventSub WebSocket Twitch Library

**Date:** 2026-03-22
**Branch:** feature/DependencyInjection
**Version target:** v4.0.0

---

## 1. Context & Goal

The library currently operates one WebSocket connection per user (`UserSequencer` owns a `WebsocketClient`). This does not scale and bypasses the resilience benefits of the Twitch Conduit transport.

The goal is to evolve the library so that Conduit is the standard transport. Multiple users share parallel WebSocket shards. Subscriptions are attached to the conduit — not to individual WebSocket sessions. The public API surface (`IEventSubClient`, `IEventProvider`) remains compatible.

---

## 2. Architecture Overview

### Component map

```
IEventSubClient (IAsyncDisposable + IHostedService)
├── ConduitOrchestrator           ← manages conduit via TwitchConduitApi (app access token)
│   └── maps session_id → conduit shard records
├── ShardManager
│   ├── Shard #1  [WebSocket + ShardSequencer state machine]
│   │    └── UserSequencer A, B, C
│   ├── Shard #2  [WebSocket + ShardSequencer state machine]
│   │    └── UserSequencer D, E, F
│   └── ... auto-grows / auto-shrinks
├── EventRouter
│   ├── ReplayProtection (singleton, thread-safe, shared across all shards, ≥ 100 message cache)
│   └── dispatch by broadcaster_user_id → IEventProvider
└── ConcurrentDictionary<userId, EventProvider>
```

### Responsibility boundaries

| Component | Owns | Does NOT own |
|---|---|---|
| `ShardSequencer` | WebSocket lifecycle, close codes, ping→pong, reconnect flow | User identity, tokens, subscriptions |
| `UserSequencer` | Per-user tokens, subscription list, event dispatch state | WebSocket connection |
| `ShardManager` | Shard allocation (full → add, empty → dispose), `session_id` surfacing, user assignment locking | Conduit API calls |
| `ConduitOrchestrator` | Conduit create/reuse/update/delete, mapping `session_id` to conduit shards | WebSocket connections |
| `EventRouter` | Thread-safe message deduplication, routing by `broadcaster_user_id` | Connection management |

### Token model

Conduit transport follows the **app access token model** (same as webhooks), not the user access token model used by standalone WebSocket transport.

| Operation | Token |
|---|---|
| Create / update / delete conduit | App access token |
| Create subscriptions (conduit transport) | App access token — user must have pre-authorized relevant scopes against the client ID, but the API call itself uses the app access token |
| Validate user token (pre-flight check) | User access token (retained in `UserSequencer`) |
| Get subscriptions list | App access token |

`UserSequencer` retains the user access token **only** for pre-flight token validation (`/oauth2/validate`). Subscription creation calls in `SubscriptionManager` use the app access token from `EventSubClientOptions.AppAccessToken`. This is a meaningful change from the standalone WebSocket model where `UserSequencer` supplied the user token to subscription API calls.

### Shard capacity rules

**Important:** The per-connection limits of 300 subscriptions / cost 10 apply to **standalone WebSocket transport with user tokens**. Conduit transport with app access tokens uses different, much higher global limits. The exact conduit-level limits must be verified against the live Twitch Conduit API before implementation of `ShardManager` capacity logic. The architecture assumes shards will be bounded by a configurable `MaxShardsPerConduit` value rather than per-shard subscription counting.

- Conduit subscription cost model uses global app-token limits (order of magnitude: ~10,000), not per-connection limits
- `ShardManager` shard growth is bounded by `MaxShardsPerConduit` (operator-configured ceiling, not a Twitch protocol limit per shard)
- Shard loses all users → `ShardManager` disposes it and notifies `ConduitOrchestrator`

---

## 3. Phase 1 — Protocol Hardening & Options Validation

### 3.1 WebSocket close codes → state machine transitions

Twitch defines 7 application-level close codes (RFC 6455 private-use range 4000–4999). All must map to explicit `ShardSequencer` triggers. Descriptions are from the official Twitch EventSub documentation:

| Code | Meaning (Twitch docs) | Transition / Action |
|---|---|---|
| 4000 | Internal server error | `ServerError` → reconnect with backoff |
| 4001 | Client sent inbound traffic (only pong permitted) | `ClientProtocolViolation` → log critical, do **not** reconnect (code bug) |
| 4002 | Ping-pong failure | `PingPongFailure` → reconnect immediately |
| 4003 | No subscription within 10 s of Welcome | `SubscriptionTimeout` → fire `OnSubscriptionWindowMissed`, reconnect |
| 4004 | Reconnect grace period expired | `ReconnectExpired` → force fresh connect (not reconnect URL) |
| 4005 | Network timeout | `NetworkTimeout` → reconnect with backoff |
| 4006 | Network error | `NetworkError` → reconnect with backoff |
| 4007 | Invalid reconnect | `InvalidReconnect` → reconnect with backoff |

### 3.2 Ping → Pong — Two distinct mechanisms

There are two separate ping/pong mechanisms in the Twitch EventSub WebSocket protocol. They must not be conflated:

**A. WebSocket protocol-level ping frames (RFC 6455 opcode 0x9)**
Handled automatically by `Websocket.Client`. No application code required. Close code 4002 ("ping-pong failure") refers to this protocol-level mechanism.

**B. Twitch application-level `session_ping` JSON message**
Twitch sends a JSON message with `message_type: "session_ping"`. Already implemented in `UserSequencer.PingMessageProcessingAsync()` (`UserSequencer.cs:657`):

```csharp
Socket.Send("Pong");
```

This sends a text frame. The `"Pong"` casing must be confirmed against the Twitch spec during integration testing — the official documentation does not explicitly specify the response payload format.

**Migration note (Phase 2):** Both mechanisms move to `ShardSequencer`. The protocol-level ping requires no code. The application-level pong (`Socket.Send("Pong")`) moves from `UserSequencer` to `ShardSequencer`.

### 3.3 Spec-compliant reconnect flow

The current `UserSequencer.ReconnectMessageProcessingAsync()` mutates `Socket.Url` and calls `Socket.ReconnectOrFail()` on the existing `WebsocketClient` instance, which closes the old connection before confirming a new one. This violates the Twitch spec.

**Correct sequence (per Twitch spec):**
1. Receive `session_reconnect` with `reconnect_url`
2. Open a **new** `WebsocketClient` instance against `reconnect_url` — as-is, unmodified
3. Wait for Welcome on the new connection
4. Only after Welcome received on new connection: dispose the old `WebsocketClient`
5. Surface new `session_id` to `ConduitOrchestrator`

**Implementation requirement:** `ShardSequencer` must hold a `_pendingWebsocketClient` field during the reconnect window alongside the live `_activeWebsocketClient`. The old subscription to `Socket.MessageReceived` and `Socket.DisconnectionHappened` must be disposed (via the `IDisposable` returned by `.Subscribe()`) before the new subscription is created, to prevent duplicate handler accumulation across reconnect cycles.

### 3.4 `EventSubClientOptions` hardening

```csharp
public record EventSubClientOptions
{
    [Required]
    [MinLength(1)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string AppAccessToken { get; set; } = string.Empty;

    [Range(10, 600)]
    public int KeepaliveTimeoutSeconds { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int MaxShardsPerConduit { get; set; } = 10;  // Operator ceiling only — Twitch conduit shard limits are much higher than WebSocket per-user limits; verify exact Twitch limit before setting a hard upper bound

    public TimeSpan WelcomeMessageTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan AccessTokenValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan SubscriptionOperationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan WatchdogTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan ReconnectGraceTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

`[MinLength(1)]` is required alongside `[Required]` because `[Required]` on a `string` only checks for null — `string.Empty` defaults would otherwise pass validation silently.

Registration uses `ValidateOnStart()`:

```csharp
services.AddOptions<EventSubClientOptions>()
    .Configure(configure)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

## 4. Phase 2 — ShardManager & ShardSequencer

### 4.1 ShardSequencer

Owns exactly one active `WebsocketClient` (plus one pending `WebsocketClient` during reconnect). Pure WebSocket lifecycle state machine.

**States:**
```
Disconnected → Connecting → WaitingForWelcome → Active → Reconnecting → Disposing → Disposed
                                                    ↑           ↓
                                              (reconnect loop)
```

**Triggers:**
- `Connect` → `Disconnected` → `Connecting`
- `WelcomeReceived(sessionId)` → `WaitingForWelcome` → `Active`
- `ReconnectRequested(reconnectUrl)` → `Active` → `Reconnecting`
- `NewConnectionWelcomeReceived(newSessionId)` → `Reconnecting` → `Active` (swaps clients, disposes old)
- `CloseCode(code)` → per Phase 1 table
- `Dispose` → any state → `Disposing` → `Disposed`

**Subscription management:**
- All `IDisposable` handles returned by RxNET `.Subscribe()` calls are stored and disposed before new subscriptions are created — prevents duplicate handler accumulation across reconnect cycles.

**Exposes:**
```csharp
string? SessionId { get; }
IObservable<ParsedMessage> Messages { get; }
event EventHandler<ShardCloseArgs> OnClosed;
```

Does not know about users, tokens, or subscriptions.

### 4.2 ShardManager

Owns `ConcurrentDictionary<string, ShardContext>` (shard id → shard + its user assignments). User assignment operations are serialized by a `SemaphoreSlim(1,1)` to prevent race conditions in the capacity check-then-act.

```csharp
public interface IShardManager
{
    Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, int subscriptionCost, CancellationToken ct);
    Task ReleaseUserFromShardAsync(string userId, CancellationToken ct);
    IReadOnlyList<(string ShardId, string SessionId)> ActiveSessionIds { get; }
    event EventHandler<SessionIdUpdatedArgs> OnSessionIdUpdated;
}
```

**Capacity logic (under `SemaphoreSlim` lock):**
- Find first shard where `SubscriptionCount < 300` AND `Cost + newCost ≤ 10`
- None found → create new `ShardSequencer`, start it, wait for Welcome, then add user
- Shard loses all users → dispose shard, fire `OnSessionIdUpdated(shardId, oldSessionId, null)` to signal removal

**`session_id` surfacing:**
- `ShardSequencer` transitions to `Active` or completes reconnect → `ShardManager` fires `OnSessionIdUpdated(shardId, oldSessionId, newSessionId)`
- `ConduitOrchestrator` subscribes to this event

---

## 5. Phase 3 — Coupling UserSequencer with ShardManager

### 5.1 Changes to UserSequencer

**Removed:**
- `WebsocketClient Socket` field (from `UserBase` — see note below)
- `RunWebsocketAsync()` — WebSocket start/stop is `ShardSequencer`'s responsibility
- Direct WebSocket URL construction

**`UserBase` changes:**
`UserBase` currently declares `WebsocketClient Socket` and calls `Socket.Dispose()` in `DisposeProcedureAsync()`. Since `Socket` ownership moves to `ShardSequencer`, `UserBase.DisposeProcedureAsync()` must be updated to remove the `Socket.Dispose()` call. `UserBase` no longer holds any WebSocket reference.

**Added to UserSequencer:**
- `ShardId` — which shard this user is assigned to
- `SessionId` — sourced from `IShardBinding`, not self-generated
- Subscribes to `IShardBinding.UserMessages` (pre-filtered — see Section 5.3)

**State machine — semantic changes only.** All 16 states and 24 actions remain valid:

| Before | After |
|---|---|
| `WebsocketFail` triggered by own WebSocket disconnect | `WebsocketFail` triggered by `IShardBinding.OnShardLost` |
| `HandShake`: subscribe using own `session_id` | `HandShake`: subscribe using `SessionId` from `IShardBinding` |
| `Reconnecting` entry action: call `ReconnectOrFail()` | `Reconnecting` entry action: subscribe to `IShardBinding.OnSessionIdChanged`, await new `session_id`, then fire `UserActions.ReconnectSuccess` or `UserActions.ReconnectFail` |

The `Reconnecting` entry action must wire `IShardBinding.OnSessionIdChanged` to `StateMachine.FireAsync(UserActions.ReconnectSuccess, newSessionId)` using an `OnEntryAsync` delegate in the Stateless configuration.

**Implementation constraint:** `UserBase.StateMachineCofiguration` currently configures `Reconnecting` with only `.Permit` calls and no `OnEntryAsync`. Adding `OnEntryAsync` requires access to `IShardBinding`, which only `UserSequencer` holds. Resolution: add `protected abstract Task ReconnectingEntryAsync()` to `UserBase` (alongside the existing `ReconnectingAfterWatchdogFailAsync`) and wire it in `StateMachineCofiguration` as `.OnEntryAsync(ReconnectingEntryAsync)`. `UserSequencer` overrides it to subscribe to `IShardBinding.OnSessionIdChanged` and fire `ReconnectSuccess` / `ReconnectFail`.

### 5.2 IShardBinding

```csharp
public interface IShardBinding
{
    string SessionId { get; }
    IObservable<ParsedMessage> UserMessages { get; }  // pre-filtered for this user's broadcaster_user_id
    event EventHandler OnShardLost;
    event EventHandler<string> OnSessionIdChanged;    // new session_id after reconnect completes
}
```

`ShardManager` creates one `IShardBinding` per user when `GetOrCreateShardForUserAsync` is called. `UserSequencer` holds one `IShardBinding` — no direct WebSocket reference.

### 5.3 UserMessages filtering and routing keys

`IShardBinding.UserMessages` is a filtered view of the shard's `IObservable<ParsedMessage>`. The filter and routing key depend on the subscription type. Three routing categories exist:

**Category A — broadcaster-scoped (most subscription types)**
Routing key: `event.broadcaster_user_id`. Filter: `event.broadcaster_user_id == this.UserId`.

**Category B — user-scoped (e.g., `user.whisper.message`)**
Subscription condition uses `user_id`. Routing key at dispatch time: `event.user_id`. Filter: `event.user_id == this.UserId`.

**Category C — conduit/platform-scoped (`conduit.shard.disabled`)**
Subscription condition uses `client_id` only — not tied to any broadcaster or user. These events are **not** routed to any `EventProvider`. They are intercepted by `ConduitOrchestrator` directly: `conduit.shard.disabled` signals that a shard has become inactive, triggering `ShardManager` to start a replacement shard. These messages must be filtered out of `IShardBinding.UserMessages` entirely and handled at the `EventRouter` → `ConduitOrchestrator` path.

**Existing registry bug — `user.update` condition (`Register.cs:693`):**
`RegUserUpdate` currently sets `Conditions = CondList(ConditionTypes.ClientId)`. The Twitch EventSub reference specifies that `user.update` requires `user_id` as the subscription condition. This is an existing bug in `Register.cs` that must be corrected as part of Phase 5. Once corrected, `user.update` becomes Category B: subscription created per user with `user_id` condition, events routed by `event.user_id`.

The full routing key mapping must be verified against every entry in `SubsRegister.Register` before Phase 5 implementation begins. Any subscription type not covered by categories A, B, or C must be explicitly assigned a routing path.

### 5.4 ReconnectingFromWatchdog and watchdog fallback path

`UserBase` has a `ReconnectingFromWatchdog` state with `OnEntryAsync(ReconnectingAfterWatchdogFailAsync)` (currently in `UserSequencer`) that launches a new WebSocket connection. In conduit mode there is no per-user WebSocket to restart. This state is redefined:

- `ReconnectingAfterWatchdogFailAsync` in `UserSequencer`: instead of creating a new WebSocket, fires `IShardBinding.OnShardLost` signal to `ShardManager`, which detects the unresponsive shard and replaces it. `UserSequencer` then waits for `IShardBinding.OnSessionIdChanged` before returning to `InitialAccessTest`.

The watchdog fallback path in `OnWatchdogTimeoutAsync` (`UserSequencer.cs:682`) currently calls `Socket.Stop()` and `Socket.Dispose()` directly when the state machine cannot fire `ReconnectFromWatchdog`. In conduit mode these calls are replaced: instead of stopping the socket directly, the fallback fires `IShardBinding.OnShardLost` and invokes `OnOutsideDisconnectAsync` as before. The socket lifecycle remains `ShardSequencer`'s responsibility.

### 5.5 Revocation handling in conduit mode

`UserSequencer.RevocationMessageProcessingAsync()` currently auto-re-subscribes the revoked subscription. In conduit mode, subscriptions are managed at the conduit level. The auto-re-subscribe must be preserved but retargeted to use the conduit transport (i.e., use `ConduitId` as transport rather than `session_id`). The existing `_subscriptionManager.ApiTrySubscribeAsync` call site is retained with updated transport payload.

### 5.6 ShardManager as UserSequencer owner

`EventSubClient` delegates `AddUserAsync` / `DeleteUserAsync` / `UpdateUser` to `ShardManager`, which assigns/unassigns users to shards and manages `UserSequencer` lifecycle.

```
ShardManager
├── Shard #1 (ShardSequencer)
│    ├── UserSequencer A  ← IShardBinding from Shard #1
│    └── UserSequencer B  ← IShardBinding from Shard #1
└── Shard #2 (ShardSequencer)
     └── UserSequencer C  ← IShardBinding from Shard #2
```

---

## 6. Phase 4 — Conduit Layer

### 6.1 Startup lifecycle

`ConduitOrchestrator.InitializeAsync` is called from `IHostedService.StartAsync` on `EventSubClient` — before any `AddUserAsync` calls. This establishes the conduit before shards are started.

`EventSubClient` must implement both `IHostedService` and `IAsyncDisposable`. When registered via `AddTwitchEventSub()`, the library also calls `services.AddHostedService<EventSubClient>()` so that `StartAsync` / `StopAsync` are lifecycle-managed by the host.

### 6.2 ConduitOrchestrator

Single responsibility: keep the Twitch Conduit record in sync with live shards.

```csharp
public interface IConduitOrchestrator
{
    Task InitializeAsync(CancellationToken ct);
    Task AddShardAsync(string sessionId, CancellationToken ct);
    Task UpdateShardAsync(string oldSessionId, string newSessionId, CancellationToken ct);
    Task RemoveShardAsync(string sessionId, CancellationToken ct);
    Task TeardownAsync(CancellationToken ct);
    string ConduitId { get; }
}
```

**Startup sequence:**
1. Call `TwitchConduitApi.GetConduitsAsync()` — if an existing conduit exists for this `ClientId`, reuse it (store `ConduitId`). Only call `CreateConduitAsync()` if none exists. This prevents conduit quota exhaustion on restart.
2. Subscribe to `ShardManager.OnSessionIdUpdated`
3. As shards come online → `TwitchConduitApi.UpdateConduitShardsAsync()` to register each `session_id`

**Shard swap on reconnect:**
1. `ShardSequencer` enters `Reconnecting` — old `session_id` still registered on conduit (Twitch continues delivery)
2. New shard connects, new `session_id` confirmed via Welcome
3. `ConduitOrchestrator.UpdateShardAsync(old, new)` — must complete **before** old shard is disposed
4. If `UpdateShardAsync` fails: old shard remains active; new shard is disposed; `ShardManager` retries reconnect
5. Only on `UpdateShardAsync` success: old `ShardSequencer` disposes

**Teardown (required by Twitch spec — called from `IHostedService.StopAsync`):**
1. Stop accepting new users
2. Delete all subscriptions via `SubscriptionManager` (per-user, using user access tokens)
3. Delete conduit via `TwitchConduitApi` using app access token
4. Dispose all `ShardSequencer` instances

### 6.3 SubscriptionManager retargeting

Two changes are required — transport payload **and** authorization token:

**Transport payload** changes from WebSocket session to conduit:
```json
{
  "method": "conduit",
  "conduit_id": "<conduit_id>"
}
```

**Authorization** changes from user access token to app access token. Conduit subscriptions follow the webhook/app-token model. The user must have pre-authorized the relevant scopes against the client ID, but `SubscriptionManager` now uses `EventSubClientOptions.AppAccessToken` for all subscription API calls rather than the per-user token from `UserSequencer`.

`UserSequencer` calls `SubscriptionManager.SubscribeAsync(userId, subscriptionType, conduitId)` — same call site. `ConduitId` injected from `ConduitOrchestrator`. The per-user token is no longer passed to `SubscriptionManager`; it is used only in `UserSequencer`'s own token validation step.

### 6.4 Subscription persistence and event replay across shard transitions

**Conduit-initiated reconnect (`session_reconnect`):**
When Twitch sends a `session_reconnect` message, the shard swap in `ConduitOrchestrator.UpdateShardAsync` preserves all conduit subscriptions automatically — they remain attached to the conduit, not to the individual shard. No resubscription is required. Events continue to be delivered to the new shard without loss during the grace period.

**Unexpected shard loss (full drop, no reconnect message):**
Twitch does not replay events lost while no shard is active. In the conduit model this risk is mitigated: if multiple shards are running, other shards continue receiving events while the failed shard recovers. If only one shard is running and it drops, events during the recovery window are lost. `ShardManager` should maintain at least 2 shards when resilience is required — this is operator-configurable via `MaxShardsPerConduit`.

After a full shard loss and re-connection, conduit subscriptions are still intact (subscriptions live on the conduit, not the shard). `ConduitOrchestrator` registers the new shard's `session_id` without resubscribing. No `UserSequencer` resubscription is needed.

### 6.5 `StartAsync(userId)` / `StopAsync(userId)` redefinition

In conduit mode, the WebSocket is owned by the shard — not the user. `IEventSubClient.StartAsync(userId)` and `StopAsync(userId)` are redefined as:

- `StartAsync(userId)`: start the `UserSequencer` state machine for that user (transition from `Registered` to `InitialAccessTest`). Does not start a WebSocket.
- `StopAsync(userId)`: stop the `UserSequencer` for that user and release it from its shard. Does not stop the shard WebSocket if other users are still on it.

These changes are breaking and must appear in the v4.0.0 migration notes.

---

## 7. Phase 5 — Event Routing

### 7.1 EventRouter

```csharp
public interface IEventRouter
{
    void RegisterUser(string broadcasterUserId, IEventProvider provider);
    void UnregisterUser(string broadcasterUserId);
    void OnMessageReceived(ParsedMessage message);
}
```

**Message flow:**
```
ShardSequencer #1 ──┐
ShardSequencer #2 ──┼──► ShardManager ──► EventRouter ──► ReplayProtection (thread-safe singleton)
ShardSequencer #N ──┘                                           │
                                                    route by broadcaster_user_id / user_id
                                                                │
                                            ┌───────────────────┤
                                            ▼                   ▼
                                     EventProvider A     EventProvider B
```

**Routing key:** `event.broadcaster_user_id` for broadcaster-scoped events; `event.user_id` for user-scoped events. See Section 5.3 for per-subscription-type routing key mapping.

**Non-notification messages** (keepalive, reconnect, revocation) handled by `ShardManager` directly — WebSocket session concerns only.

**Revocation routing:** `condition.broadcaster_user_id` → correct `EventProvider` → fires `OnRevocationAsync` → `UserSequencer` retries subscription via conduit transport (see Section 5.4).

### 7.2 ReplayProtection — thread-safe singleton

Current implementation uses a plain `Queue<string>` with non-atomic check-then-enqueue. This is unsafe under concurrent shard delivery of the same `message_id`.

**Required changes:**
- Replace `Queue<string>` with a thread-safe structure. Options:
  - `ConcurrentDictionary<string, byte>` bounded by a size counter under `lock` on the eviction path
  - `MemoryCache` with TTL matching the Twitch 10-minute deduplication window
- All `IsDuplicate()` read-check-write operations must be atomic
- Increase cache from 10 to minimum 100 entries
- Promote from per-user instance to singleton registered in DI
- Keep existing 10-minute timestamp validation

### 7.3 EventProvider registration

`EventSubClient.AddUserAsync` → `ShardManager.GetOrCreateShardForUserAsync` → `EventRouter.RegisterUser`. `DeleteUserAsync` → `EventRouter.UnregisterUser`. No change to `IEventProvider` or `IEventSubClient` public surface.

---

## 8. Phase 6 — State Machine Coverage via Logging & Tests

### 8.1 Structured logging at every transition

Every state machine transition in `ShardSequencer` and `UserSequencer` emits a structured log entry using existing `LoggerExtension` methods.

**ShardSequencer:**
```
[Information] Shard {ShardId} transition: {FromState} → {ToState} trigger={Trigger}
[Warning]     Shard {ShardId} close code {Code}: {Reason} — action={Action}
[Error]       Shard {ShardId} critical stop: trigger={Trigger} state={State}
```

**UserSequencer (extended):**
```
[Information] User {UserId} transition: {FromState} → {ToState} trigger={Trigger} shard={ShardId}
[Warning]     User {UserId} token refresh requested: state={State} attempt={Attempt}
[Error]       User {UserId} failed: trigger={Trigger} state={State}
```

### 8.2 Test coverage targets

| Phase | Tests |
|---|---|
| Phase 1 | All 7 close codes → correct `ShardSequencer` state transition; `EventSubClientOptions` validation fails at startup when `ClientId` or `AppAccessToken` is missing or empty |
| Phase 2 | `ShardManager` creates new shard when current is full (subscription count); creates new shard when cost limit reached; disposes empty shard; concurrent `AddUserAsync` calls do not exceed shard capacity; `session_id` surfaced correctly |
| Phase 3 | `UserSequencer` transitions to correct state on `OnShardLost`; `OnSessionIdChanged` fires `ReconnectSuccess` and triggers re-subscribe; `UserBase.DisposeProcedureAsync` no longer calls `Socket.Dispose()` |
| Phase 4 | `ConduitOrchestrator` reuses existing conduit on restart; startup sequence order (conduit created before shards); shard swap calls `UpdateShardAsync` before disposing old shard; teardown deletes subscriptions before conduit |
| Phase 5 | `EventRouter` routes to correct `EventProvider` by `broadcaster_user_id`; routes by `user_id` for user-scoped events; duplicate `message_id` across two concurrent shards deduplicated exactly once; revocation routed and re-subscription uses conduit transport |

Existing tests (registry sanity, message processing, API serialization) extended — not replaced. All use existing `WireMock.Net` + `Moq` patterns.

---

## 9. Breaking Changes (v4.0.0)

| Area | Change |
|---|---|
| `EventSubClientOptions.AppAccessToken` | New required field — startup fails if missing or empty |
| `EventSubClientOptions.ClientId` | Now validated as non-empty at startup |
| `IEventSubClient.StartAsync(userId)` | Redefined: starts `UserSequencer` state machine only; does not start a WebSocket |
| `IEventSubClient.StopAsync(userId)` | Redefined: stops `UserSequencer` and releases user from shard; does not stop shard if other users remain |
| Per-user WebSocket | Removed — users share shard WebSocket connections |
| Conduit ownership | Library creates and owns a Twitch Conduit on startup — app must have conduit creation permission and a valid app access token |
| Conduit reuse | Library reuses an existing conduit if one is found for the configured `ClientId` on startup |
| `ServiceCollectionExtensions.AddTwitchEventSub()` | Signature unchanged; now also registers `EventSubClient` as `IHostedService` |
| `ReplayProtection` | Promoted to singleton; no longer per-user |
| Teardown | Library deletes all subscriptions and the conduit on `IHostedService.StopAsync` / `IAsyncDisposable.DisposeAsync` |

---

## 10. Files to Create / Modify

### New files
- `CoreFunctions/ShardSequencer.cs`
- `CoreFunctions/ShardManager.cs`
- `CoreFunctions/IShardBinding.cs`
- `CoreFunctions/EventRouter.cs`
- `API/ConduitOrchestrator.cs`
- `IConduitOrchestrator.cs`
- `IShardManager.cs`
- `IEventRouter.cs`

### Modified files
- `EventSubClientOptions.cs` — `[MinLength(1)]` + `[Required]`, new fields, `ValidateOnStart()`
- `User/UserBase.cs` — remove `WebsocketClient Socket` field and `Socket.Dispose()` from `DisposeProcedureAsync`; remove WebSocket URL construction
- `User/UserSequencer.cs` — remove WebSocket ownership; add `IShardBinding`; wire `Reconnecting` entry action to `IShardBinding.OnSessionIdChanged`; retarget revocation re-subscribe to conduit transport
- `User/SubscriptionManager.cs` — conduit transport payload; inject `ConduitId`
- `EventSubClient.cs` — implement `IHostedService` + `IAsyncDisposable`; delegate user lifecycle to `ShardManager`; call `ConduitOrchestrator.InitializeAsync` in `StartAsync`; call `TeardownAsync` in `StopAsync`
- `ServiceCollectionExtensions.cs` — register new components; add `AddHostedService<EventSubClient>()`
- `CoreFunctions/ReplayProtection.cs` — replace `Queue<string>` with thread-safe structure; increase cache to ≥ 100; promote to singleton
- `SubsRegister/Register.cs` — fix `RegUserUpdate` condition from `ClientId` to `UserId` (existing bug)
- `TwitchEventSub_Websocket.Tests/` — extend existing + add new tests per phase

### Removed files
- None immediately — per-user WebSocket code removed from `UserBase.cs` and `UserSequencer.cs` inline
