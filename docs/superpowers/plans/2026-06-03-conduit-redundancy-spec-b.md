# Spec B — Conduit Redundancy, Shard Recovery & Cross-Conduit Dedup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Run the conduit transport across N independent conduit replicas (RedundancyFactor 1–3) with automatic `conduit.shard.disabled` recovery and cross-conduit event de-duplication, so losing a whole conduit does not lose events.

**Architecture:** `ConduitOrchestrator` owns N `ConduitReplica`s (each = today's single-conduit state). `ShardManager` allocates shards per replica. `EventProvider` runs N `UserSequencer`s per identity (one per replica). A shared two-layer dedup gate (`message_id` + event-content hash) collapses redundant deliveries. `conduit.shard.disabled` is type-routed by `MessagePipeline` to the orchestrator, which reactivates the shard with a fresh session.

**Tech Stack:** C#/.NET 10, xUnit, Moq, System.Reactive, Newtonsoft.Json, Stateless, Websocket.Client.

**Spec:** `docs/superpowers/specs/2026-06-03-conduit-redundancy-spec-b-design.md`

**Conventions (every task):**
- **DO NOT git commit / git add. Leave all changes uncommitted in the working tree.** (Standing user instruction this session.)
- Kill the harness before building (it locks the DLL): `Get-Process -Name "TwitchEventSub.LiveHarness" -ErrorAction SilentlyContinue | Stop-Process -Force`
- Build: `dotnet build "TwitchEventSubWebsocket.sln" -c Debug --nologo`
- Filtered test: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~<Name>"`
- PowerShell shell. Baseline: 155 tests pass.
- New tests go under `TwitchEventSub_Websocket.Tests/Phase8Tests/`.

**Current signatures (verified, for reference):**
- `IConduitOrchestrator`: `InitializeAsync(ct)`, `AddShardAsync(shardId, sessionId, ct)`, `UpdateShardAsync(shardId, old, new, ct)`, `RemoveShardAsync(shardId, ct)`, `TeardownAsync(ct)`, `string ConduitId { get; }`.
- `IShardManager`: `GetOrCreateShardForUserAsync(userId, ct)`, `ReleaseUserFromShardAsync(userId, ct)`, `IReadOnlyList<(string,string?)> ActiveSessionIds`, `event EventHandler<SessionIdUpdatedArgs> OnSessionIdUpdated`. Concrete `ShardManager(IOptions<EventSubClientOptions>, ILogger<ShardManager>, IMessagePipeline)` with overridable `CreateShard(shardId)`/`ConnectShardAsync(shard, ct)`.
- `SessionIdUpdatedArgs { string ShardId; string? OldSessionId; string? NewSessionId; }` (init-only).
- `EventProvider` ctor: `(userId, accessToken, listOfSubs, clientId, logger, allowRecovery, twitchApi, conduitOrchestrator, appAccessToken, shardManager, replayProtection, messagePipeline, keepAliveTimeoutSeconds)`.
- `EventSubClient` ctor: `(IOptions<EventSubClientOptions>, ILogger<EventSubClient>, TwitchApi, IConduitOrchestrator, IShardManager, ReplayProtection, IMessagePipeline)`. `OnShardSessionIdUpdated` calls orchestrator Add/Update/Remove by shardId.
- `MessagePipeline`: `RegisterUser(userId, handler)`, `UnregisterUser(userId)`, `Attach(stream)`. Routes notifications by `condition.BroadcasterUserId ?? condition.UserId`.

---

## File Structure

**New (library):**
- `API/ConduitReplica.cs` — per-replica state + shard-slot math (internal)
- `CoreFunctions/EventKey.cs` — event-content hash for cross-conduit dedup

**Modified (library):**
- `IConduitOrchestrator.cs` — replica-addressed shard ops; `ConduitIds`/`ConduitIdAt`; `HandleShardDisabledAsync`
- `API/ConduitOrchestrator.cs` — owns `List<ConduitReplica>`; N-conduit init + orphan reconcile; shard.disabled bootstrap + recovery
- `IShardManager.cs` + `CoreFunctions/ShardManager.cs` — per-replica allocation; replica index in args
- `CoreFunctions/SessionIdUpdatedArgs.cs` — add `int ReplicaIndex`
- `CoreFunctions/MessagePipeline.cs` + `IMessagePipeline.cs` — `RegisterPlatformHandler`; type-route `conduit.shard.disabled`
- `CoreFunctions/ReplayProtection.cs` — `IsDuplicateEvent(eventKey)`
- `User/UserSequencer.cs` — event-key dedup on notification path
- `User/EventProvider.cs` — N `UserSequencer`s per identity
- `EventSubClient.cs` — wire replica index through; pass redundancy to providers
- `EventSubClientOptions.cs` — `RedundancyFactor` + validation
- `ServiceCollectionExtensions.cs` — register platform handler wiring
- `SubsRegister/Register.cs` + `API/Extensions/CreateSubscriptionRequestExtension.cs` — fix `conduit.shard.disabled` condition (client_id + conduit_id)
- `TwitchEventSub.LiveHarness/Program.cs` — `RedundancyFactor` knob

**Tests:** `Phase8Tests/` — `ConduitReplicaTests`, `OrchestratorMultiConduitTests`, `OrphanReconcileTests`, `ShardDisabledRoutingTests`, `ShardRecoveryTests`, `RedundancyOptionsTests`, `EventKeyTests`, `EventDedupTests`, `RedundancyProviderTests`, `RedundancySyntheticTests`, `RedundancyFuzzTests`.

---

## PHASE B1a — ConduitReplica (state + shard math extraction)

### Task 1: `ConduitReplica` with shard-slot math

**Files:**
- Create: `Twitch EventSub library/API/ConduitReplica.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/ConduitReplicaTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase8Tests/ConduitReplicaTests.cs`:

```csharp
using Twitch.EventSub.API;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ConduitReplicaTests
{
    [Fact]
    public void AddShard_AssignsSequentialIndices_AndExpands()
    {
        var r = new ConduitReplica(0, "conduit-A");
        var i0 = r.ReserveShardSlot("shard-1");   // returns (twitchIndex, needsExpandTo)
        Assert.Equal("0", i0.TwitchIndex);
        Assert.Equal(1, i0.NewShardCount);
        r.CommitShard("shard-1", "0", "sess-1");

        var i1 = r.ReserveShardSlot("shard-2");
        Assert.Equal("1", i1.TwitchIndex);
        Assert.Equal(2, i1.NewShardCount);
        r.CommitShard("shard-2", "1", "sess-2");

        Assert.Equal(2, r.TwitchShardCount);
        Assert.True(r.TryGetShard("shard-1", out var e1) && e1.SessionId == "sess-1");
    }

    [Fact]
    public void RemoveShard_SwapsLastIntoFreedSlot_AndScalesDown()
    {
        var r = new ConduitReplica(0, "conduit-A");
        r.CommitShard("shard-1", "0", "sess-1"); r.TwitchShardCount = 1;
        r.CommitShard("shard-2", "1", "sess-2"); r.TwitchShardCount = 2;

        var plan = r.PlanRemoval("shard-1");   // returns swap instruction
        Assert.False(plan.TargetIsLast);
        Assert.Equal("0", plan.FreedIndex);
        Assert.Equal("sess-2", plan.LastSessionId);   // last shard's session moves into freed slot

        r.ApplyRemoval("shard-1", plan);
        Assert.Equal(1, r.TwitchShardCount);
        Assert.True(r.TryGetShard("shard-2", out var moved) && moved.TwitchIndex == "0");
        Assert.False(r.TryGetShard("shard-1", out _));
    }
}
```

- [ ] **Step 2: Run → FAIL** (`ConduitReplica` does not exist).

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~ConduitReplicaTests"`

- [ ] **Step 3: Implement `ConduitReplica`**

Create `Twitch EventSub library/API/ConduitReplica.cs`:

```csharp
namespace Twitch.EventSub.API;

/// <summary>
/// Per-conduit replica state and shard-slot bookkeeping. One instance per redundant conduit.
/// Pure in-memory math; the orchestrator performs the actual Twitch API writes.
/// </summary>
internal sealed class ConduitReplica
{
    public int Index { get; }
    public string ConduitId { get; set; }
    public int TwitchShardCount { get; set; }

    // internal shardId -> (Twitch slot index, current sessionId)
    private readonly Dictionary<string, (string TwitchIndex, string SessionId)> _shardMap = new();

    public ConduitReplica(int index, string conduitId)
    {
        Index = index;
        ConduitId = conduitId;
    }

    public IReadOnlyDictionary<string, (string TwitchIndex, string SessionId)> Shards => _shardMap;
    public int ShardCount => _shardMap.Count;

    public bool TryGetShard(string shardId, out (string TwitchIndex, string SessionId) entry) =>
        _shardMap.TryGetValue(shardId, out entry);

    public readonly record struct SlotReservation(string TwitchIndex, int NewShardCount, bool NeedsExpand);

    /// <summary>Computes the next slot for a new shard; does not mutate the map.</summary>
    public SlotReservation ReserveShardSlot(string shardId)
    {
        int nextIndex = _shardMap.Count;
        bool needsExpand = nextIndex >= TwitchShardCount;
        int newCount = needsExpand ? nextIndex + 1 : TwitchShardCount;
        return new SlotReservation(nextIndex.ToString(), newCount, needsExpand);
    }

    public void CommitShard(string shardId, string twitchIndex, string sessionId)
    {
        _shardMap[shardId] = (twitchIndex, sessionId);
    }

    public void UpdateShardSession(string shardId, string newSessionId)
    {
        if (_shardMap.TryGetValue(shardId, out var e))
            _shardMap[shardId] = (e.TwitchIndex, newSessionId);
    }

    public readonly record struct RemovalPlan(bool TargetIsLast, string FreedIndex, string? LastShardId, string? LastSessionId);

    /// <summary>Plans a clean scale-down removal (swap target with last slot). Does not mutate.</summary>
    public RemovalPlan PlanRemoval(string shardId)
    {
        if (!_shardMap.TryGetValue(shardId, out var target))
            return new RemovalPlan(true, "-1", null, null);

        string lastIndexStr = (TwitchShardCount - 1).ToString();
        var lastEntry = _shardMap.FirstOrDefault(kv => kv.Value.TwitchIndex == lastIndexStr);
        bool targetIsLast = target.TwitchIndex == lastIndexStr;
        return new RemovalPlan(targetIsLast, target.TwitchIndex, lastEntry.Key, lastEntry.Value.SessionId);
    }

    /// <summary>Applies the planned removal to the in-memory map and shard count.</summary>
    public void ApplyRemoval(string shardId, RemovalPlan plan)
    {
        if (!plan.TargetIsLast && plan.LastShardId != null)
            _shardMap[plan.LastShardId] = (plan.FreedIndex, plan.LastSessionId!);

        _shardMap.Remove(shardId);
        if (TwitchShardCount > 1) TwitchShardCount -= 1;
    }
}
```

- [ ] **Step 4: Run → PASS** (2 tests). Build the solution → 0 errors.

- [ ] **Step 5: DO NOT COMMIT.**

---

## PHASE B1b — Orchestrator owns N replicas; ShardManager per-replica

### Task 2: `SessionIdUpdatedArgs` + `IShardManager` gain replica index

**Files:**
- Modify: `Twitch EventSub library/CoreFunctions/SessionIdUpdatedArgs.cs`
- Modify: `Twitch EventSub library/IShardManager.cs`
- Modify: `Twitch EventSub library/CoreFunctions/ShardManager.cs`, `ShardContext.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/OrchestratorMultiConduitTests.cs` (created in Task 3 will rely on this; add a small ShardManager test here)

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase8Tests/ShardManagerReplicaTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ShardManagerReplicaTests
{
    private sealed class TestableShardManager : ShardManager
    {
        public TestableShardManager(IOptions<EventSubClientOptions> o)
            : base(o, NullLogger<ShardManager>.Instance, new MessagePipeline(NullLogger<MessagePipeline>.Instance)) { }
        internal override ShardSequencer CreateShard(string shardId) => new(shardId, NullLogger.Instance);
        internal override Task ConnectShardAsync(ShardSequencer shard, System.Threading.CancellationToken ct) => Task.CompletedTask;
    }

    private static IOptions<EventSubClientOptions> Opts() =>
        Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxShardsPerConduit = 10, MaxUsersPerShard = 100 });

    [Fact]
    public async Task SessionUpdate_CarriesReplicaIndex()
    {
        var manager = new TestableShardManager(Opts());
        SessionIdUpdatedArgs? captured = null;
        manager.OnSessionIdUpdated += (_, a) => captured = a;

        var binding = await manager.GetOrCreateShardForUserAsync("user-1", replicaIndex: 2, System.Threading.CancellationToken.None);
        manager.SimulateSessionIdUpdatedForTest("user-1", "sess-1");

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.ReplicaIndex);
    }
}
```

- [ ] **Step 2: Run → FAIL** (`GetOrCreateShardForUserAsync` has no replicaIndex param; `SessionIdUpdatedArgs` has no `ReplicaIndex`).

- [ ] **Step 3: Implement**

In `CoreFunctions/SessionIdUpdatedArgs.cs` add:
```csharp
    public int ReplicaIndex { get; init; }
```

In `IShardManager.cs` change the two methods:
```csharp
    Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, int replicaIndex, CancellationToken ct);
    Task ReleaseUserFromShardAsync(string userId, int replicaIndex, CancellationToken ct);
```

In `ShardManager.cs`:
- The user→shard key must now be `(userId, replicaIndex)`. Change `_userToShard` to `ConcurrentDictionary<(string,int),string>` and the shard pool to be partitioned by replica. Simplest correct approach: maintain `_shards` keyed by shardId as today, but record each ShardContext's `ReplicaIndex`, and only reuse a shard whose `ReplicaIndex` matches the requested one.
- Add `int ReplicaIndex` to `ShardContext` (set at creation).
- When raising `OnSessionIdUpdated` (all sites: `NotifySessionIdUpdated`, the empty-shard release path, `SimulateSessionIdUpdatedForTest`), include `ReplicaIndex = ctx.ReplicaIndex`.
- `NotifySessionIdUpdated(shardId, old, new)` → look up the owning ShardContext to get its ReplicaIndex; include it.
- Keep `CreateShard`/`ConnectShardAsync` seams. `ConnectShardAsync` is unchanged (URL is the same for all replicas — the conduit binding is server-side, not in the WS URL).

`ShardContext.cs` add:
```csharp
    public int ReplicaIndex { get; init; }
```
and set it where contexts are created in `ShardManager.GetOrCreateShardForUserAsync`.

- [ ] **Step 4: Run → PASS**. Build → expect errors in callers (`EventProvider`, `EventSubClient.OnShardSessionIdUpdated`, existing Phase2/6 tests) — fixed in Tasks 3–4 and their test-update steps. If you can, update the obvious caller compile sites minimally to pass `replicaIndex: 0` so the solution builds; final correctness comes in Task 7.

- [ ] **Step 5: DO NOT COMMIT.**

### Task 3: Orchestrator holds N replicas; replica-addressed shard ops

**Files:**
- Modify: `Twitch EventSub library/IConduitOrchestrator.cs`
- Modify: `Twitch EventSub library/API/ConduitOrchestrator.cs`
- Modify: `Twitch EventSub library/EventSubClient.cs` (`OnShardSessionIdUpdated`)
- Modify: `Twitch EventSub library/EventSubClientOptions.cs` (`RedundancyFactor` — defined fully in Task 8, but add the property now defaulting 1 so init can read it)
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/OrchestratorMultiConduitTests.cs`

- [ ] **Step 1: Write the failing test**

Create `OrchestratorMultiConduitTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.APIConduit;
using Twitch.EventSub.APIConduit.Models.Shared;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class OrchestratorMultiConduitTests
{
    private static IOptions<EventSubClientOptions> Opts(int redundancy) =>
        Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxConduits = 5, RedundancyFactor = redundancy });

    [Fact]
    public async Task Initialize_CreatesRedundancyFactorConduits()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string>());
        var created = 0;
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(() => $"conduit-{created++}");
        // shard.disabled subscription creation is via a sub API path; allow it to no-op here (Task 6 adds it).
        var orch = new ConduitOrchestrator(api.Object, Opts(2), NullLogger<ConduitOrchestrator>.Instance);

        await orch.InitializeAsync(CancellationToken.None);

        Assert.Equal(2, orch.ConduitIds.Count);
        Assert.Equal("conduit-0", orch.ConduitIdAt(0));
        Assert.Equal("conduit-1", orch.ConduitIdAt(1));
    }

    [Fact]
    public async Task AddShard_RoutesToNamedReplica()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string>());
        var created = 0;
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(() => $"conduit-{created++}");
        var orch = new ConduitOrchestrator(api.Object, Opts(2), NullLogger<ConduitOrchestrator>.Instance);
        await orch.InitializeAsync(CancellationToken.None);

        await orch.AddShardAsync(replicaIndex: 1, "shard-x", "sess-x", CancellationToken.None);

        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-1", "0", "sess-x",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

Note: this requires `ITwitchConduitApi` to expose the methods the orchestrator calls (`GetConduitIdsAsync`, `CreateConduitAsync`, `UpdateConduitShardCountAsync`, `UpdateConduitShardSessionAsync`, `DeleteConduitAsync`, and for orphan reconcile `GetAllConduitGetShardsAsync`, and for B4 a create-subscription call). The current `ITwitchConduitApi` has the first five; for this task it is enough. If `GetAllConduitGetShardsAsync` is on the concrete class but not the interface, add it to `ITwitchConduitApi` now (signature copied from the concrete method) so Task 5 can mock it.

- [ ] **Step 2: Run → FAIL** (interface/shape mismatch).

- [ ] **Step 3: Implement**

In `IConduitOrchestrator.cs` replace the body with:
```csharp
namespace Twitch.EventSub;

public interface IConduitOrchestrator
{
    Task InitializeAsync(CancellationToken ct);
    Task AddShardAsync(int replicaIndex, string shardId, string sessionId, CancellationToken ct);
    Task UpdateShardAsync(int replicaIndex, string shardId, string oldSessionId, string newSessionId, CancellationToken ct);
    Task RemoveShardAsync(int replicaIndex, string shardId, CancellationToken ct);
    Task TeardownAsync(CancellationToken ct);
    Task HandleShardDisabledAsync(string conduitId, string shardId, CancellationToken ct);
    IReadOnlyList<string> ConduitIds { get; }
    string ConduitIdAt(int replicaIndex);
}
```

In `ConduitOrchestrator.cs`:
- Replace `ConduitId`/`_shardMap`/`_twitchShardCount` with `private readonly List<ConduitReplica> _replicas = new();`
- `ConduitIds => _replicas.Select(r => r.ConduitId).ToList();` `ConduitIdAt(i) => _replicas[i].ConduitId;`
- `InitializeAsync`: read `_options.RedundancyFactor` (default 1). `existing = GetConduitIdsAsync(...)`. If `existing.Count > _options.MaxConduits` throw (as today). Build replicas: reuse up to `RedundancyFactor` existing ids; for each reused, **orphan-reconcile** (Task 5 fills this — for now set `TwitchShardCount = 1` and clear map, leaving a `// TODO B1b orphan reconcile` ONLY if Task 5 not yet done; since Task 5 is in this plan, implement reconcile here using `GetAllConduitGetShardsAsync`). Create the shortfall via `CreateConduitAsync`. (shard.disabled subscription bootstrap added in Task 6.)
- `AddShardAsync(replicaIndex, …)`: under the lock, `var r = _replicas[replicaIndex];` `var slot = r.ReserveShardSlot(shardId);` if `slot.NeedsExpand` call `UpdateConduitShardCountAsync(r.ConduitId, slot.NewShardCount, …)` and set `r.TwitchShardCount = slot.NewShardCount;` then `UpdateConduitShardSessionAsync(r.ConduitId, slot.TwitchIndex, sessionId, …)` and `r.CommitShard(shardId, slot.TwitchIndex, sessionId);`
- `UpdateShardAsync(replicaIndex, …)`: `var r = _replicas[replicaIndex];` if `r.TryGetShard(shardId, out var e)` → `UpdateConduitShardSessionAsync(r.ConduitId, e.TwitchIndex, newSessionId, …)`; `r.UpdateShardSession(shardId, newSessionId);`
- `RemoveShardAsync(replicaIndex, …)`: `var r = _replicas[replicaIndex];` `var plan = r.PlanRemoval(shardId);` if `!plan.TargetIsLast && plan.LastShardId != null` → `UpdateConduitShardSessionAsync(r.ConduitId, plan.FreedIndex, plan.LastSessionId, …)`; then `r.ApplyRemoval(shardId, plan);` then `UpdateConduitShardCountAsync(r.ConduitId, r.TwitchShardCount, …)` if `>=1`.
- `TeardownAsync`: delete every replica's conduit; clear `_replicas`.
- `HandleShardDisabledAsync`: stub `=> Task.CompletedTask;` for now (Task 7 fills it).
- Keep the single `SemaphoreSlim`.

In `EventSubClient.OnShardSessionIdUpdated`, pass `args.ReplicaIndex` into the orchestrator calls:
```csharp
if (args.NewSessionId == null) await _conduitOrchestrator.RemoveShardAsync(args.ReplicaIndex, args.ShardId, CancellationToken.None);
else if (args.OldSessionId == null) await _conduitOrchestrator.AddShardAsync(args.ReplicaIndex, args.ShardId, args.NewSessionId, CancellationToken.None);
else await _conduitOrchestrator.UpdateShardAsync(args.ReplicaIndex, args.ShardId, args.OldSessionId, args.NewSessionId, CancellationToken.None);
```

In `EventSubClientOptions.cs` add (full validation in Task 8):
```csharp
    [Range(1, 3)]
    public int RedundancyFactor { get; set; } = 1;
```

- [ ] **Step 4: Run → PASS** (2 tests). Build → fix Phase4 `ConduitOrchestratorTests` to the new signatures (they call old `AddShardAsync(shardId,…)`/`ConduitId`). Update them to pass `replicaIndex: 0` and use `ConduitIdAt(0)`.

- [ ] **Step 5: DO NOT COMMIT.**

### Task 4: Wire ShardManager replica index → EventSubClient (build green)

**Files:**
- Modify: `Twitch EventSub library/EventSubClient.cs`, `User/EventProvider.cs` (interim: pass `replicaIndex: 0`), Phase2/Phase6 tests.

- [ ] **Step 1:** Update `EventProvider.StartAsync`/`StopAsync` calls to `GetOrCreateShardForUserAsync(_userId, 0, …)` / `ReleaseUserFromShardAsync(_userId, 0, …)` as an interim single-replica wiring (Task 9 makes it N).
- [ ] **Step 2:** Update Phase2 `ShardManagerTests` and Phase6 `ShardManagerConnectTests`/`EventProviderBindingTests` calls to include `replicaIndex: 0`.
- [ ] **Step 3:** `dotnet build` → 0 errors. `dotnet test` full → all green (155 + new Phase8). Report count.
- [ ] **Step 4: DO NOT COMMIT.**

### Task 5: Orphan-shard reconciliation on reuse

**Files:**
- Modify: `Twitch EventSub library/API/ConduitOrchestrator.cs` (InitializeAsync reuse path)
- Modify: `Twitch EventSub library/APIConduit/ITwitchConduitApi.cs` (ensure `GetAllConduitGetShardsAsync` is on the interface)
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/OrphanReconcileTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.APIConduit;
using Twitch.EventSub.APIConduit.Models.Shared;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class OrphanReconcileTests
{
    [Fact]
    public async Task Reuse_RebuildsShardCountFromTwitch_NotResetTo1()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<string> { "existing-A" });
        api.Setup(a => a.GetAllConduitGetShardsAsync(It.IsAny<string>(), It.IsAny<string>(), "existing-A",
                It.IsAny<CancellationTokenSource>(), It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<SubscriptionStatusTypes>()))
           .ReturnsAsync(new List<ConduitShard>
           {
               new ConduitShard { Id = "0", Status = "enabled" },
               new ConduitShard { Id = "1", Status = "enabled" },
               new ConduitShard { Id = "2", Status = "enabled" },
           });
        var opts = Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxConduits = 5, RedundancyFactor = 1 });
        var orch = new ConduitOrchestrator(api.Object, opts, NullLogger<ConduitOrchestrator>.Instance);

        await orch.InitializeAsync(CancellationToken.None);

        // After reconcile, adding a shard should expand to index 3 (count was rebuilt to 3), not overwrite slot 1.
        await orch.AddShardAsync(0, "new-shard", "sess", CancellationToken.None);
        api.Verify(a => a.UpdateConduitShardCountAsync("existing-A", 4, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

(If `ConduitShard`'s property names differ, read `APIConduit/Models/Shared/ConduitShard.cs` and adjust the test's object initializer to the real fields — keep the Id/Status intent.)

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement reconcile**

Ensure `ITwitchConduitApi` declares `GetAllConduitGetShardsAsync` (copy the concrete signature from `TwitchApiConduit`). In `InitializeAsync` reuse path, for each reused conduit call `GetAllConduitGetShardsAsync` and set `replica.TwitchShardCount = shards.Count;` (do not re-add them to the in-memory map as owned — they are server-side slots with sessions the library no longer drives; rebuilding the count prevents index collisions on the next add). Replace the previous blanket `UpdateConduitShardCountAsync(ConduitId, 1, …)` reset.

- [ ] **Step 4: Run → PASS.** Build + full test → green.
- [ ] **Step 5: DO NOT COMMIT.**

---

## PHASE B4 — Shard health & recovery

### Task 6: Fix `conduit.shard.disabled` condition + bootstrap subscription

**Files:**
- Modify: `Twitch EventSub library/SubsRegister/Register.cs` (RegConduitShardDisabled)
- Modify: `Twitch EventSub library/API/Extensions/CreateSubscriptionRequestExtension.cs`
- Modify: `Twitch EventSub library/API/ConduitOrchestrator.cs` (create the sub per replica in InitializeAsync)
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/ShardDisabledRoutingTests.cs` (condition-building part)

- [ ] **Step 1: Write the failing test**

```csharp
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Extensions;
using Twitch.EventSub.API.Models;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ConduitShardDisabledConditionTests
{
    [Fact]
    public void SetSubscriptionType_ShardDisabled_SetsClientIdAndConduitId()
    {
        var req = new CreateSubscriptionRequest { Condition = new Condition(), Transport = new Transport() };
        // New overload that takes explicit client id + conduit id for platform subscriptions.
        req.SetConduitShardDisabled(clientId: "client-1", conduitId: "conduit-A");

        Assert.Equal("conduit.shard.disabled", req.Type);
        Assert.Equal("client-1", req.Condition.ClientId);
        Assert.Equal("conduit-A", req.Condition.ConduitId);
    }
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement**

In `Register.cs` `RegConduitShardDisabled`, set `Conditions = CondList(ConditionTypes.ClientId, ConditionTypes.ConduitId)`.

In `CreateSubscriptionRequestExtension.cs` add a dedicated builder (the generic `SetSubscriptionType` maps every condition field to the same `userId`, which is wrong for this platform type):
```csharp
        public static CreateSubscriptionRequest SetConduitShardDisabled(this CreateSubscriptionRequest request, string clientId, string conduitId)
        {
            request.Type = SubsRegister.RegisterKeys.ConduitShardDisabled.ToEventString();
            request.Version = "1";
            request.Condition.ClientId = clientId;
            request.Condition.ConduitId = conduitId;
            return request;
        }
```

In `ConduitOrchestrator.InitializeAsync`, after replicas exist, create the `conduit.shard.disabled` subscription on each replica using the app token and that conduit's id. (Use the conduit-subscription creation path. If the orchestrator does not already have a `TwitchApi.SubscribeAsync` dependency, inject `TwitchApi` into `ConduitOrchestrator` — add it to the constructor and DI. The request transport is `{ method = "conduit", conduit_id = replica.ConduitId }`.) Log creation; tolerate "already exists" (HTTP 409/Conflict → treat as success).

- [ ] **Step 4: Run → PASS.** Build + full test → green. Update any `ConduitOrchestrator` constructor callers/tests if you injected `TwitchApi` (Phase4 + Phase8 orchestrator tests — pass a `new TwitchApi(Mock.Of<IHttpClientFactory>())`).
- [ ] **Step 5: DO NOT COMMIT.**

### Task 7: Platform routing + shard recovery

**Files:**
- Modify: `Twitch EventSub library/IMessagePipeline.cs`, `CoreFunctions/MessagePipeline.cs`
- Modify: `Twitch EventSub library/API/ConduitOrchestrator.cs` (HandleShardDisabledAsync)
- Modify: `Twitch EventSub library/ServiceCollectionExtensions.cs` or `EventSubClient.StartAsync` (register platform handler)
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/ShardDisabledRoutingTests.cs`, `ShardRecoveryTests.cs`

- [ ] **Step 1: Write the failing tests**

`ShardDisabledRoutingTests.cs`:
```csharp
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ShardDisabledRoutingTests
{
    private static ShardInbound Disabled(string conduitId, string shardId) => new("{}", new WebSocketNotificationMessage
    {
        Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = "d1", MessageTimestamp = System.DateTime.UtcNow.ToString("o"), SubscriptionType = "conduit.shard.disabled" },
        Payload = new WebSocketNotificationPayload
        {
            Subscription = new WebSocketSubscription { Type = "conduit.shard.disabled", Condition = new Condition { ClientId = "c", ConduitId = conduitId } },
            Event = new ConduitShardDisabledEvent { ConduitId = conduitId, ShardId = shardId, Status = "websocket_disconnected" }
        }
    });

    [Fact]
    public async Task ShardDisabled_RoutesToPlatformHandler_NotUser()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        bool userGot = false; (string c, string s)? platform = null;
        pipeline.RegisterUser("anyone", _ => { userGot = true; return Task.CompletedTask; });
        pipeline.RegisterPlatformHandler(n =>
        {
            var ev = (ConduitShardDisabledEvent)n.Payload!.Event!;
            platform = (ev.ConduitId, ev.ShardId);
            return Task.CompletedTask;
        });
        pipeline.Attach(subject);

        subject.OnNext(Disabled("conduit-A", "3"));
        await Task.Delay(50);

        Assert.False(userGot);
        Assert.Equal(("conduit-A", "3"), platform);
    }
}
```

`ShardRecoveryTests.cs`:
```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.APIConduit;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class ShardRecoveryTests
{
    [Fact]
    public async Task HandleShardDisabled_OpensFreshSession_AndPatchesSlot()
    {
        var api = new Mock<ITwitchConduitApi>();
        api.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        var n = 0;
        api.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => $"conduit-{n++}");
        var opts = Options.Create(new EventSubClientOptions { ClientId = "c", AppAccessToken = "t", MaxConduits = 5, RedundancyFactor = 1 });

        // A fake shard provider the orchestrator uses to open a fresh session during recovery.
        var freshSession = "new-sess";
        var orch = new ConduitOrchestrator(api.Object, opts, NullLogger<ConduitOrchestrator>.Instance,
            new TwitchApi(Mock.Of<IHttpClientFactory>()));
        await orch.InitializeAsync(CancellationToken.None);
        // Seed a shard at slot 0 so there is something to recover.
        await orch.AddShardAsync(0, "shard-1", "old-sess", CancellationToken.None);
        api.Invocations.Clear();

        await orch.HandleShardDisabledAsync("conduit-0", "0", CancellationToken.None);

        // Recovery PATCHes slot 0 with some new session (non-empty, not the old one).
        api.Verify(a => a.UpdateConduitShardSessionAsync("conduit-0", "0",
            It.Is<string>(s => !string.IsNullOrEmpty(s) && s != "old-sess"),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
```

NOTE: `HandleShardDisabledAsync` needs a way to open a fresh WebSocket session for the replacement. The orchestrator does not own shards — `ShardManager` does. Resolve by giving the orchestrator a delegate/seam `Func<int,CancellationToken,Task<string>> OpenReplacementSessionAsync` set by the composition root (EventSubClient/DI), which calls into ShardManager to spin a new shard and return its session id. For the unit test, make this seam overridable/injectable so the test can supply one returning `"new-sess"`. Document the seam in the orchestrator.

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement**

`IMessagePipeline.cs` add `void RegisterPlatformHandler(Func<WebSocketNotificationMessage, Task> handler);`

`MessagePipeline.cs`: add `private Func<WebSocketNotificationMessage,Task>? _platform;` `public void RegisterPlatformHandler(...) => _platform = handler;` In `HandleAsync`, at the top of the notification branch:
```csharp
if (notification.Payload?.Subscription?.Type == "conduit.shard.disabled")
{
    if (_platform != null) await _platform(notification);
    return;
}
```

`ConduitOrchestrator`: add the recovery seam (constructor delegate or settable property `OpenReplacementSessionAsync`). Implement `HandleShardDisabledAsync(conduitId, shardId, ct)`: find replica by conduitId; `var newSession = await OpenReplacementSessionAsync(replica.Index, ct);` `await _api.UpdateConduitShardSessionAsync(conduitId, shardId, newSession, _options.AppAccessToken, _options.ClientId, ct);` `replica.UpdateShardSession(shardId-or-mapped, newSession);` Log.

Composition (EventSubClient.StartAsync or DI): `_messagePipeline.RegisterPlatformHandler(n => { var ev=(ConduitShardDisabledEvent)n.Payload.Event; return _conduitOrchestrator.HandleShardDisabledAsync(ev.ConduitId, ev.ShardId, CancellationToken.None); });` and set the orchestrator's `OpenReplacementSessionAsync` to a method that uses `_shardManager` to create a fresh shard on that replica and return its session.

- [ ] **Step 4: Run → PASS.** Build + full test → green.
- [ ] **Step 5: DO NOT COMMIT.**

---

## PHASE B2 — Redundancy wiring

### Task 8: `RedundancyFactor` validation

**Files:**
- Modify: `Twitch EventSub library/EventSubClientOptions.cs`
- Create: a validator (`IValidateOptions<EventSubClientOptions>`) or `ValidateOnStart` rule in `ServiceCollectionExtensions.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/RedundancyOptionsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancyOptionsTests
{
    [Fact]
    public void RedundancyFactor_ExceedingMaxConduits_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "x"; o.AppAccessToken = "y"; o.MaxConduits = 2; o.RedundancyFactor = 3; })
            .ValidateDataAnnotations()
            .Validate(o => o.RedundancyFactor <= o.MaxConduits, "RedundancyFactor must be <= MaxConduits")
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void RedundancyFactor_WithinLimits_Passes()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "x"; o.AppAccessToken = "y"; o.MaxConduits = 3; o.RedundancyFactor = 2; })
            .ValidateDataAnnotations()
            .Validate(o => o.RedundancyFactor <= o.MaxConduits, "RedundancyFactor must be <= MaxConduits")
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Equal(2, provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value.RedundancyFactor);
    }
}
```

- [ ] **Step 2: Run → FAIL** if `RedundancyFactor` missing (added in Task 3) — if already present this asserts the validation rule wiring. The cross-field `.Validate(...)` must also be added to the real DI in `ServiceCollectionExtensions.AddTwitchEventSub` so production enforces it.

- [ ] **Step 3: Implement** — in `ServiceCollectionExtensions.AddTwitchEventSub`, after `.Configure(configure)`, add `.Validate(o => o.RedundancyFactor <= o.MaxConduits, "RedundancyFactor must be <= MaxConduits")`. Confirm `[Range(1,3)]` on the property (Task 3).

- [ ] **Step 4: Run → PASS.** Build + full test → green.
- [ ] **Step 5: DO NOT COMMIT.**

### Task 9: `EventProvider` runs N UserSequencers per identity

**Files:**
- Modify: `Twitch EventSub library/User/EventProvider.cs`
- Modify: `Twitch EventSub library/EventSubClient.cs` (pass RedundancyFactor)
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/RedundancyProviderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancyProviderTests
{
    [Fact]
    public async Task StartAsync_AcquiresOneShardPerReplica()
    {
        var shardManager = new Mock<IShardManager>();
        shardManager.Setup(m => m.GetOrCreateShardForUserAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IShardBinding>(b => b.SessionId == "s" && b.ShardStream == System.Reactive.Linq.Observable.Empty<ShardInbound>()));
        var conduit = new Mock<IConduitOrchestrator>();
        conduit.SetupGet(c => c.ConduitIds).Returns(new[] { "A", "B" });
        conduit.Setup(c => c.ConduitIdAt(It.IsAny<int>())).Returns<int>(i => i == 0 ? "A" : "B");

        var provider = new EventProvider("123", "tok", new List<SubscriptionTypes>(), "cid",
            NullLogger.Instance, allowRecovery: false, new TwitchApi(Mock.Of<IHttpClientFactory>()),
            conduit.Object, "app", shardManager.Object, new ReplayProtection(100),
            new MessagePipeline(NullLogger<MessagePipeline>.Instance), keepAliveTimeoutSeconds: 10,
            redundancyFactor: 2);

        await provider.StartAsync();

        shardManager.Verify(m => m.GetOrCreateShardForUserAsync("123", 0, It.IsAny<CancellationToken>()), Times.Once);
        shardManager.Verify(m => m.GetOrCreateShardForUserAsync("123", 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run → FAIL** (`EventProvider` has no `redundancyFactor` param; holds one sequencer).

- [ ] **Step 3: Implement**

`EventProvider`: add ctor param `int redundancyFactor` (store). Replace the single `_userSequencer` with `UserSequencer[] _sequencers` of length `redundancyFactor` (and matching `_shardBinding[]`, `_bindingApplied[]`). `Create()` builds N sequencers; the i-th uses a `SubscriptionManager` bound to `_conduitOrchestrator.ConduitIdAt(i)`. NOTE: today the conduit id is supplied to `RunCheckAsync` via `_conduitOrchestrator.ConduitId` inside `UserSequencer`; since that property is gone, pass the per-replica conduit id into each `UserSequencer` (add a `string conduitId` ctor param to `UserSequencer`, used in its `RunCheckAsync` calls instead of reading `_conduitOrchestrator.ConduitId`). `StartAsync`: for i in 0..N-1, `binding[i] = await _shardManager.GetOrCreateShardForUserAsync(_userId, i, ct)`, apply to sequencer i, `_messagePipeline.RegisterUser(_userId, handler)` once (the pipeline already keys by userId; all replicas feed the same user handler — the shared dedup gate collapses duplicates). Start all sequencers. `StopAsync`: stop all; `ReleaseUserFromShardAsync(_userId, i, ct)` for each i; `UnregisterUser` once. `IsConnected` = any sequencer connected. Token refresh + recovery operate per sequencer.

IMPORTANT pipeline note: `MessagePipeline.RegisterUser` keys by userId and a single handler. With N sequencers, register ONE handler that forwards to whichever sequencer should process — but since all N replicas carry the same events and dedup is by event-key downstream, simplest correct design: the handler forwards to a single "primary" processing path (sequencer 0's `HandleInboundAsync`) AND the dedup gate is shared, so duplicates from replicas 1..N-1 (which arrive on their own shard streams, also routed to userId) are collapsed. BUT each replica's shard stream is attached separately by ShardManager, all calling the same registered userId handler. So register one handler = `inbound => _sequencers[0].HandleInboundAsync(inbound)` — every replica's copy flows through sequencer 0's dedup. Sequencers 1..N-1 still exist to (a) hold their own subscription lifecycle on their replica and (b) keep their shard alive; they do NOT each process messages. Document this clearly. (This keeps one dedup path per identity.)

`EventSubClient.AddUserAsync`: pass `_options...RedundancyFactor` (store `_redundancyFactor` from options in ctor) into `new EventProvider(...)`.

- [ ] **Step 4: Run → PASS.** Build + full test → green. Update Phase6/Phase7 `EventProvider` construction helpers to pass `redundancyFactor: 1` and `UserSequencer` construction (if any test news it directly) to pass the new `conduitId` param.
- [ ] **Step 5: DO NOT COMMIT.**

---

## PHASE B3 — Two-layer dedup

### Task 10: `EventKey` content hash

**Files:**
- Create: `Twitch EventSub library/CoreFunctions/EventKey.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/EventKeyTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class EventKeyTests
{
    private static WebSocketNotificationMessage Msg(string broadcaster, string startedAt, string conduitId) => new()
    {
        Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = System.Guid.NewGuid().ToString(), MessageTimestamp = "2026-01-01T00:00:00Z", SubscriptionType = "stream.online", SubscriptionVersion = "1" },
        Payload = new WebSocketNotificationPayload
        {
            Subscription = new WebSocketSubscription { Type = "stream.online", Version = "1", Condition = new Condition { BroadcasterUserId = broadcaster },
                Transport = new WebSocketTransport { Method = "conduit", ConduitId = conduitId } },
            Event = new StreamOnlineEvent { BroadcasterUserId = broadcaster, StartedAt = startedAt }
        }
    };

    [Fact]
    public void SameEvent_DifferentConduitAndMessageId_ProducesSameKey()
    {
        var a = EventKey.Compute(Msg("1", "2026-01-01T10:00:00Z", "conduit-A"));
        var b = EventKey.Compute(Msg("1", "2026-01-01T10:00:00Z", "conduit-B"));
        Assert.Equal(a, b);   // transport/conduit and message_id must NOT affect the key
    }

    [Fact]
    public void DifferentEvent_ProducesDifferentKey()
    {
        var a = EventKey.Compute(Msg("1", "2026-01-01T10:00:00Z", "conduit-A"));
        var b = EventKey.Compute(Msg("1", "2026-01-01T11:00:00Z", "conduit-A"));
        Assert.NotEqual(a, b);
    }
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement**

Create `Twitch EventSub library/CoreFunctions/EventKey.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Stable content hash of a notification used to collapse redundant cross-conduit deliveries
/// (which carry different message_ids and different conduit transports for the same real event).
/// Key = SHA256( type | version | condition-json | event-json ). Transport/conduit and metadata are excluded.
/// </summary>
public static class EventKey
{
    private static readonly JsonSerializerSettings Canonical = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver()
    };

    public static string Compute(WebSocketNotificationMessage notification)
    {
        var sub = notification.Payload?.Subscription;
        var type = sub?.Type ?? notification.Metadata?.SubscriptionType ?? "";
        var version = sub?.Version ?? notification.Metadata?.SubscriptionVersion ?? "";
        var conditionJson = JsonConvert.SerializeObject(sub?.Condition, Canonical);
        var eventJson = JsonConvert.SerializeObject(notification.Payload?.Event, Canonical);

        var raw = $"{type}|{version}|{conditionJson}|{eventJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
```
NOTE on stability: Newtonsoft serializes properties in declaration order, deterministic for the same type, so two identical events serialize identically. Condition excludes transport (which differs per conduit). If the condition object happened to include conduit-specific data it must be excluded — `Condition` does not, so it is safe.

- [ ] **Step 4: Run → PASS.** (If `StreamOnlineEvent`/`WebSocketTransport` property names differ, read the files and fix the test initializer — keep the same intent.) Build + full test → green.
- [ ] **Step 5: DO NOT COMMIT.**

### Task 11: `ReplayProtection.IsDuplicateEvent` + UserSequencer notification dedup

**Files:**
- Modify: `Twitch EventSub library/CoreFunctions/ReplayProtection.cs`
- Modify: `Twitch EventSub library/User/UserSequencer.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/EventDedupTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class EventDedupTests
{
    [Fact]
    public void IsDuplicateEvent_SecondSameKey_True_FirstFalse()
    {
        var rp = new ReplayProtection(100);
        Assert.False(rp.IsDuplicateEvent("key-1"));
        Assert.True(rp.IsDuplicateEvent("key-1"));
        Assert.False(rp.IsDuplicateEvent("key-2"));
    }
}
```

- [ ] **Step 2: Run → FAIL** (`IsDuplicateEvent` missing).

- [ ] **Step 3: Implement**

In `ReplayProtection.cs` add a second window mirroring the message-id one (its own `ConcurrentDictionary<string,long>` + the same eviction-before-add logic, `_maxSize`-bounded), method `public bool IsDuplicateEvent(string eventKey)` with identical semantics to `IsDuplicate`.

In `UserSequencer.ProcessWebSocketMessageAsync`, in the notification branch only (after passing the message_id + timestamp gate), compute and check the event key:
```csharp
case WebSocketNotificationMessage notificationMessage:
    var eventKey = EventKey.Compute(notificationMessage);
    if (_replayProtection.IsDuplicateEvent(eventKey))
    {
        _logger.LogDebug("[UserSequencer] deduped redundant cross-conduit copy {EventKey}", eventKey);
        return;
    }
    await NotificationMessageProcessingAsync(notificationMessage);
    return;
```
The raw callback still fires before any dedup (unchanged). Non-notification frames unaffected. `using Twitch.EventSub.CoreFunctions;` already present.

- [ ] **Step 4: Run → PASS.** Build + full test → green.
- [ ] **Step 5: DO NOT COMMIT.**

### Task 12: Synthetic redundancy test (end-to-end collapse)

**Files:**
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/RedundancySyntheticTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancySyntheticTests
{
    // Models the production path: two replica shard streams → pipeline (one user handler) →
    // a dedup gate keyed by EventKey → count of user-visible deliveries.
    [Fact]
    public async Task SameEventViaTwoReplicas_DeliveredOnce()
    {
        var rp = new ReplayProtection(100);
        int delivered = 0;
        Task Handler(ShardInbound i)
        {
            if (i.Parsed is WebSocketNotificationMessage n)
            {
                var key = EventKey.Compute(n);
                if (rp.IsDuplicateEvent(key)) return Task.CompletedTask;
                Interlocked.Increment(ref delivered);
            }
            return Task.CompletedTask;
        }

        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        pipeline.RegisterUser("1", Handler);
        var replicaA = new Subject<ShardInbound>();
        var replicaB = new Subject<ShardInbound>();
        pipeline.Attach(replicaA);
        pipeline.Attach(replicaB);

        ShardInbound Copy(string conduit, string msgId) => new("{}", new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = msgId, MessageTimestamp = System.DateTime.UtcNow.ToString("o"), SubscriptionType = "stream.online", SubscriptionVersion = "1" },
            Payload = new WebSocketNotificationPayload
            {
                Subscription = new WebSocketSubscription { Type = "stream.online", Version = "1", Condition = new Condition { BroadcasterUserId = "1" }, Transport = new WebSocketTransport { Method = "conduit", ConduitId = conduit } },
                Event = new StreamOnlineEvent { BroadcasterUserId = "1", StartedAt = "2026-01-01T10:00:00Z" }
            }
        });

        replicaA.OnNext(Copy("conduit-A", "msg-A"));
        replicaB.OnNext(Copy("conduit-B", "msg-B"));   // same real event, different conduit + message_id
        await Task.Delay(80);

        Assert.Equal(1, delivered);
    }
}
```

- [ ] **Step 2: Run → PASS** (relies on Tasks 8/10/11). If it shows 2, the event-key is incorporating conduit/transport — fix `EventKey`.
- [ ] **Step 3: DO NOT COMMIT.**

### Task 13: Redundancy fuzz test

**Files:**
- Test: `TwitchEventSub_Websocket.Tests/Phase8Tests/RedundancyFuzzTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;
using Xunit.Abstractions;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancyFuzzTests
{
    private readonly ITestOutputHelper _out;
    public RedundancyFuzzTests(ITestOutputHelper o) => _out = o;

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task RedundantDeliveries_CollapseToDistinctEvents(int seed)
    {
        var rng = new Random(seed);
        var rp = new ReplayProtection(1000);
        var delivered = new HashSet<string>();
        var expected = new HashSet<string>();
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        pipeline.RegisterUser("1", i =>
        {
            if (i.Parsed is WebSocketNotificationMessage n)
            {
                var key = EventKey.Compute(n);
                if (!rp.IsDuplicateEvent(key)) lock (delivered) delivered.Add(((StreamOnlineEvent)n.Payload!.Event!).StartedAt);
            }
            return Task.CompletedTask;
        });
        var a = new Subject<ShardInbound>(); var b = new Subject<ShardInbound>();
        pipeline.Attach(a); pipeline.Attach(b);

        ShardInbound Ev(string startedAt, string conduit) => new("{}", new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = System.Guid.NewGuid().ToString(), MessageTimestamp = System.DateTime.UtcNow.ToString("o"), SubscriptionType = "stream.online", SubscriptionVersion = "1" },
            Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Type = "stream.online", Version = "1", Condition = new Condition { BroadcasterUserId = "1" }, Transport = new WebSocketTransport { Method = "conduit", ConduitId = conduit } }, Event = new StreamOnlineEvent { BroadcasterUserId = "1", StartedAt = startedAt } }
        });

        for (int i = 0; i < 400; i++)
        {
            var startedAt = $"2026-01-01T{rng.Next(0, 24):00}:{rng.Next(0, 60):00}:00Z";
            expected.Add(startedAt);
            // deliver via A always, via B sometimes (redundant copy)
            a.OnNext(Ev(startedAt, "conduit-A"));
            if (rng.Next(2) == 0) b.OnNext(Ev(startedAt, "conduit-B"));
        }
        await Task.Delay(300);

        Assert.Equal(expected, delivered);   // every distinct event delivered exactly once, no duplicates
        _out.WriteLine($"seed={seed} distinct={expected.Count}");
    }
}
```

- [ ] **Step 2: Run → PASS** (5 seeds). Then full suite.
- [ ] **Step 3: DO NOT COMMIT.**

---

## PHASE B-final — verification + harness

### Task 14: Harness RedundancyFactor knob + final verification

**Files:**
- Modify: `TwitchEventSub.LiveHarness/Program.cs`

- [ ] **Step 1:** In `Program.cs`, read `RedundancyFactor` from env (`HARNESS_REDUNDANCY`, default 1) and set `o.RedundancyFactor` in `AddTwitchEventSub`. Add a startup log line of the value.
- [ ] **Step 2:** Kill harness, `dotnet build` solution → 0 errors. `dotnet test "TwitchEventSubWebsocket.sln" -c Debug` → ALL pass; report exact total (155 baseline + new Phase8).
- [ ] **Step 3:** Live smoke (user runs): `HARNESS_REDUNDANCY=1` → behaves like Spec A (events deliver, raw fires, clean teardown deletes 1 conduit). Then `HARNESS_REDUNDANCY=2` → log shows "deduped redundant cross-conduit copy" lines; teardown deletes 2 conduits. Verify via API that 0 conduits remain afterward.
- [ ] **Step 4:** Update `docs/superpowers/specs/2026-05-31-full-codebase-audit.md`: mark #6 (shard.disabled + recovery), redundancy, and event-key dedup as implemented in Spec B.
- [ ] **Step 5: DO NOT COMMIT.**

---

## Self-review notes (addressed)
- **Spec coverage:** B1 replicas (T1–T5), orphan reconcile/#11 (T5), B4 condition fix + routing + recovery (T6–T7), B2 validation + N providers (T8–T9), B3 EventKey + IsDuplicateEvent + dedup path (T10–T11), synthetic (T12), fuzz (T13), harness/live (T14).
- **Pipeline-with-N-replicas resolution:** Task 9 documents the one-handler-per-userId design (replica streams all route to userId's handler → sequencer 0 → shared dedup). Sequencers 1..N-1 own their replica's subscription lifecycle/shard; they do not each process. This keeps a single dedup path and avoids double-processing.
- **Type consistency:** `ConduitReplica` (ReserveShardSlot/CommitShard/PlanRemoval/ApplyRemoval/UpdateShardSession), `SessionIdUpdatedArgs.ReplicaIndex`, `IShardManager.GetOrCreateShardForUserAsync(userId,replicaIndex,ct)`, `IConduitOrchestrator` (ConduitIds/ConduitIdAt/replica-addressed shard ops/HandleShardDisabledAsync), `MessagePipeline.RegisterPlatformHandler`, `EventKey.Compute`, `ReplayProtection.IsDuplicateEvent`, `EventProvider(... redundancyFactor)`, `UserSequencer(... conduitId ...)` used consistently.
- **Interface blast radius flagged** (T2–T4, T9): every signature change pairs with its test-update step. `UserSequencer` gains a `conduitId` ctor param (replaces removed `IConduitOrchestrator.ConduitId` read) — called out in T9.
- **Open risk for executor:** the recovery seam (`OpenReplacementSessionAsync`) crosses orchestrator↔ShardManager; T7 defines it as an injectable delegate set by the composition root so it stays unit-testable. If wiring proves awkward, escalate rather than coupling the two managers directly.
