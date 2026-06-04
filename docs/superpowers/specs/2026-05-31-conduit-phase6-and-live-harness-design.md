# Conduit Phase 6 Integration + Live OAuth Test Harness — Design

**Date:** 2026-05-31
**Branch:** `develop/conduit`
**Author:** audit + design session

## 1. Context & current state

`develop/conduit` introduces conduit transport (app-token managed conduit + sharded WebSockets) replacing master's per-user WebSocket model. A full audit found:

- **Build was broken** by committed merge-conflict markers in `Twitch.EventSub_Websocket.csproj` and `User/UserSequencer.cs`. **Resolved** (kept conduit/HEAD side). Solution now builds (0 errors) and all 78 unit tests pass — this is our baseline.
- **The conduit transport is unwired in production.** Every building block exists and is unit-tested, but nothing assembles them at runtime:
  - `ShardManager.GetOrCreateShardForUserAsync` / `UserSequencer.SetShardBinding` — called only by tests.
  - `ShardSequencer.ConnectAsync` / `HandleWelcomeAsync` / `HandleReconnectAsync` / `HandleCloseCodeAsync` — never called in production; tests use `Simulate*ForTest`.
  - `EventRouter` — DI-registered but consumed by nothing.
  - Net effect: a started user reaches `UserState.Websocket`, `AwaitShardReadyAsync` finds a null `_shardBinding`, fires `WebsocketFail`, and the user dies right after token validation. No shard socket is ever opened, so `ConduitOrchestrator` never receives a session id.

This design closes that gap (**Part A — Phase 6 integration**) and adds an interactive **Part B — live OAuth harness** to exercise the assembled library against real Twitch.

### Token model (unchanged, for reference)
- **App access token** (`EventSubClientOptions.AppAccessToken`): conduit management + all subscription CRUD. Requires `client_credentials` grant (client secret). The library does **not** mint it — the caller supplies it.
- **User access token** (per `AddUserAsync`): used only for pre-flight `/oauth2/validate` and revocation re-subscribe. Its scope grant is what authorizes conduit subscriptions server-side.

## 2. Part A — Phase 6 integration layer

Goal: assemble the existing components into a working runtime path with the **minimum** new code. Three gaps to close.

### A1. Shard self-drives its own state machine
`ShardSequencer.SubscribeToClient` currently deserializes active-connection messages and republishes them to `_messages`, but never advances its own FSM. Change it to react to the active connection:
- Welcome message → `HandleWelcomeAsync(session.id)` (Disconnected/WaitingForWelcome → Active).
- Reconnect message → `HandleReconnectAsync(session.reconnect_url)`; on the pending connection's Welcome → `HandleNewConnectionWelcomeAsync(newSession.id)`.
- `DisconnectionHappened` (active, non-pending) → `HandleCloseCodeAsync(code)` using the WebSocket close status.

The public `Handle*` methods stay (keeps Phase 2 tests valid); they simply also get called internally. Welcome/Reconnect/NewConnectionWelcome continue to be published to `_messages` so `UserSequencer` (via `ShardBinding`) still processes them for its own FSM.

New event so the manager learns the session:
```csharp
public event EventHandler<string>? OnSessionAssigned; // fired by HandleWelcomeAsync & HandleNewConnectionWelcomeAsync with the new sessionId
```

### A2. ShardManager connects new shards and forwards session changes
In `GetOrCreateShardForUserAsync`, when a **new** `ShardSequencer` is created:
1. Subscribe to its `OnSessionAssigned`: track previous session per shard and call `NotifySessionIdUpdated(shardId, oldSession, newSession)` (old `null` on first welcome ⇒ `AddShardAsync`; non-null ⇒ `UpdateShardAsync`).
2. Subscribe to its `OnClosed` for terminal (4001) shards to release/replace (best-effort; full replacement is future work).
3. `await sequencer.ConnectAsync(BuildWebSocketUri(), ct)` to actually open the socket.

`BuildWebSocketUri()` = `wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds={options.KeepaliveTimeoutSeconds}` (production URL; testing-URL injection is out of scope since we test against real Twitch).

Existing `OnSessionIdUpdated` → `EventSubClient.OnShardSessionIdUpdated` → `ConduitOrchestrator.{Add,Update,Remove}ShardAsync` path then registers the session with the conduit. (Already wired in `EventSubClient`.)

### A3. Bind user → shard around start/stop
`EventProvider` gains an injected `IShardManager` (added to its constructor; `EventSubClient.AddUserAsync` already has it). Binding must survive `UserSequencer` re-creation (recovery), so:
- `EventProvider` acquires/holds an `IShardBinding` and applies it inside `Create()` via `_userSequencer.SetShardBinding(binding)` every time the sequencer is (re)created.
- Acquire the binding lazily at first `StartAsync`: `binding ??= await _shardManager.GetOrCreateShardForUserAsync(userId, ct)`.
- On `StopAsync`/dispose and on `EventSubClient.DeleteUserAsync`: `await _shardManager.ReleaseUserFromShardAsync(userId, ct)` and dispose the binding.

`EventSubClient.StartAsync(userId)` stays the entry point; the binding acquisition happens inside the provider so recovery restarts re-use the same shard assignment.

### A4. Ordering / known timing consideration
Sequence per user: shard socket opens → Welcome → session registered to conduit (`AddShardAsync`) ‖ `UserSequencer` proceeds Websocket → WelcomeMessage → HandShake (creates subs on the conduit by `conduitId`). Subscriptions are bound to the **conduit**, not the session, so a sub created microseconds before the session is registered still delivers once the session is active; at worst a few initial events are missed. Acceptable for a smoke test. Gating handshake on conduit-registration is noted as a future hardening, not done here.

### A5. Explicitly out of scope (Part A)
- `conduit.shard.disabled` → automatic replacement-shard provisioning (documented; deferred).
- Removing/retiring the now-redundant `EventRouter` (leave in place, unused, to minimize churn).
- Multi-shard spill/rebalance correctness beyond what current unit tests cover.

## 3. Part B — Live OAuth test harness (standalone console app)

New project: `TwitchEventSub.LiveHarness` (net10.0 console) referencing the library. **Not** added to the shipped NuGet; for local manual runs only.

### B1. Configuration & secrets
- `ClientId` = `ysuewozy8rdlarc4c9lljb6ripiyxa` (temporary test client).
- `ClientSecret` = supplied by the user via **dotnet user-secrets** (or `TWITCH_CLIENT_SECRET` env var). **Never committed.** A `.gitignore` entry covers any local `appsettings.*.json`.
- Redirect URI: `http://localhost:5000/` (registered on the test client).

### B2. Auth flow
1. **App access token** — `POST https://id.twitch.tv/oauth2/token` with `grant_type=client_credentials` → app token for conduit + subs.
2. **User access token** — Authorization Code flow:
   - Spin up an `HttpListener` on `http://localhost:5000/`.
   - Open the browser to `https://id.twitch.tv/oauth2/authorize?response_type=code&client_id=...&redirect_uri=http://localhost:5000&scope=<scopes>`.
   - Capture `?code=` on redirect, return a friendly "you can close this tab" page.
   - Exchange `code` → user access token (+ refresh token) via `oauth2/token`.
3. **User id** — `GET https://api.twitch.tv/helix/users` with the user token → `data[0].id`.

### B3. Scopes (from chosen events)
`channel.update` and `stream.online/offline` need none; `channel.chat.message` needs `user:read:chat`; `channel.follow` v2 needs `moderator:read:followers`. Requested scope set: `user:read:chat moderator:read:followers`.

### B4. Wiring & run loop
- `AddTwitchEventSub(o => { o.ClientId = ...; o.AppAccessToken = appToken; })`, build host, start it (creates the conduit via `IHostedService`).
- `AddUserAsync(userId, userToken, [ChannelUpdate, StreamOnline, StreamOffline, ChannelChatMessage, ChannelFollow], allowRecovery: false)`.
- Subscribe console printers to `OnUpdateEventAsync`, `OnStreamOnlineEventAsync`, `OnStreamOfflineEventAsync`, `OnChatEventAsync`, `OnFollowEventAsync`, plus `OnRawMessageAsync` (debug) and `OnRefreshTokenAsync` (refresh via `refresh_token` grant).
- `StartAsync(userId)`. Print a checklist of how to trigger each event (change title, type in chat, etc.). Run until Ctrl-C.
- On shutdown: `DeleteUserAsync(userId)` then host stop → `ConduitOrchestrator.TeardownAsync` deletes the conduit (Twitch auto-removes its subscriptions).

### B5. Subscription-type mapping check
Confirm the chosen `SubscriptionTypes` enum members exist and map to the right type+version+condition in the registry (`SubsRegister/Register.cs`) during implementation; adjust the event list if any are websocket-only-incompatible.

## 4. Testing strategy
- **Unit (TDD, must stay green + new):** Part A adds tests for: shard self-drive (Welcome on the message stream drives Active + fires `OnSessionAssigned`); `ShardManager` calls `ConnectAsync` and emits `OnSessionIdUpdated` on first welcome; `EventProvider` sets the binding on create and releases on stop; an `EventSubClient` start→user-reaches-handshake happy path with mocked shard/socket. Reuse existing Phase 1–5 tests as regression.
- **Live (manual):** the harness is the end-to-end verification on real Twitch, on the user's behalf.
- All `dotnet test` must pass before any live run.

## 5. Build sequence
1. (done) Resolve conflicts, green baseline.
2. Part A via TDD: A1 shard self-drive → A2 manager connect/forward → A3 provider binding → A4 client happy-path integration test.
3. `dotnet test` green.
4. Part B harness project + auth + wiring.
5. Live smoke test with the user logged in.
6. Cleanup: remove temp client usage, document, decide on conflict-fix commit.

## 6. Risks
- Output/locale noise in this environment's shell — mitigate by checking exit codes and compact summaries.
- Conduit timing race (A4) — accepted for smoke test.
- Scope-grant mismatches surfacing only at live run — the harness prints subscription failures clearly so we can adjust scopes.
