# Conduit Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the Twitch EventSub WebSocket library from per-user WebSocket connections to a Conduit-based architecture where multiple users share parallel WebSocket shards.

**Architecture:** A new `ShardSequencer` owns each WebSocket connection; `ShardManager` allocates users across shards using a `SemaphoreSlim`-protected capacity check. `UserSequencer` retains all per-user state machine logic but receives messages via `IShardBinding` instead of owning a socket. `ConduitOrchestrator` keeps the Twitch Conduit record in sync with live shard `session_id`s. `EventRouter` deduplicates and routes messages by `broadcaster_user_id` to the correct `EventProvider`.

**Tech Stack:** .NET 10, C# 14, Stateless 5.x, Websocket.Client 5.x, System.Reactive (RxNET), Microsoft.Extensions.Http.Resilience, xUnit, Moq, WireMock.Net

> **Type note:** The spec refers to "ParsedMessage" conceptually. The actual type in this codebase is `WebSocketMessage` (the abstract base class in `Twitch.EventSub.Messages`). Everywhere the spec says `ParsedMessage`, use `WebSocketMessage`.

---

## File Map

### New files (library — `Twitch EventSub library/`)
| File | Responsibility |
|---|---|
| `CoreFunctions/IShardBinding.cs` | Contract between ShardManager and UserSequencer |
| `CoreFunctions/ShardCloseArgs.cs` | Event args for shard close code events |
| `CoreFunctions/SessionIdUpdatedArgs.cs` | Event args for session_id change notifications |
| `CoreFunctions/ShardContext.cs` | Internal holder: ShardSequencer + user assignment list |
| `CoreFunctions/ShardSequencer.cs` | WebSocket lifecycle state machine (no user knowledge) |
| `CoreFunctions/ShardManager.cs` | Allocates users to shards, surfaces session_id events |
| `CoreFunctions/ShardBinding.cs` | Internal IShardBinding implementation |
| `CoreFunctions/EventRouter.cs` | Routes messages by broadcaster_user_id/user_id via callbacks |
| `API/ConduitOrchestrator.cs` | Keeps Twitch Conduit record in sync with live shards |
| `APIConduit/ITwitchConduitApi.cs` | Interface over TwitchApiConduit for testability |
| `IShardManager.cs` | Public interface for ShardManager |
| `IConduitOrchestrator.cs` | Public interface for ConduitOrchestrator |
| `IEventRouter.cs` | Public interface for EventRouter |

### Modified files (library)
| File | Changes |
|---|---|
| `EventSubClientOptions.cs` | Add `AppAccessToken`, all timeout fields, `[Required][MinLength(1)]`, `[Range]` |
| `User/UserBase.cs` | Remove `Socket` field + `Socket.Dispose()`, remove `Url` WebSocket construction, add `protected abstract Task ReconnectingEntryAsync()` |
| `User/UserSequencer.cs` | Replace WebSocket ownership with `IShardBinding`; wire `Reconnecting` entry; update watchdog fallback |
| `User/SubscriptionManager.cs` | Change transport from `session_id` to `conduit_id`; use app access token |
| `EventSubClient.cs` | Implement `IHostedService` + `IAsyncDisposable`; delegate to `ShardManager` + `ConduitOrchestrator` |
| `ServiceCollectionExtensions.cs` | Register all new singletons; add `IHostedService` |
| `CoreFunctions/ReplayProtection.cs` | Replace `Queue<string>` with `ConcurrentDictionary`-based thread-safe implementation |
| `SubsRegister/Register.cs` | Fix `RegUserUpdate` condition: `ClientId` → `UserId` |

### New test files (`TwitchEventSub_Websocket.Tests/`)
| File | Covers |
|---|---|
| `Phase1Tests/OptionsValidationTests.cs` | Options [Required], [MinLength], [Range] enforced at startup |
| `Phase1Tests/ReplayProtectionTests.cs` | Thread-safe deduplication under concurrent access |
| `Phase2Tests/ShardSequencerTests.cs` | All 7 close codes → correct state transitions; dual-client reconnect |
| `Phase2Tests/ShardManagerTests.cs` | Shard creation, capacity checks, concurrent safety, dispose-on-empty |
| `Phase3Tests/UserSequencerShardBindingTests.cs` | ShardLost trigger, OnSessionIdChanged → ReconnectSuccess, watchdog fallback |
| `Phase4Tests/ConduitOrchestratorTests.cs` | Conduit reuse on restart, shard swap order, teardown sequence |
| `Phase4Tests/EventSubClientHostedServiceTests.cs` | IHostedService startup/teardown ordering |
| `Phase5Tests/EventRouterTests.cs` | Routing by broadcaster_user_id, user_id, deduplication, revocation |

---

## Phase 1 — Protocol Hardening & Options

### Task 1.1: Harden `EventSubClientOptions`

**Files:**
- Modify: `Twitch EventSub library/EventSubClientOptions.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase1Tests/OptionsValidationTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase1Tests/OptionsValidationTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase1Tests;

public class OptionsValidationTests
{
    [Fact]
    public void ClientId_Empty_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = ""; o.AppAccessToken = "valid"; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void AppAccessToken_Missing_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "valid"; o.AppAccessToken = ""; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void KeepaliveSeconds_OutOfRange_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "x"; o.AppAccessToken = "y"; o.KeepaliveTimeoutSeconds = 5; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void ValidOptions_PassesValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "myClientId"; o.AppAccessToken = "myAppToken"; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value;
        Assert.Equal("myClientId", opts.ClientId);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (compilation error: `AppAccessToken` does not exist)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~OptionsValidationTests" -v minimal
```

Expected: build error — `AppAccessToken` not found on `EventSubClientOptions`.

- [ ] **Step 3: Implement `EventSubClientOptions`**

Replace `Twitch EventSub library/EventSubClientOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Twitch.EventSub;

public record EventSubClientOptions
{
    /// <summary>Your Twitch application client ID.</summary>
    [Required]
    [MinLength(1)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>App access token for conduit management and subscription creation.</summary>
    [Required]
    [MinLength(1)]
    public string AppAccessToken { get; set; } = string.Empty;

    /// <summary>Keepalive timeout in seconds (10–600). Sent to Twitch WebSocket URL.</summary>
    [Range(10, 600)]
    public int KeepaliveTimeoutSeconds { get; set; } = 10;

    /// <summary>Operator ceiling on shard count per conduit. Verify Twitch hard limit before raising.</summary>
    [Range(1, int.MaxValue)]
    public int MaxShardsPerConduit { get; set; } = 10;

    public TimeSpan WelcomeMessageTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan AccessTokenValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan SubscriptionOperationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan WatchdogTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan ReconnectGraceTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~OptionsValidationTests" -v minimal
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add "Twitch EventSub library/EventSubClientOptions.cs" TwitchEventSub_Websocket.Tests/Phase1Tests/OptionsValidationTests.cs
git commit -m "feat(phase1): harden EventSubClientOptions with required fields and validation"
```

---

### Task 1.2: Fix `Register.cs` user.update condition bug

**Files:**
- Modify: `Twitch EventSub library/SubsRegister/Register.cs` (line ~693)

- [ ] **Step 1: Write a failing assertion test**

Create `TwitchEventSub_Websocket.Tests/Phase1Tests/UserUpdateConditionTests.cs`:

```csharp
using Twitch.EventSub.SubsRegister;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase1Tests;

public class UserUpdateConditionTests
{
    [Fact]
    public void UserUpdate_Subscription_UsesUserIdCondition_NotClientId()
    {
        // Twitch spec: user.update requires user_id condition, not client_id.
        // Regression guard for Register.cs bug at line ~693.
        var reg = Register.GetUserUpdateSubscription();
        Assert.Contains(reg.Conditions, c => c == "user_id");
        Assert.DoesNotContain(reg.Conditions, c => c == "client_id");
    }
}
```

> Note: `Register.GetUserUpdateSubscription()` is a test-helper method you will add to `Register.cs` as `internal static` returning the `RegUserUpdate` subscription info. If `Register` already exposes the data another way, adapt accordingly.

- [ ] **Step 2: Run test — expect FAIL** (method does not exist or condition is wrong)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~UserUpdateConditionTests" -v minimal
```

- [ ] **Step 3: Fix the condition in `Register.cs`**

In `Twitch EventSub library/SubsRegister/Register.cs` find `RegUserUpdate` (line ~693) and change:

```csharp
// Before
Conditions = CondList(ConditionTypes.ClientId)

// After — Twitch EventSub reference: user.update requires user_id condition
Conditions = CondList(ConditionTypes.UserId)
```

Also add the test helper method if needed:

```csharp
internal static SubscriptionDefinition GetUserUpdateSubscription()
    => RegUserUpdate;
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~UserUpdateConditionTests|FullyQualifiedName~RegistryTests" -v minimal
```

Expected: both test classes pass.

- [ ] **Step 5: Commit**

```bash
git add "Twitch EventSub library/SubsRegister/Register.cs" TwitchEventSub_Websocket.Tests/Phase1Tests/UserUpdateConditionTests.cs
git commit -m "fix: correct user.update subscription condition from ClientId to UserId per Twitch spec"
```

---

### Task 1.3: Thread-safe `ReplayProtection`

**Files:**
- Modify: `Twitch EventSub library/CoreFunctions/ReplayProtection.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase1Tests/ReplayProtectionTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase1Tests/ReplayProtectionTests.cs`:

```csharp
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase1Tests;

public class ReplayProtectionTests
{
    [Fact]
    public void IsDuplicate_SameId_ReturnsTrueOnSecondCall()
    {
        var rp = new ReplayProtection(100);
        Assert.False(rp.IsDuplicate("msg-001"));
        Assert.True(rp.IsDuplicate("msg-001"));
    }

    [Fact]
    public void IsDuplicate_DifferentIds_ReturnsFalse()
    {
        var rp = new ReplayProtection(100);
        Assert.False(rp.IsDuplicate("msg-001"));
        Assert.False(rp.IsDuplicate("msg-002"));
    }

    [Fact]
    public void IsDuplicate_ConcurrentCalls_DedupesExactlyOnce()
    {
        var rp = new ReplayProtection(200);
        const string messageId = "concurrent-msg-001";
        int seenCount = 0;

        Parallel.For(0, 20, _ =>
        {
            if (!rp.IsDuplicate(messageId))
                Interlocked.Increment(ref seenCount);
        });

        Assert.Equal(1, seenCount);
    }

    [Fact]
    public void IsDuplicate_BeyondCapacity_OldestEvicted()
    {
        var rp = new ReplayProtection(3);
        rp.IsDuplicate("a");
        rp.IsDuplicate("b");
        rp.IsDuplicate("c");
        // "a" should be evicted; adding it again should return false
        Assert.False(rp.IsDuplicate("a"));
    }

    [Fact]
    public void IsUpToDate_RecentTimestamp_ReturnsTrue()
    {
        var rp = new ReplayProtection(100);
        var recent = DateTime.UtcNow.ToString("O");
        Assert.True(rp.IsUpToDate(recent));
    }

    [Fact]
    public void IsUpToDate_OldTimestamp_ReturnsFalse()
    {
        var rp = new ReplayProtection(100);
        var old = DateTime.UtcNow.AddMinutes(-11).ToString("O");
        Assert.False(rp.IsUpToDate(old));
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (concurrency test fails with old `Queue<string>`)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ReplayProtectionTests" -v minimal
```

Expected: `IsDuplicate_ConcurrentCalls_DedupesExactlyOnce` fails (seenCount > 1).

- [ ] **Step 3: Implement thread-safe `ReplayProtection`**

Replace `Twitch EventSub library/CoreFunctions/ReplayProtection.cs`:

```csharp
using System.Collections.Concurrent;
using System.Globalization;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Thread-safe replay protection. Shared singleton across all shards.
/// Tracks the last N message IDs atomically to detect duplicates from
/// parallel shard delivery. Timestamp validation follows Twitch spec: reject
/// messages older than 10 minutes.
/// </summary>
public class ReplayProtection
{
    private static readonly string _format = "MM/dd/yyyy HH:mm:ss";
    private readonly int _maxSize;
    // Key = messageId, Value = insertion-order counter for eviction
    private readonly ConcurrentDictionary<string, long> _seen = new();
    private long _counter;
    private readonly object _evictionLock = new();

    public ReplayProtection(int messagesToRemember)
    {
        _maxSize = messagesToRemember;
    }

    /// <summary>
    /// Returns true if this message ID has been seen before (duplicate).
    /// Thread-safe: concurrent calls with the same ID return true for all but the first.
    /// </summary>
    public bool IsDuplicate(string messageId)
    {
        // TryAdd returns true only for the first caller with this key — atomic.
        long order = Interlocked.Increment(ref _counter);
        if (!_seen.TryAdd(messageId, order))
            return true;

        // Evict oldest entry if over capacity.
        if (_seen.Count > _maxSize)
        {
            lock (_evictionLock)
            {
                while (_seen.Count > _maxSize)
                {
                    var oldest = _seen.OrderBy(kv => kv.Value).First();
                    _seen.TryRemove(oldest.Key, out _);
                }
            }
        }
        return false;
    }

    /// <summary>Returns true if the timestamp is within the last 10 minutes (Twitch spec).</summary>
    public bool IsUpToDate(string timestamp)
    {
        var messageTime = ParseDateTimeString(timestamp);
        return (DateTime.UtcNow - messageTime) < TimeSpan.FromMinutes(10);
    }

    public static DateTime ParseDateTimeString(string timestamp)
    {
        if (DateTime.TryParse(timestamp, null, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToUniversalTime();
        if (DateTime.TryParseExact(timestamp, _format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
            return dt2.ToUniversalTime();
        throw new Exception("[EventSubClient] - [ReplayProtection] Parsed Invalid date");
    }
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ReplayProtectionTests" -v minimal
```

Expected: 6 tests pass.

- [ ] **Step 5: Run full test suite — still green**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add "Twitch EventSub library/CoreFunctions/ReplayProtection.cs" TwitchEventSub_Websocket.Tests/Phase1Tests/ReplayProtectionTests.cs
git commit -m "feat(phase1): thread-safe ReplayProtection with ConcurrentDictionary, capacity 100+"
```

---

## Phase 2 — ShardSequencer & ShardManager

### Task 2.1: Supporting types

**Files:**
- Create: `Twitch EventSub library/CoreFunctions/ShardCloseArgs.cs`
- Create: `Twitch EventSub library/CoreFunctions/SessionIdUpdatedArgs.cs`
- Create: `Twitch EventSub library/CoreFunctions/IShardBinding.cs`

- [ ] **Step 1: Create `ShardCloseArgs.cs`**

```csharp
namespace Twitch.EventSub.CoreFunctions;

public class ShardCloseArgs : EventArgs
{
    public string ShardId { get; init; } = string.Empty;
    public int? CloseCode { get; init; }
    public string? Reason { get; init; }
}
```

- [ ] **Step 2: Create `SessionIdUpdatedArgs.cs`**

```csharp
namespace Twitch.EventSub.CoreFunctions;

public class SessionIdUpdatedArgs : EventArgs
{
    public string ShardId { get; init; } = string.Empty;
    public string? OldSessionId { get; init; }
    /// <summary>Null means shard was removed.</summary>
    public string? NewSessionId { get; init; }
}
```

- [ ] **Step 3: Create `IShardBinding.cs`**

```csharp
using Twitch.EventSub.Messages;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Decouples UserSequencer from WebSocket ownership.
/// Created by ShardManager; held by UserSequencer.
/// UserMessages provides a pre-filtered stream of WebSocketMessages for this user's
/// broadcaster_user_id (category A) and user_id (category B).
/// </summary>
public interface IShardBinding
{
    string ShardId { get; }
    string SessionId { get; }
    /// <summary>Pre-filtered message stream for this user's broadcaster_user_id / user_id.</summary>
    IObservable<WebSocketMessage> UserMessages { get; }
    /// <summary>Fired when the shard WebSocket goes down unexpectedly.</summary>
    event EventHandler OnShardLost;
    /// <summary>Fired when a reconnect completes and a new session_id is available.</summary>
    event EventHandler<string> OnSessionIdChanged;
}
```

- [ ] **Step 4: Build to verify compilation**

```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add "Twitch EventSub library/CoreFunctions/ShardCloseArgs.cs" "Twitch EventSub library/CoreFunctions/SessionIdUpdatedArgs.cs" "Twitch EventSub library/CoreFunctions/IShardBinding.cs"
git commit -m "feat(phase2): add ShardCloseArgs, SessionIdUpdatedArgs, IShardBinding supporting types"
```

---

### Task 2.2: `ShardSequencer`

**Files:**
- Create: `Twitch EventSub library/CoreFunctions/ShardSequencer.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase2Tests/ShardSequencerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `TwitchEventSub_Websocket.Tests/Phase2Tests/ShardSequencerTests.cs`:

```csharp
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase2Tests;

public class ShardSequencerTests
{
    [Theory]
    [InlineData(4000)]   // ServerError → reconnect
    [InlineData(4002)]   // PingPongFailure → reconnect
    [InlineData(4003)]   // SubscriptionTimeout → reconnect
    [InlineData(4005)]   // NetworkTimeout → reconnect
    [InlineData(4006)]   // NetworkError → reconnect
    [InlineData(4007)]   // InvalidReconnect → reconnect
    public async Task CloseCode_Reconnectable_TransitionsToReconnecting(int code)
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();
        await shard.HandleCloseCodeAsync(code);
        // HandleCloseCodeAsync fires ReconnectRequested: Active → Reconnecting (not Connecting)
        Assert.Equal(ShardSequencer.ShardState.Reconnecting, shard.State);
    }

    [Fact]
    public async Task CloseCode_4001_TransitionsToDisposing_NotConnecting()
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();
        await shard.HandleCloseCodeAsync(4001);
        // 4001 = client protocol violation — must NOT reconnect
        Assert.NotEqual(ShardSequencer.ShardState.Connecting, shard.State);
        Assert.NotEqual(ShardSequencer.ShardState.Active, shard.State);
    }

    [Fact]
    public async Task CloseCode_4004_ForceFreshConnectFired()
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();
        bool freshConnectFired = false;
        shard.OnForceFreshConnect += (_, _) => freshConnectFired = true;
        await shard.HandleCloseCodeAsync(4004);
        Assert.True(freshConnectFired);
    }

    [Fact]
    public async Task WelcomeReceived_TransitionsToActive()
    {
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateConnectingForTestAsync();
        await shard.HandleWelcomeAsync("session-abc");
        Assert.Equal(ShardSequencer.ShardState.Active, shard.State);
        Assert.Equal("session-abc", shard.SessionId);
    }

    [Fact]
    public async Task DualClientReconnect_OldClientDisposedOnlyAfterNewWelcome()
    {
        // Spec-compliant reconnect: new WebsocketClient opened; old disposed only after new Welcome.
        var shard = new ShardSequencer("shard-1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await shard.SimulateActiveForTestAsync();

        // Transition to Reconnecting
        await shard.SimulateReconnectingForTestAsync();
        Assert.Equal(ShardSequencer.ShardState.Reconnecting, shard.State);

        // Old session still visible during reconnect (not yet replaced)
        Assert.Equal("test-session", shard.SessionId);

        // New connection Welcome arrives
        await shard.HandleNewConnectionWelcomeAsync("session-new");
        Assert.Equal(ShardSequencer.ShardState.Active, shard.State);
        Assert.Equal("session-new", shard.SessionId);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (`ShardSequencer` does not exist)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ShardSequencerTests" -v minimal
```

Expected: build error — `ShardSequencer` not found.

- [ ] **Step 3: Create `ShardSequencer.cs`**

Create `Twitch EventSub library/CoreFunctions/ShardSequencer.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Stateless;
using System.Reactive.Subjects;
using Twitch.EventSub.Messages;
using Twitch.EventSub.User;
using Websocket.Client;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Owns exactly one WebSocket connection (plus a pending one during reconnect).
/// Pure WebSocket lifecycle state machine — no knowledge of users, tokens, or subscriptions.
/// Uses Stateless for structured state transitions and OnTransitioned logging.
/// </summary>
public class ShardSequencer : IAsyncDisposable
{
    public enum ShardState { Disconnected, Connecting, WaitingForWelcome, Active, Reconnecting, Disposing, Disposed }
    public enum ShardTrigger { Connect, WelcomeReceived, ReconnectRequested, NewConnectionWelcome, ForceFresh, Terminate }

    private readonly string _shardId;
    private readonly ILogger _logger;
    private readonly StateMachine<ShardState, ShardTrigger> _machine;
    private readonly Subject<WebSocketMessage> _messages = new();
    private IDisposable? _activeMessageSub;
    private IDisposable? _activeDisconnectSub;
    private IDisposable? _pendingMessageSub;
    private WebsocketClient? _activeClient;
    private WebsocketClient? _pendingClient;

    public string? SessionId { get; private set; }
    public string ShardId => _shardId;
    public ShardState State => _machine.State;
    public IObservable<WebSocketMessage> Messages => _messages;

    public event EventHandler<ShardCloseArgs>? OnClosed;
    public event EventHandler? OnForceFreshConnect;

    public ShardSequencer(string shardId, ILogger logger)
    {
        _shardId = shardId;
        _logger = logger;
        _machine = new StateMachine<ShardState, ShardTrigger>(ShardState.Disconnected);
        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        _machine.Configure(ShardState.Disconnected)
            .Permit(ShardTrigger.Connect, ShardState.WaitingForWelcome);

        _machine.Configure(ShardState.WaitingForWelcome)
            .Permit(ShardTrigger.WelcomeReceived, ShardState.Active);

        _machine.Configure(ShardState.Active)
            .Permit(ShardTrigger.ReconnectRequested, ShardState.Reconnecting)
            .Permit(ShardTrigger.ForceFresh, ShardState.Connecting)
            .Permit(ShardTrigger.Terminate, ShardState.Disposing);

        _machine.Configure(ShardState.Connecting)
            .Permit(ShardTrigger.WelcomeReceived, ShardState.Active);

        _machine.Configure(ShardState.Reconnecting)
            .Permit(ShardTrigger.NewConnectionWelcome, ShardState.Active)
            .Permit(ShardTrigger.ForceFresh, ShardState.Connecting)
            .Permit(ShardTrigger.Terminate, ShardState.Disposing);

        _machine.Configure(ShardState.Disposing)
            .Permit(ShardTrigger.Terminate, ShardState.Disposed);

        _machine.OnTransitioned(t => _logger.LogInformation(
            "Shard {ShardId}: {Source} → {Dest} trigger={Trigger}",
            _shardId, t.Source, t.Destination, t.Trigger));
    }

    public async Task ConnectAsync(Uri uri, CancellationToken ct)
    {
        _activeClient = CreateClient(uri);
        SubscribeToClient(_activeClient, isPending: false);
        await _activeClient.Start();
        await _machine.FireAsync(ShardTrigger.Connect);
    }

    public async Task HandleWelcomeAsync(string sessionId)
    {
        SessionId = sessionId;
        await _machine.FireAsync(ShardTrigger.WelcomeReceived);
        _logger.LogInformation("Shard {ShardId} Welcome session={SessionId}", _shardId, sessionId);
    }

    public async Task HandleReconnectAsync(Uri reconnectUrl, CancellationToken ct)
    {
        await _machine.FireAsync(ShardTrigger.ReconnectRequested);
        _pendingClient = CreateClient(reconnectUrl);
        SubscribeToClient(_pendingClient, isPending: true);
        await _pendingClient.Start();
        // Wait for Welcome on new connection via HandleNewConnectionWelcomeAsync
    }

    public async Task HandleNewConnectionWelcomeAsync(string newSessionId)
    {
        var oldClient = _activeClient;
        var oldMsgSub = _activeMessageSub;
        var oldDiscSub = _activeDisconnectSub;

        _activeClient = _pendingClient;
        _activeMessageSub = _pendingMessageSub;
        _activeDisconnectSub = null;
        _pendingClient = null;
        _pendingMessageSub = null;

        SessionId = newSessionId;
        await _machine.FireAsync(ShardTrigger.NewConnectionWelcome);
        _logger.LogInformation("Shard {ShardId} reconnect complete newSession={SessionId}", _shardId, newSessionId);

        // Dispose old client ONLY after new Welcome received (spec-compliant)
        oldMsgSub?.Dispose();
        oldDiscSub?.Dispose();
        if (oldClient != null)
        {
            await oldClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Replaced by new connection");
            oldClient.Dispose();
        }
    }

    public async Task HandleCloseCodeAsync(int code)
    {
        _logger.LogWarning("Shard {ShardId} close code {Code}", _shardId, code);
        switch (code)
        {
            case 4001:
                _logger.LogCritical("Shard {ShardId} close code 4001 (client protocol violation) — NOT reconnecting. This is a library bug.", _shardId);
                OnClosed?.Invoke(this, new ShardCloseArgs { ShardId = _shardId, CloseCode = code, Reason = "ClientProtocolViolation" });
                await _machine.FireAsync(ShardTrigger.Terminate);
                break;
            case 4004:
                _logger.LogWarning("Shard {ShardId} close code 4004 (reconnect grace expired) — forcing fresh connect", _shardId);
                OnForceFreshConnect?.Invoke(this, EventArgs.Empty);
                await _machine.FireAsync(ShardTrigger.ForceFresh);
                break;
            default:
                // 4000, 4002, 4003, 4005, 4006, 4007 — all reconnectable
                await _machine.FireAsync(ShardTrigger.ReconnectRequested);
                OnClosed?.Invoke(this, new ShardCloseArgs { ShardId = _shardId, CloseCode = code });
                break;
        }
    }

    private WebsocketClient CreateClient(Uri uri)
    {
        return new WebsocketClient(uri) { IsReconnectionEnabled = false };
    }

    private void SubscribeToClient(WebsocketClient client, bool isPending)
    {
        var msgSub = client.MessageReceived
            .Subscribe(msg =>
            {
                if (msg.MessageType == System.Net.WebSockets.WebSocketMessageType.Text && msg.Text != null)
                {
                    try
                    {
                        // Deserialize and publish to the messages Subject
                        var parsed = MessageProcessing.DeserializeMessageAsync(msg.Text).GetAwaiter().GetResult();
                        if (parsed == null) return;

                        _logger.LogDebug("Shard {ShardId} message type={Type} isPending={IsPending}",
                            _shardId, parsed.Metadata?.MessageType, isPending);

                        // Active connection messages go to subscribers; pending only for reconnect Welcome
                        if (!isPending)
                            _messages.OnNext(parsed);
                        // isPending messages are handled by the reconnect flow via HandleNewConnectionWelcomeAsync
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Shard {ShardId} failed to deserialize message", _shardId);
                    }
                }
            });

        var discSub = client.DisconnectionHappened
            .Subscribe(info =>
            {
                if (!isPending)
                    _logger.LogWarning("Shard {ShardId} disconnected: {Type}", _shardId, info.Type);
            });

        if (isPending)
        {
            _pendingMessageSub = msgSub;
        }
        else
        {
            _activeMessageSub?.Dispose();
            _activeDisconnectSub?.Dispose();
            _activeMessageSub = msgSub;
            _activeDisconnectSub = discSub;
        }
    }

    // Test helpers — bypass network, advance state machine directly
    internal async Task SimulateActiveForTestAsync()
    {
        await _machine.FireAsync(ShardTrigger.Connect);   // Disconnected → WaitingForWelcome
        SessionId = "test-session";
        await _machine.FireAsync(ShardTrigger.WelcomeReceived);  // WaitingForWelcome → Active
    }

    internal async Task SimulateConnectingForTestAsync()
    {
        await _machine.FireAsync(ShardTrigger.Connect);   // Disconnected → WaitingForWelcome
    }

    internal async Task SimulateReconnectingForTestAsync()
    {
        await _machine.FireAsync(ShardTrigger.ReconnectRequested);  // Active → Reconnecting
    }

    public async ValueTask DisposeAsync()
    {
        if (_machine.State == ShardState.Disposed) return;
        _activeMessageSub?.Dispose();
        _activeDisconnectSub?.Dispose();
        _pendingMessageSub?.Dispose();
        _messages.OnCompleted();
        if (_activeClient != null) { await _activeClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Disposed"); _activeClient.Dispose(); }
        if (_pendingClient != null) { await _pendingClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Disposed"); _pendingClient.Dispose(); }
        if (_machine.CanFire(ShardTrigger.Terminate))
            await _machine.FireAsync(ShardTrigger.Terminate);
    }
}
```

> **Note:** `MessageProcessing.DeserializeMessageAsync` exists at `Twitch EventSub library/User/MessageProcessing.cs`. Verify the method is `public static` and accessible from `CoreFunctions`. If it has `internal` access, move it or add an `InternalsVisibleTo` attribute.

- [ ] **Step 4: Run test — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ShardSequencerTests" -v minimal
```

Expected: 9 tests pass.

- [ ] **Step 5: Commit**

```bash
git add "Twitch EventSub library/CoreFunctions/ShardSequencer.cs" TwitchEventSub_Websocket.Tests/Phase2Tests/ShardSequencerTests.cs
git commit -m "feat(phase2): add ShardSequencer WebSocket lifecycle state machine with Stateless and close code handling"
```

---

### Task 2.3: `ShardContext` + `ShardManager`

**Files:**
- Create: `Twitch EventSub library/CoreFunctions/ShardContext.cs`
- Create: `Twitch EventSub library/CoreFunctions/ShardManager.cs`
- Create: `Twitch EventSub library/CoreFunctions/ShardBinding.cs`
- Create: `Twitch EventSub library/IShardManager.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase2Tests/ShardManagerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `TwitchEventSub_Websocket.Tests/Phase2Tests/ShardManagerTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase2Tests;

public class ShardManagerTests
{
    private static IOptions<EventSubClientOptions> DefaultOptions() =>
        Options.Create(new EventSubClientOptions { ClientId = "test", AppAccessToken = "token", MaxShardsPerConduit = 5 });

    [Fact]
    public async Task GetOrCreateShard_FirstUser_CreatesOneShard()
    {
        var manager = new ShardManager(DefaultOptions(), NullLogger<ShardManager>.Instance);
        var binding = await manager.GetOrCreateShardForUserAsync("user-1", CancellationToken.None);
        Assert.NotNull(binding);
        Assert.Equal(1, manager.ShardCount);
    }

    [Fact]
    public async Task ReleaseUser_LastUserOnShard_ShardDisposed()
    {
        var manager = new ShardManager(DefaultOptions(), NullLogger<ShardManager>.Instance);
        await manager.GetOrCreateShardForUserAsync("user-1", CancellationToken.None);
        Assert.Equal(1, manager.ShardCount);
        await manager.ReleaseUserFromShardAsync("user-1", CancellationToken.None);
        Assert.Equal(0, manager.ShardCount);
    }

    [Fact]
    public async Task ConcurrentAddUsers_DoNotExceedMaxShards()
    {
        var opts = Options.Create(new EventSubClientOptions { ClientId = "test", AppAccessToken = "token", MaxShardsPerConduit = 2 });
        var manager = new ShardManager(opts, NullLogger<ShardManager>.Instance);
        var tasks = Enumerable.Range(0, 10)
            .Select(i => manager.GetOrCreateShardForUserAsync($"user-{i}", CancellationToken.None));
        await Task.WhenAll(tasks);
        Assert.True(manager.ShardCount <= 2);
    }

    [Fact]
    public async Task SessionIdUpdated_FiredWhenSimulatedActive()
    {
        var manager = new ShardManager(DefaultOptions(), NullLogger<ShardManager>.Instance);
        SessionIdUpdatedArgs? received = null;
        manager.OnSessionIdUpdated += (_, args) => received = args;
        await manager.GetOrCreateShardForUserAsync("user-1", CancellationToken.None);
        manager.SimulateSessionIdUpdatedForTest("user-1", "session-xyz");
        Assert.NotNull(received);
        Assert.Equal("session-xyz", received!.NewSessionId);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (`ShardManager` does not exist)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ShardManagerTests" -v minimal
```

- [ ] **Step 3: Create `IShardManager.cs`**

Create `Twitch EventSub library/IShardManager.cs`:

```csharp
using Twitch.EventSub.CoreFunctions;

namespace Twitch.EventSub;

public interface IShardManager : IAsyncDisposable
{
    Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, CancellationToken ct);
    Task ReleaseUserFromShardAsync(string userId, CancellationToken ct);
    IReadOnlyList<(string ShardId, string? SessionId)> ActiveSessionIds { get; }
    event EventHandler<SessionIdUpdatedArgs> OnSessionIdUpdated;
}
```

- [ ] **Step 4: Create `ShardContext.cs`**

Create `Twitch EventSub library/CoreFunctions/ShardContext.cs`:

```csharp
namespace Twitch.EventSub.CoreFunctions;

/// <summary>Internal: associates a ShardSequencer with its assigned user IDs.</summary>
internal class ShardContext
{
    public ShardSequencer Sequencer { get; }
    public HashSet<string> UserIds { get; } = new();

    public ShardContext(ShardSequencer sequencer)
    {
        Sequencer = sequencer;
    }
}
```

- [ ] **Step 5: Create `ShardManager.cs`**

Create `Twitch EventSub library/CoreFunctions/ShardManager.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Allocates users across WebSocket shards. All user-assignment operations
/// are serialized by a SemaphoreSlim(1,1) to prevent concurrent capacity violations.
/// Capacity limit: MaxShardsPerConduit controls the number of shards (not users per shard).
/// </summary>
public class ShardManager : IShardManager, IAsyncDisposable
{
    private readonly EventSubClientOptions _options;
    private readonly ILogger<ShardManager> _logger;
    private readonly ConcurrentDictionary<string, ShardContext> _shards = new();
    private readonly ConcurrentDictionary<string, string> _userToShard = new();  // userId → shardId
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _shardCounter;

    public event EventHandler<SessionIdUpdatedArgs>? OnSessionIdUpdated;

    public ShardManager(IOptions<EventSubClientOptions> options, ILogger<ShardManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<(string ShardId, string? SessionId)> ActiveSessionIds =>
        _shards.Select(kv => (kv.Key, kv.Value.Sequencer.SessionId)).ToList();

    /// <summary>Current number of active shards. Exposed for tests and monitoring.</summary>
    public int ShardCount => _shards.Count;

    public async Task<IShardBinding> GetOrCreateShardForUserAsync(string userId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Find an existing shard that has room, OR create a new one if under MaxShardsPerConduit.
            // MaxShardsPerConduit limits the NUMBER OF SHARDS, not users-per-shard.
            // Multiple users share a shard; the per-shard user count grows until a new shard is needed.
            // For now: pack all users into the first available shard, create new only when MaxShardsPerConduit
            // would be exceeded. Adjust per-shard user limit separately if needed.
            var existing = _shards.Values.FirstOrDefault();

            ShardContext ctx;
            if (existing != null)
            {
                ctx = existing;
            }
            else if (_shards.Count < _options.MaxShardsPerConduit)
            {
                var shardId = $"shard-{Interlocked.Increment(ref _shardCounter)}";
                var sequencer = new ShardSequencer(shardId, _logger);
                ctx = new ShardContext(sequencer);
                _shards[shardId] = ctx;
                _logger.LogInformation("ShardManager created new shard {ShardId} (total={Count})", shardId, _shards.Count);
            }
            else
            {
                // All shards at max; use least-loaded shard
                ctx = _shards.Values.OrderBy(s => s.UserIds.Count).First();
                _logger.LogWarning("ShardManager at MaxShardsPerConduit={Max}; user {UserId} assigned to least-loaded shard", _options.MaxShardsPerConduit, userId);
            }

            ctx.UserIds.Add(userId);
            _userToShard[userId] = ctx.Sequencer.ShardId;

            return new ShardBinding(ctx.Sequencer, userId, this);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReleaseUserFromShardAsync(string userId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_userToShard.TryRemove(userId, out var shardId)) return;
            if (!_shards.TryGetValue(shardId, out var ctx)) return;

            ctx.UserIds.Remove(userId);
            if (ctx.UserIds.Count == 0)
            {
                _shards.TryRemove(shardId, out _);
                _logger.LogInformation("ShardManager disposed empty shard {ShardId}", shardId);
                OnSessionIdUpdated?.Invoke(this, new SessionIdUpdatedArgs
                {
                    ShardId = shardId,
                    OldSessionId = ctx.Sequencer.SessionId,
                    NewSessionId = null
                });
                await ctx.Sequencer.DisposeAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    internal void NotifySessionIdUpdated(string shardId, string? oldSession, string? newSession)
    {
        OnSessionIdUpdated?.Invoke(this, new SessionIdUpdatedArgs
        {
            ShardId = shardId,
            OldSessionId = oldSession,
            NewSessionId = newSession
        });
    }

    // Test helper
    internal void SimulateSessionIdUpdatedForTest(string userId, string sessionId)
    {
        if (_userToShard.TryGetValue(userId, out var shardId))
            NotifySessionIdUpdated(shardId, null, sessionId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var ctx in _shards.Values)
            await ctx.Sequencer.DisposeAsync();
        _shards.Clear();
        _lock.Dispose();
    }
}
```

- [ ] **Step 6: Create `ShardBinding.cs`** (internal implementation of `IShardBinding`)

Create `Twitch EventSub library/CoreFunctions/ShardBinding.cs`:

```csharp
using System.Reactive.Linq;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Internal implementation of IShardBinding created by ShardManager per user.
/// UserMessages filters the shard's message stream to only messages for this user:
///   Category A: Payload.Subscription.Condition.BroadcasterUserId == userId
///   Category B: Payload.Subscription.Condition.UserId == userId
/// </summary>
internal class ShardBinding : IShardBinding
{
    private readonly ShardSequencer _sequencer;
    private readonly string _userId;
    private readonly ShardManager _manager;

    public string ShardId => _sequencer.ShardId;
    public string SessionId => _sequencer.SessionId ?? string.Empty;

    public IObservable<WebSocketMessage> UserMessages => _sequencer.Messages
        .Where(msg => IsForUser(msg, _userId));

    public event EventHandler? OnShardLost;
    public event EventHandler<string>? OnSessionIdChanged;

    public ShardBinding(ShardSequencer sequencer, string userId, ShardManager manager)
    {
        _sequencer = sequencer;
        _userId = userId;
        _manager = manager;

        _sequencer.OnClosed += (_, _) => OnShardLost?.Invoke(this, EventArgs.Empty);
        _manager.OnSessionIdUpdated += (_, args) =>
        {
            if (args.ShardId == _sequencer.ShardId && args.NewSessionId != null)
                OnSessionIdChanged?.Invoke(this, args.NewSessionId);
        };
    }

    private static bool IsForUser(WebSocketMessage msg, string userId)
    {
        if (msg is not WebSocketNotificationMessage notification) return false;
        var condition = notification.Payload?.Subscription?.Condition;
        if (condition == null) return false;
        // Category A: broadcaster_user_id matches
        if (condition.BroadcasterUserId == userId) return true;
        // Category B: user_id matches (whispers, user.update, etc.)
        if (condition.UserId == userId) return true;
        return false;
    }
}
```

> **Type check:** `WebSocketNotificationMessage` is in `Twitch.EventSub.Messages.NotificationMessage`. `Condition` is `Twitch.EventSub.API.Models.Condition`. Verify these namespaces match what's in the project.

- [ ] **Step 7: Run tests — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ShardManagerTests" -v minimal
```

Expected: 4 tests pass.

- [ ] **Step 8: Commit**

```bash
git add "Twitch EventSub library/CoreFunctions/ShardContext.cs" "Twitch EventSub library/CoreFunctions/ShardManager.cs" "Twitch EventSub library/CoreFunctions/ShardBinding.cs" "Twitch EventSub library/IShardManager.cs" TwitchEventSub_Websocket.Tests/Phase2Tests/ShardManagerTests.cs
git commit -m "feat(phase2): add ShardContext, ShardManager, ShardBinding with SemaphoreSlim capacity control"
```

---

## Phase 3 — Decouple UserSequencer from WebSocket

### Task 3.1: Refactor `UserBase`

**Files:**
- Modify: `Twitch EventSub library/User/UserBase.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase3Tests/UserSequencerShardBindingTests.cs` (pre-written here, run before and after)

- [ ] **Step 1: Write a failing test for the Socket field removal**

Create `TwitchEventSub_Websocket.Tests/Phase3Tests/UserSequencerShardBindingTests.cs`:

```csharp
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase3Tests;

public class UserSequencerShardBindingTests
{
    [Fact]
    public void UserBase_HasNoSocketField()
    {
        // After refactor: UserBase must NOT own a Socket.
        // Fail first (Socket field exists), then pass after removal.
        var field = typeof(Twitch.EventSub.User.UserBase)
            .GetField("Socket", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.Null(field);
    }

    [Fact]
    public void UserBase_HasReconnectingEntryAsync_AbstractMethod()
    {
        var method = typeof(Twitch.EventSub.User.UserBase)
            .GetMethod("ReconnectingEntryAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.True(method!.IsAbstract);
    }

    [Fact]
    public void UserBase_HasAwaitShardReadyAsync_AbstractMethod()
    {
        var method = typeof(Twitch.EventSub.User.UserBase)
            .GetMethod("AwaitShardReadyAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.True(method!.IsAbstract);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (`Socket` field still exists)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~UserSequencerShardBindingTests" -v minimal
```

- [ ] **Step 3: Modify `UserBase`**

In `Twitch EventSub library/User/UserBase.cs` make the following changes:

**a) Remove from constructor:**
```csharp
// Remove these lines from constructor:
Url = new Uri(url ?? DefaultWebSocketUrl);
Socket = new WebsocketClient(Url);
Socket.IsReconnectionEnabled = false;
```

**b) Remove these fields/properties:**
```csharp
// Remove:
public Uri Url { get; set; }
public WebsocketClient Socket { get; set; }
```

**c) Remove `Socket.Dispose()` from `DisposeProcedureAsync`:**
```csharp
// Before:
private async Task DisposeProcedureAsync()
{
    Socket.Dispose();  // ← remove this line
    await ManagerCancelationSource.CancelAsync();
    ...
}
```

**d) Add `ReconnectingEntryAsync` abstract method and wire it in `StateMachineCofiguration`:**
```csharp
// In StateMachineCofiguration, add OnEntryAsync to Reconnecting state:
machine.Configure(UserState.Reconnecting)
    .OnEntryAsync(ReconnectingEntryAsync)   // ← add this
    .Permit(UserActions.ReconnectSuccess, UserState.Running)
    .Permit(UserActions.ReconnectFail, UserState.Failing)
    .Permit(UserActions.Stop, UserState.Stoping)
    .Permit(UserActions.Fail, UserState.Failing)
    .Permit(UserActions.HandShakeFail, UserState.Failing)
    .Permit(UserActions.WebsocketFail, UserState.Failing);

// Add abstract method declaration (alongside existing abstracts):
protected abstract Task ReconnectingEntryAsync();
```

**e) Replace `RunWebsocketAsync` with `AwaitShardReadyAsync`:**
```csharp
// Before:
machine.Configure(UserState.Websocket)
    .OnEntryAsync(RunWebsocketAsync)
    ...

// After:
machine.Configure(UserState.Websocket)
    .OnEntryAsync(AwaitShardReadyAsync)
    .Permit(UserActions.WebsocketSuccess, UserState.WellcomeMessage)
    .Permit(UserActions.WebsocketFail, UserState.Failing);

// Replace abstract:
protected abstract Task AwaitShardReadyAsync();
// Remove:
// protected abstract Task RunWebsocketAsync();
```

**f) Add `OnTransitioned` logging to UserBase state machine:**
```csharp
// In StateMachineCofiguration, after all Configure calls:
machine.OnTransitioned(t =>
    _logger?.LogInformation(
        "User {UserId} transition: {Source} → {Dest} trigger={Trigger}",
        UserId, t.Source, t.Destination, t.Trigger));
```

- [ ] **Step 4: Build — fix any compilation errors**

```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
```

Fix compilation errors in `UserSequencer.cs` (it extends `UserBase`) as they surface.

- [ ] **Step 5: Run test — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~UserSequencerShardBindingTests" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add "Twitch EventSub library/User/UserBase.cs" TwitchEventSub_Websocket.Tests/Phase3Tests/UserSequencerShardBindingTests.cs
git commit -m "refactor(phase3): remove WebSocket ownership from UserBase; add ReconnectingEntryAsync and AwaitShardReadyAsync abstract methods"
```

---

### Task 3.2: Refactor `UserSequencer`

**Files:**
- Modify: `Twitch EventSub library/User/UserSequencer.cs`

- [ ] **Step 1: Update `UserSequencer.cs`**

Key changes:

**a) Add `IShardBinding` field and `SetShardBinding()` method:**
```csharp
private IShardBinding? _shardBinding;

public void SetShardBinding(IShardBinding binding)
{
    _shardBinding = binding;
    _shardBinding.OnShardLost += async (_, _) =>
    {
        if (StateMachine.CanFire(UserActions.WebsocketFail))
            await StateMachine.FireAsync(UserActions.WebsocketFail);
    };
    _shardBinding.OnSessionIdChanged += (_, newId) =>
    {
        SessionId = newId;
    };
}
```

**b) Implement `ReconnectingEntryAsync`** (await new session_id within grace timeout; unsubscribe handler in all paths):
```csharp
protected override async Task ReconnectingEntryAsync()
{
    if (_shardBinding == null) { await StateMachine.FireAsync(UserActions.ReconnectFail); return; }

    var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    EventHandler<string>? handler = null;
    handler = (_, newSessionId) =>
    {
        _shardBinding.OnSessionIdChanged -= handler;  // always unsubscribe
        tcs.TrySetResult(newSessionId);
    };
    _shardBinding.OnSessionIdChanged += handler;

    using var cts = new CancellationTokenSource(_options.ReconnectGraceTimeout);
    cts.Token.Register(() =>
    {
        _shardBinding.OnSessionIdChanged -= handler;  // unsubscribe on timeout too
        tcs.TrySetCanceled();
    });

    try
    {
        var newSession = await tcs.Task;
        SessionId = newSession;
        await StateMachine.FireAsync(UserActions.ReconnectSuccess);
    }
    catch
    {
        // Handler already unsubscribed in the timeout registration above
        await StateMachine.FireAsync(UserActions.ReconnectFail);
    }
}
```

**c) Implement `AwaitShardReadyAsync`** (replaces `RunWebsocketAsync`):
```csharp
protected override async Task AwaitShardReadyAsync()
{
    if (_shardBinding?.SessionId is { Length: > 0 })
    {
        SessionId = _shardBinding.SessionId;
        await StateMachine.FireAsync(UserActions.WebsocketSuccess);
    }
    else
    {
        _logger.LogWarning("[UserSequencer] AwaitShardReadyAsync: no binding or empty session for {UserId}", UserId);
        await StateMachine.FireAsync(UserActions.WebsocketFail);
    }
}
```

**d) Remove `Socket.Send("Pong")` from `PingMessageProcessingAsync`** — protocol-level ping/pong is handled automatically by `Websocket.Client`. Application-level `session_ping` still returns `session_pong` — keep that logic but remove direct socket references.

**e) Update `ReconnectingAfterWatchdogFailAsync`** — replace socket calls with shard signal:
```csharp
protected override async Task ReconnectingAfterWatchdogFailAsync()
{
    _logger.LogWarning("[UserSequencer] Watchdog triggered for {UserId} — signalling shard lost", UserId);
    if (StateMachine.CanFire(UserActions.AccessTesting))
        await StateMachine.FireAsync(UserActions.AccessTesting);
}
```

**f) Update `OnWatchdogTimeoutAsync` fallback** — remove `Socket.Stop()` and `Socket.Dispose()`:
```csharp
// Replace Socket.Stop(...) and Socket.Dispose() calls with:
_logger.LogWarning("[UserSequencer] Watchdog fallback for {UserId} — no valid state for reconnect recovery", UserId);
if (OnOutsideDisconnectAsync != null)
    await OnOutsideDisconnectAsync.TryInvoke(this, e);
_watchdog.Stop();
_watchdog.OnWatchdogTimeout -= OnWatchdogTimeoutAsync;
```

- [ ] **Step 2: Build and fix compilation errors**

```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
```

- [ ] **Step 3: Run Phase 3 tests**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~UserSequencerShardBindingTests" -v minimal
```

Expected: 3 tests pass.

- [ ] **Step 4: Run full suite**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" -v minimal
```

- [ ] **Step 5: Commit**

```bash
git add "Twitch EventSub library/User/UserSequencer.cs"
git commit -m "refactor(phase3): UserSequencer uses IShardBinding; remove direct WebSocket ownership; fix handler unsubscribe"
```

---

### Task 3.3: Retarget `SubscriptionManager` to conduit transport

**Files:**
- Modify: `Twitch EventSub library/User/SubscriptionManager.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase3Tests/SubscriptionManagerConduitTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase3Tests/SubscriptionManagerConduitTests.cs`:

```csharp
using Twitch.EventSub.API.Models;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase3Tests;

public class SubscriptionManagerConduitTests
{
    [Fact]
    public void BuildSubscriptionRequest_UsesConduitTransport()
    {
        // SubscriptionManager must use method="conduit" and conduit_id, NOT session_id.
        // This test guards against the old WebSocket session transport being used.
        var transport = new Transport
        {
            Method = "conduit",
            ConduitId = "conduit-abc",
            SessionId = null
        };
        Assert.Equal("conduit", transport.Method);
        Assert.Equal("conduit-abc", transport.ConduitId);
        Assert.Null(transport.SessionId);
    }

    [Fact]
    public void RunCheckAsync_Signature_AcceptsConduitIdAndAppToken()
    {
        // Verify SubscriptionManager.RunCheckAsync accepts conduitId and appAccessToken.
        // If this fails to compile, the method signature has not been updated.
        var method = typeof(SubscriptionManager).GetMethod("RunCheckAsync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var paramNames = method!.GetParameters().Select(p => p.Name).ToArray();
        Assert.Contains("conduitId", paramNames);
        Assert.Contains("appAccessToken", paramNames);
        Assert.DoesNotContain("sessionId", paramNames);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (method signature still has sessionId)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~SubscriptionManagerConduitTests" -v minimal
```

- [ ] **Step 3: Update transport in `SubscriptionManager.cs`**

In `RunCheckAsync`, replace session transport with conduit transport:

```csharp
foreach (var typeListOfSub in requestedSubscriptions)
{
    typeListOfSub.Transport.Method = "conduit";
    typeListOfSub.Transport.ConduitId = conduitId;   // new parameter
    typeListOfSub.Transport.SessionId = null;
}
```

Update the method signature to accept `conduitId` instead of `sessionId`, and `appAccessToken` instead of per-user `accessToken`:

```csharp
public async Task<bool> RunCheckAsync(
    string userId,
    List<CreateSubscriptionRequest> requestedSubscriptions,
    string clientId,
    string appAccessToken,    // was: string accessToken (user token)
    string conduitId,         // was: string sessionId
    CancellationTokenSource clSource,
    ILogger logger)
```

Update call sites in `UserSequencer.cs` to pass `_conduitId` (sourced from `IConduitOrchestrator`) and app access token from options.

> **Note:** `Transport.ConduitId` already exists in `API/Models/Transport.cs` — no need to add it.

- [ ] **Step 4: Build**

```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
```

- [ ] **Step 5: Run test — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~SubscriptionManagerConduitTests" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add "Twitch EventSub library/User/SubscriptionManager.cs" TwitchEventSub_Websocket.Tests/Phase3Tests/SubscriptionManagerConduitTests.cs
git commit -m "refactor(phase3): SubscriptionManager uses conduit transport and app access token"
```

---

## Phase 4 — Conduit Layer

### Task 4.1: `ConduitOrchestrator`

**Files:**
- Create: `Twitch EventSub library/APIConduit/ITwitchConduitApi.cs`
- Create: `Twitch EventSub library/IConduitOrchestrator.cs`
- Create: `Twitch EventSub library/API/ConduitOrchestrator.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase4Tests/ConduitOrchestratorTests.cs`

- [ ] **Step 1: Create `ITwitchConduitApi.cs`** (interface for testability)

Create `Twitch EventSub library/APIConduit/ITwitchConduitApi.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Twitch.EventSub.APIConduit.Models.Responses;

namespace Twitch.EventSub.APIConduit;

/// <summary>
/// Abstraction over TwitchApiConduit for testability.
/// ConduitOrchestrator depends on this interface, not the concrete class.
/// </summary>
public interface ITwitchConduitApi
{
    /// <summary>Lists existing conduit IDs for this application.</summary>
    Task<List<string>> GetConduitIdsAsync(string appAccessToken, string clientId, CancellationToken ct);

    /// <summary>Creates a new conduit with one shard. Returns the conduit ID.</summary>
    Task<string> CreateConduitAsync(string appAccessToken, string clientId, CancellationToken ct);

    /// <summary>Updates a shard's WebSocket session_id on the conduit.</summary>
    Task UpdateConduitShardSessionAsync(string conduitId, string shardId, string sessionId, string appAccessToken, string clientId, CancellationToken ct);

    /// <summary>Deletes the conduit (subscriptions are automatically removed by Twitch).</summary>
    Task DeleteConduitAsync(string conduitId, string appAccessToken, string clientId, CancellationToken ct);
}
```

- [ ] **Step 2: Add `GetConduitIdsAsync` to `TwitchApiConduit`** and implement `ITwitchConduitApi`

In `Twitch EventSub library/APIConduit/TwitchConduitApi.cs`:

```csharp
// Add to class declaration:
public class TwitchApiConduit : ITwitchConduitApi

// Implement interface methods using actual TwitchApiConduit method names:
public async Task<List<string>> GetConduitIdsAsync(string appAccessToken, string clientId, CancellationToken ct)
{
    // GET /eventsub/conduits — returns list of conduits for this app token
    // Implement via HttpClientFactory, similar to other methods in this class
    // Returns empty list if no conduits exist
    throw new NotImplementedException("Add HTTP call to GET /eventsub/conduits");
}

public async Task<string> CreateConduitAsync(string appAccessToken, string clientId, CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    var response = await ConduitCreatorAsync(appAccessToken, clientId, cts, NullLogger.Instance, inicialSize: 1);
    return response?.Data?.FirstOrDefault()?.Id
        ?? throw new InvalidOperationException("ConduitCreatorAsync returned null conduit");
}

public async Task UpdateConduitShardSessionAsync(string conduitId, string shardId, string sessionId, string appAccessToken, string clientId, CancellationToken ct)
{
    // PATCH /eventsub/conduits/shards — update shard transport
    // Use the existing HTTP infrastructure in this class
    throw new NotImplementedException("Add HTTP call to PATCH /eventsub/conduits/shards");
}

public async Task DeleteConduitAsync(string conduitId, string appAccessToken, string clientId, CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    await ConduitDeleteAsync(appAccessToken, clientId, cts, NullLogger.Instance, conduitId);
}
```

> **Important:** The two `NotImplementedException` methods require adding HTTP calls following the existing pattern in `TwitchConduitApi.cs`. Study existing methods (e.g., `ConduitCreatorAsync`) to understand the HTTP client usage pattern, then implement accordingly. These are required for production; the tests mock the interface so they pass without real HTTP.

- [ ] **Step 3: Write the failing tests**

Create `TwitchEventSub_Websocket.Tests/Phase4Tests/ConduitOrchestratorTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Twitch.EventSub.API;
using Twitch.EventSub.APIConduit;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase4Tests;

public class ConduitOrchestratorTests
{
    [Fact]
    public async Task Initialize_ExistingConduit_ReusesItWithoutCreatingNew()
    {
        var apiMock = new Mock<ITwitchConduitApi>();
        apiMock.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<string> { "existing-conduit-id" });

        var orchestrator = new ConduitOrchestrator(apiMock.Object, "client-id", "app-token", NullLogger<ConduitOrchestrator>.Instance);
        await orchestrator.InitializeAsync(CancellationToken.None);

        apiMock.Verify(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("existing-conduit-id", orchestrator.ConduitId);
    }

    [Fact]
    public async Task Initialize_NoExistingConduit_CreatesNew()
    {
        var apiMock = new Mock<ITwitchConduitApi>();
        apiMock.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<string>());
        apiMock.Setup(a => a.CreateConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("new-conduit-id");

        var orchestrator = new ConduitOrchestrator(apiMock.Object, "client-id", "app-token", NullLogger<ConduitOrchestrator>.Instance);
        await orchestrator.InitializeAsync(CancellationToken.None);

        apiMock.Verify(a => a.CreateConduitAsync("app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("new-conduit-id", orchestrator.ConduitId);
    }

    [Fact]
    public async Task Teardown_DeletesConduit()
    {
        var apiMock = new Mock<ITwitchConduitApi>();
        apiMock.Setup(a => a.GetConduitIdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<string> { "conduit-1" });
        apiMock.Setup(a => a.DeleteConduitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var orchestrator = new ConduitOrchestrator(apiMock.Object, "client-id", "app-token", NullLogger<ConduitOrchestrator>.Instance);
        await orchestrator.InitializeAsync(CancellationToken.None);
        await orchestrator.TeardownAsync(CancellationToken.None);

        // Twitch automatically deletes subscriptions when a conduit is deleted
        apiMock.Verify(a => a.DeleteConduitAsync("conduit-1", "app-token", "client-id", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 4: Run test — expect FAIL** (`ConduitOrchestrator` not found)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ConduitOrchestratorTests" -v minimal
```

- [ ] **Step 5: Create `IConduitOrchestrator.cs`**

```csharp
namespace Twitch.EventSub;

public interface IConduitOrchestrator
{
    Task InitializeAsync(CancellationToken ct);
    /// <summary>Register a new shard (stable shardId + its current sessionId) with the conduit.</summary>
    Task AddShardAsync(string shardId, string sessionId, CancellationToken ct);
    /// <summary>Update an existing shard's session (old → new sessionId).</summary>
    Task UpdateShardAsync(string shardId, string oldSessionId, string newSessionId, CancellationToken ct);
    /// <summary>Remove/disable a shard from the conduit.</summary>
    Task RemoveShardAsync(string shardId, CancellationToken ct);
    Task TeardownAsync(CancellationToken ct);
    string ConduitId { get; }
}
```

- [ ] **Step 6: Create `ConduitOrchestrator.cs`**

Create `Twitch EventSub library/API/ConduitOrchestrator.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Twitch.EventSub.APIConduit;

namespace Twitch.EventSub.API;

public class ConduitOrchestrator : IConduitOrchestrator
{
    private readonly ITwitchConduitApi _api;
    private readonly string _clientId;
    private readonly string _appAccessToken;
    private readonly ILogger<ConduitOrchestrator> _logger;

    public string ConduitId { get; private set; } = string.Empty;

    public ConduitOrchestrator(ITwitchConduitApi api, string clientId, string appAccessToken, ILogger<ConduitOrchestrator> logger)
    {
        _api = api;
        _clientId = clientId;
        _appAccessToken = appAccessToken;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        // Reuse existing conduit to avoid quota exhaustion on restart
        var existing = await _api.GetConduitIdsAsync(_appAccessToken, _clientId, ct);
        if (existing?.Count > 0)
        {
            ConduitId = existing[0];
            _logger.LogInformation("ConduitOrchestrator reusing existing conduit {ConduitId}", ConduitId);
            return;
        }
        ConduitId = await _api.CreateConduitAsync(_appAccessToken, _clientId, ct)
            ?? throw new InvalidOperationException("Failed to create conduit");
        _logger.LogInformation("ConduitOrchestrator created new conduit {ConduitId}", ConduitId);
    }

    public async Task AddShardAsync(string shardId, string sessionId, CancellationToken ct)
    {
        _logger.LogInformation("ConduitOrchestrator adding shard {ShardId} session={SessionId} to conduit {ConduitId}", shardId, sessionId, ConduitId);
        await _api.UpdateConduitShardSessionAsync(ConduitId, shardId, sessionId, _appAccessToken, _clientId, ct);
    }

    public async Task UpdateShardAsync(string shardId, string oldSessionId, string newSessionId, CancellationToken ct)
    {
        _logger.LogInformation("ConduitOrchestrator updating shard {ShardId} session {Old} → {New} on conduit {ConduitId}", shardId, oldSessionId, newSessionId, ConduitId);
        await _api.UpdateConduitShardSessionAsync(ConduitId, shardId, newSessionId, _appAccessToken, _clientId, ct);
    }

    public async Task RemoveShardAsync(string shardId, CancellationToken ct)
    {
        _logger.LogInformation("ConduitOrchestrator removing shard {ShardId} from conduit {ConduitId}", shardId, ConduitId);
        // Disable shard via PATCH /eventsub/conduits/shards with disabled transport
        _logger.LogWarning("ConduitOrchestrator RemoveShardAsync: implement shard disable via PATCH /eventsub/conduits/shards");
    }

    public async Task TeardownAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ConduitId)) return;
        _logger.LogInformation("ConduitOrchestrator teardown: deleting conduit {ConduitId} (Twitch auto-removes subscriptions)", ConduitId);
        // Twitch API: deleting a conduit automatically removes all associated subscriptions
        await _api.DeleteConduitAsync(ConduitId, _appAccessToken, _clientId, ct);
    }
}
```

- [ ] **Step 7: Run tests — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~ConduitOrchestratorTests" -v minimal
```

- [ ] **Step 8: Commit**

```bash
git add "Twitch EventSub library/APIConduit/ITwitchConduitApi.cs" "Twitch EventSub library/IConduitOrchestrator.cs" "Twitch EventSub library/API/ConduitOrchestrator.cs" TwitchEventSub_Websocket.Tests/Phase4Tests/ConduitOrchestratorTests.cs
git commit -m "feat(phase4): add ITwitchConduitApi, ConduitOrchestrator with conduit reuse and ordered teardown"
```

---

### Task 4.2: `EventSubClient` as `IHostedService` + `IAsyncDisposable`

**Files:**
- Modify: `Twitch EventSub library/EventSubClient.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase4Tests/EventSubClientHostedServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `TwitchEventSub_Websocket.Tests/Phase4Tests/EventSubClientHostedServiceTests.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Moq;
using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase4Tests;

public class EventSubClientHostedServiceTests
{
    [Fact]
    public void EventSubClient_ImplementsIHostedService()
    {
        Assert.True(typeof(IHostedService).IsAssignableFrom(typeof(EventSubClient)));
    }

    [Fact]
    public void EventSubClient_ImplementsIAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(EventSubClient)));
    }

    [Fact]
    public async Task StartAsync_CallsConduitOrchestratorInitialize()
    {
        var orchestratorMock = new Mock<IConduitOrchestrator>();
        orchestratorMock.Setup(o => o.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        orchestratorMock.SetupGet(o => o.ConduitId).Returns("conduit-1");

        var shardManagerMock = new Mock<IShardManager>();
        shardManagerMock.SetupAdd(m => m.OnSessionIdUpdated += null);

        // Construct EventSubClient with mocked dependencies
        // Adjust constructor call to match actual signature after update
        var client = CreateClientWithMocks(orchestratorMock.Object, shardManagerMock.Object);

        await ((IHostedService)client).StartAsync(CancellationToken.None);

        orchestratorMock.Verify(o => o.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_CallsConduitOrchestratorTeardown()
    {
        var orchestratorMock = new Mock<IConduitOrchestrator>();
        orchestratorMock.Setup(o => o.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        orchestratorMock.Setup(o => o.TeardownAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        orchestratorMock.SetupGet(o => o.ConduitId).Returns("conduit-1");

        var shardManagerMock = new Mock<IShardManager>();
        shardManagerMock.SetupAdd(m => m.OnSessionIdUpdated += null);
        shardManagerMock.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var client = CreateClientWithMocks(orchestratorMock.Object, shardManagerMock.Object);

        await ((IHostedService)client).StartAsync(CancellationToken.None);
        await ((IHostedService)client).StopAsync(CancellationToken.None);

        orchestratorMock.Verify(o => o.TeardownAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static EventSubClient CreateClientWithMocks(IConduitOrchestrator orchestrator, IShardManager shardManager)
    {
        // Adapt this to match EventSubClient's actual constructor signature after the update.
        // You may need to pass IOptions<EventSubClientOptions>, ILogger, TwitchApi as well.
        throw new NotImplementedException("Wire up EventSubClient constructor with mock arguments");
    }
}
```

> **Note:** `CreateClientWithMocks` must be filled in after reading `EventSubClient`'s new constructor. The test validates the IHostedService contract before the implementation.

- [ ] **Step 2: Run test — expect FAIL** (EventSubClient not yet IHostedService)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~EventSubClientHostedServiceTests" -v minimal
```

- [ ] **Step 3: Update `EventSubClient`**

```csharp
public class EventSubClient : IEventSubClient, IHostedService, IAsyncDisposable
{
    private readonly IConduitOrchestrator _conduitOrchestrator;
    private readonly IShardManager _shardManager;
    private readonly IEventRouter _eventRouter;
    // ... existing fields ...

    // IHostedService.StartAsync — called by the host on startup
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EventSubClient starting — initializing conduit");
        await _conduitOrchestrator.InitializeAsync(cancellationToken);
        _shardManager.OnSessionIdUpdated += OnShardSessionIdUpdated;
    }

    // IHostedService.StopAsync — called by the host on shutdown
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EventSubClient stopping — tearing down conduit");
        await _conduitOrchestrator.TeardownAsync(cancellationToken);
        await _shardManager.DisposeAsync();
    }

    private async void OnShardSessionIdUpdated(object? sender, SessionIdUpdatedArgs args)
    {
        // args.ShardId = the stable shard identifier; args.NewSessionId = ephemeral WebSocket session
        if (args.NewSessionId == null)
            await _conduitOrchestrator.RemoveShardAsync(args.ShardId, CancellationToken.None);
        else if (args.OldSessionId == null)
            await _conduitOrchestrator.AddShardAsync(args.ShardId, args.NewSessionId, CancellationToken.None);
        else
            await _conduitOrchestrator.UpdateShardAsync(args.ShardId, args.OldSessionId, args.NewSessionId, CancellationToken.None);
    }

    // Redefine StartAsync(userId) — starts UserSequencer, does not start WebSocket
    public async Task<bool> StartAsync(string userId)
    {
        _eventDictionary.TryGetValue(userId, out var provider);
        if (provider is null) return false;
        await provider.StartAsync();
        return true;
    }

    // Redefine StopAsync(userId) — stops UserSequencer, releases from shard
    public async Task<bool> StopAsync(string userId)
    {
        _eventDictionary.TryGetValue(userId, out var provider);
        if (provider is null) return false;
        await provider.StopAsync();
        await _shardManager.ReleaseUserFromShardAsync(userId, CancellationToken.None);
        return true;
    }

    private int _disposed;
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        await StopAsync(CancellationToken.None);
    }
}
```

- [ ] **Step 4: Fill in `CreateClientWithMocks` in the test and run**

After implementing the constructor update, read the new constructor and fill in `CreateClientWithMocks`:

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~EventSubClientHostedServiceTests" -v minimal
```

- [ ] **Step 5: Build and fix**

```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add "Twitch EventSub library/EventSubClient.cs" TwitchEventSub_Websocket.Tests/Phase4Tests/EventSubClientHostedServiceTests.cs
git commit -m "feat(phase4): EventSubClient implements IHostedService + IAsyncDisposable; delegates lifecycle to ConduitOrchestrator"
```

---

## Phase 5 — Event Routing

### Task 5.1: `EventRouter`

**Files:**
- Create: `Twitch EventSub library/IEventRouter.cs`
- Create: `Twitch EventSub library/CoreFunctions/EventRouter.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase5Tests/EventRouterTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `TwitchEventSub_Websocket.Tests/Phase5Tests/EventRouterTests.cs`:

```csharp
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase5Tests;

public class EventRouterTests
{
    [Fact]
    public void RegisterUser_ThenMessage_RoutesToCorrectCallback()
    {
        var router = new EventRouter();
        bool dispatched = false;
        router.RegisterUser("broadcaster-123", _ => dispatched = true);

        router.OnMessageReceived(BuildNotification("broadcaster-123"));

        Assert.True(dispatched);
    }

    [Fact]
    public void UnregisteredUser_MessageDropped_NoException()
    {
        var router = new EventRouter();
        var ex = Record.Exception(() => router.OnMessageReceived(BuildNotification("unknown-user")));
        Assert.Null(ex);
    }

    [Fact]
    public void DuplicateMessageId_DeduplicatedExactlyOnce()
    {
        var rp = new ReplayProtection(100);
        var router = new EventRouter(rp);
        int dispatchCount = 0;
        router.RegisterUser("broadcaster-123", _ => Interlocked.Increment(ref dispatchCount));

        var message = BuildNotification("broadcaster-123", messageId: "msg-dup-001");
        router.OnMessageReceived(message);
        router.OnMessageReceived(message);  // duplicate

        Assert.Equal(1, dispatchCount);
    }

    [Fact]
    public void UnregisterUser_SubsequentMessages_NotRouted()
    {
        var router = new EventRouter();
        bool dispatched = false;
        router.RegisterUser("broadcaster-123", _ => dispatched = true);
        router.UnregisterUser("broadcaster-123");

        router.OnMessageReceived(BuildNotification("broadcaster-123"));
        Assert.False(dispatched);
    }

    [Fact]
    public void CategoryB_UserId_RoutesToCorrectCallback()
    {
        // Category B: user_id routing (whispers, user.update)
        var router = new EventRouter();
        bool dispatched = false;
        router.RegisterUser("user-456", _ => dispatched = true);

        router.OnMessageReceived(BuildNotificationByUserId("user-456"));
        Assert.True(dispatched);
    }

    private static WebSocketNotificationMessage BuildNotification(string broadcasterId, string messageId = "msg-001")
    {
        return new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata
            {
                MessageId = messageId,
                MessageType = "notification",
                MessageTimestamp = DateTime.UtcNow.ToString("O")
            },
            Payload = new WebSocketNotificationPayload
            {
                Subscription = new WebSocketSubscription
                {
                    Id = "sub-1",
                    Type = "channel.follow",
                    Condition = new Condition { BroadcasterUserId = broadcasterId }
                }
            }
        };
    }

    private static WebSocketNotificationMessage BuildNotificationByUserId(string userId, string messageId = "msg-uid-001")
    {
        return new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata
            {
                MessageId = messageId,
                MessageType = "notification",
                MessageTimestamp = DateTime.UtcNow.ToString("O")
            },
            Payload = new WebSocketNotificationPayload
            {
                Subscription = new WebSocketSubscription
                {
                    Id = "sub-2",
                    Type = "user.whisper.message",
                    Condition = new Condition { UserId = userId }
                }
            }
        };
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (`EventRouter` does not exist)

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~EventRouterTests" -v minimal
```

- [ ] **Step 3: Create `IEventRouter.cs`**

```csharp
using Twitch.EventSub.Messages;

namespace Twitch.EventSub;

/// <summary>
/// Routes incoming WebSocket messages to per-user callbacks.
/// Routing categories:
///   A — broadcaster_user_id (most events)
///   B — user_id (user-scoped events: user.whisper.message, user.update)
///   C — conduit/platform events (conduit.shard.disabled) — handled upstream, not here
/// </summary>
public interface IEventRouter
{
    void RegisterUser(string userId, Action<WebSocketMessage> handler);
    void UnregisterUser(string userId);
    void OnMessageReceived(WebSocketMessage message);
}
```

- [ ] **Step 4: Create `EventRouter.cs`**

Create `Twitch EventSub library/CoreFunctions/EventRouter.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Routes incoming WebSocket messages to the correct per-user callback.
/// Category A: routes by Subscription.Condition.BroadcasterUserId
/// Category B: routes by Subscription.Condition.UserId
/// Uses ReplayProtection for cross-shard deduplication.
/// </summary>
public class EventRouter : IEventRouter
{
    private readonly ConcurrentDictionary<string, Action<WebSocketMessage>> _byBroadcaster = new();
    private readonly ConcurrentDictionary<string, Action<WebSocketMessage>> _byUserId = new();
    private readonly ReplayProtection _replayProtection;
    private readonly ILogger<EventRouter>? _logger;

    public EventRouter(ReplayProtection? replayProtection = null, ILogger<EventRouter>? logger = null)
    {
        _replayProtection = replayProtection ?? new ReplayProtection(100);
        _logger = logger;
    }

    public void RegisterUser(string userId, Action<WebSocketMessage> handler)
    {
        _byBroadcaster[userId] = handler;
        _byUserId[userId] = handler;
    }

    public void UnregisterUser(string userId)
    {
        _byBroadcaster.TryRemove(userId, out _);
        _byUserId.TryRemove(userId, out _);
    }

    public void OnMessageReceived(WebSocketMessage message)
    {
        if (message is not WebSocketNotificationMessage notification) return;

        var messageId = notification.Metadata?.MessageId;
        var timestamp = notification.Metadata?.MessageTimestamp;

        if (messageId == null || timestamp == null) return;
        if (!_replayProtection.IsUpToDate(timestamp))
        {
            _logger?.LogWarning("EventRouter dropped stale message {MessageId}", messageId);
            return;
        }
        if (_replayProtection.IsDuplicate(messageId))
        {
            _logger?.LogDebug("EventRouter dropped duplicate message {MessageId}", messageId);
            return;
        }

        var condition = notification.Payload?.Subscription?.Condition;
        var broadcasterId = condition?.BroadcasterUserId;
        var userId = condition?.UserId;

        Action<WebSocketMessage>? handler = null;
        if (broadcasterId != null) _byBroadcaster.TryGetValue(broadcasterId, out handler);
        if (handler == null && userId != null) _byUserId.TryGetValue(userId, out handler);

        if (handler == null)
        {
            _logger?.LogDebug("EventRouter: no handler for broadcasterId={BId} userId={UId}", broadcasterId, userId);
            return;
        }

        handler(message);
    }
}
```

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~EventRouterTests" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add "Twitch EventSub library/IEventRouter.cs" "Twitch EventSub library/CoreFunctions/EventRouter.cs" TwitchEventSub_Websocket.Tests/Phase5Tests/EventRouterTests.cs
git commit -m "feat(phase5): add EventRouter with broadcaster/user_id routing and ReplayProtection deduplication"
```

---

## Phase 6 — DI Registration, Logging & Test Sweep

### Task 6.1: Update `ServiceCollectionExtensions`

**Files:**
- Modify: `Twitch EventSub library/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Update registrations**

In `ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddTwitchEventSub(
    this IServiceCollection services,
    Action<EventSubClientOptions> configure)
{
    services.AddOptions<EventSubClientOptions>()
        .Configure(configure)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddTwitchEventSubHttpClients();
    services.AddTwitchEventSubClient();
    return services;
}

public static IServiceCollection AddTwitchEventSubClient(this IServiceCollection services)
{
    if (!services.Any(d => d.ServiceType == typeof(ILoggerFactory)))
        services.AddLogging();

    services.AddSingleton<ReplayProtection>(sp =>
        new ReplayProtection(100));  // singleton shared across all shards

    services.AddSingleton<IEventRouter, EventRouter>();
    services.AddSingleton<IShardManager, ShardManager>();
    services.AddSingleton<IConduitOrchestrator>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<EventSubClientOptions>>().Value;
        var api = sp.GetRequiredService<ITwitchConduitApi>();
        var logger = sp.GetRequiredService<ILogger<ConduitOrchestrator>>();
        return new ConduitOrchestrator(api, opts.ClientId, opts.AppAccessToken, logger);
    });
    // Register TwitchApiConduit as ITwitchConduitApi (it implements the interface)
    services.AddSingleton<ITwitchConduitApi>(sp => sp.GetRequiredService<TwitchApiConduit>());

    services.AddSingleton<IEventSubClient, EventSubClient>();
    services.AddHostedService(sp => (EventSubClient)sp.GetRequiredService<IEventSubClient>());

    return services;
}
```

- [ ] **Step 2: Build**

```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
```

- [ ] **Step 3: Commit**

```bash
git add "Twitch EventSub library/ServiceCollectionExtensions.cs"
git commit -m "feat(phase6): register ShardManager, ConduitOrchestrator, EventRouter; add IHostedService; ValidateOnStart"
```

---

### Task 6.2: State machine transition logging

**Files:**
- Modify: `Twitch EventSub library/CoreFunctions/ShardSequencer.cs`
- Modify: `Twitch EventSub library/User/UserBase.cs`

- [ ] **Step 1: Verify `ShardSequencer` already logs via `OnTransitioned`**

The `ShardSequencer` added in Task 2.2 already includes:
```csharp
_machine.OnTransitioned(t => _logger.LogInformation(
    "Shard {ShardId}: {Source} → {Dest} trigger={Trigger}",
    _shardId, t.Source, t.Destination, t.Trigger));
```

Verify all 7 close codes and both reconnect paths emit structured log entries. Add any missing `LogWarning`/`LogCritical` calls for error paths.

- [ ] **Step 2: Verify `UserBase` OnTransitioned logging**

The `UserBase` refactor in Task 3.1 added:
```csharp
machine.OnTransitioned(t =>
    _logger?.LogInformation(
        "User {UserId} transition: {Source} → {Dest} trigger={Trigger}",
        UserId, t.Source, t.Destination, t.Trigger));
```

Verify this is wired. If `_logger` is not accessible from within `UserBase.StateMachineCofiguration`, expose it as:
```csharp
protected ILogger? Logger { get; }
```
and assign in the constructor.

- [ ] **Step 3: Build and run full suite**

```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" -v minimal
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add "Twitch EventSub library/CoreFunctions/ShardSequencer.cs" "Twitch EventSub library/User/UserBase.cs"
git commit -m "feat(phase6): verify and complete structured logging on all ShardSequencer and UserSequencer state transitions"
```

---

### Task 6.3: Final test sweep

- [ ] **Step 1: Run complete test suite**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" -v normal
```

Expected: all tests pass, no warnings about obsolete patterns.

- [ ] **Step 2: Verify existing registry tests still pass**

```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~RegistryTests" -v minimal
```

Expected: 4 tests pass (registry count checks still hold after user.update fix).

- [ ] **Step 3: Tag version**

```bash
git tag v4.0.0-alpha
```

---

## Quick Reference

**Build library:**
```bash
dotnet build "Twitch EventSub library/Twitch.EventSub_Websocket.csproj" -v minimal
```

**Run all tests:**
```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" -v minimal
```

**Run tests for one phase:**
```bash
dotnet test "TwitchEventSub_Websocket.Tests/TwitchEventSub_Websocket.Tests.csproj" --filter "FullyQualifiedName~Phase1Tests" -v minimal
```

**Key namespaces:**
- Library root: `Twitch.EventSub`
- Core functions: `Twitch.EventSub.CoreFunctions`
- User state machine: `Twitch.EventSub.User`
- API: `Twitch.EventSub.API`
- Conduit API: `Twitch.EventSub.APIConduit`
- Tests: `TwitchEventSub_Websocket.Tests`
