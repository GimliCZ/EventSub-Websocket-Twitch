# Spec A — Conduit Correctness Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the conduit transport's correctness bugs on the single-conduit path (per-user subscription scoping, raw messages, pagination, config-driven keepalive, ordered processing, shared dedup, concurrency-safe HTTP) and add logic/synthetic/fuzz test suites.

**Architecture:** Enforce the ownership chain library→conduit→user→subscriptions→management→messages as call-direction. Introduce `ShardInbound` (raw+parsed) and a `MessagePipeline` that serializes delivery with Rx `.Concat()`; route by condition to the owning user; dedup per-user through one shared `ReplayProtection`. Subscription reconciliation becomes condition-scoped so users never touch each other's subs. HTTP calls use per-request headers.

**Tech Stack:** C# / .NET 10, xUnit, Moq, System.Reactive, Newtonsoft.Json, Stateless, Websocket.Client.

**Spec:** `docs/superpowers/specs/2026-05-31-conduit-correctness-spec-a-design.md`

**Conventions for every task:**
- Build the library: `dotnet build "TwitchEventSubWebsocket.sln" -c Debug --nologo`
- Run a filtered test set: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~<Name>"`
- The harness EXE locks `Twitch.EventSub_Websocket.dll`. Before building, ensure it is not running: `Get-Process -Name "TwitchEventSub.LiveHarness" -ErrorAction SilentlyContinue | Stop-Process -Force`
- Commit message footer line: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Do NOT push. Commit locally only. Branch is `develop/conduit`.
- Baseline before starting: 142 tests pass.

---

## File Structure

**New files (library):**
- `Twitch EventSub library/CoreFunctions/ShardInbound.cs` — record `(string Raw, WebSocketMessage Parsed)`
- `Twitch EventSub library/CoreFunctions/MessagePipeline.cs` — ordered delivery + condition routing
- `Twitch EventSub library/CoreFunctions/IMessagePipeline.cs` — interface for the pipeline

**Modified (library):**
- `API/TwitchApi.cs` — per-request headers; cursor-driven pagination
- `API/Models/GetSubscriptionsResponse.cs` — typed `Pagination { cursor }`
- `APIConduit/TwitchConduitApi.cs` — per-request headers
- `EventSubClientOptions.cs` — `DedupWindowSize`; document keepalive usage
- `ServiceCollectionExtensions.cs` — remove EventRouter; register pipeline + shared ReplayProtection from options
- `CoreFunctions/ShardSequencer.cs` — `Subject<ShardInbound>`; `.Concat()` internal handling; `NegotiatedKeepaliveSeconds`
- `CoreFunctions/IShardBinding.cs` + `ShardBinding.cs` — `IObservable<ShardInbound>`
- `User/UserSequencer.cs` — raw callback always; shared dedup; keepalive from config; delete dead parse path
- `User/EventProvider.cs` — inject shared ReplayProtection
- `User/SubscriptionManager.cs` — condition-scoped reconciliation + exact usage accounting
- `EventSubClient.cs` — pass shared ReplayProtection into providers (via constructor)

**Deleted (library):**
- `CoreFunctions/EventRouter.cs`, `IEventRouter.cs`
- `Messages/NotificationMessage/WebSocketNotificationCondition.cs`

**Tests:**
- Delete `Phase5Tests/EventRouterTests.cs`
- New: `Phase7Tests/CursorPaginationTests.cs`, `OptionsConfigTests.cs`, `ShardInboundTests.cs`, `MessagePipelineTests.cs`, `RawMessageTests.cs`, `KeepaliveConfigTests.cs`, `SubscriptionScopingTests.cs`, `SyntheticScenarioTests.cs`, `FuzzPipelineTests.cs`

---

## PHASE 1 — API edge: cursor pagination + per-request headers

### Task 1: Fix `GetSubscriptionsResponse` pagination model

**Files:**
- Modify: `Twitch EventSub library/API/Models/GetSubscriptionsResponse.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/CursorPaginationTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/CursorPaginationTests.cs`:

```csharp
using Newtonsoft.Json;
using Twitch.EventSub.API.Models;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class CursorPaginationTests
{
    [Fact]
    public void Deserialize_ReadsCursorFromPaginationObject()
    {
        // Twitch returns the paging cursor inside "pagination", not at top level.
        var json = @"{ ""total"": 2, ""data"": [], ""pagination"": { ""cursor"": ""abc123"" } }";
        var resp = JsonConvert.DeserializeObject<GetSubscriptionsResponse>(json);
        Assert.NotNull(resp);
        Assert.Equal("abc123", resp!.Pagination.Cursor);
    }

    [Fact]
    public void Deserialize_EmptyPagination_CursorIsNullOrEmpty()
    {
        var json = @"{ ""total"": 0, ""data"": [], ""pagination"": {} }";
        var resp = JsonConvert.DeserializeObject<GetSubscriptionsResponse>(json);
        Assert.NotNull(resp);
        Assert.True(string.IsNullOrEmpty(resp!.Pagination.Cursor));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~CursorPaginationTests"`
Expected: FAIL — `GetSubscriptionsResponse` has no `Pagination.Cursor` (it has `object Pagination` and a top-level `Cursor`).

- [ ] **Step 3: Rewrite the model**

Replace the entire contents of `Twitch EventSub library/API/Models/GetSubscriptionsResponse.cs`:

```csharp
using Newtonsoft.Json;
using Twitch.EventSub.Messages.SharedContents;

namespace Twitch.EventSub.API.Models
{
    public class GetSubscriptionsResponse
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("data")]
        public List<WebSocketSubscription> Data { get; set; } = new();

        [JsonProperty("total_cost")]
        public int TotalCost { get; set; }

        [JsonProperty("max_total_cost")]
        public int MaxTotalCost { get; set; }

        [JsonProperty("pagination")]
        public SubscriptionPagination Pagination { get; set; } = new();
    }

    public class SubscriptionPagination
    {
        [JsonProperty("cursor")]
        public string? Cursor { get; set; }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~CursorPaginationTests"`
Expected: PASS (2 tests). The build may FAIL elsewhere because `TwitchApi.GetAllSubscriptionsAsync` references `response.Cursor` — fixed in Task 2. If so, proceed to Task 2 before committing.

- [ ] **Step 5: Commit (after Task 2 builds clean)**

---

### Task 2: Drive `GetAllSubscriptionsAsync` off the cursor

**Files:**
- Modify: `Twitch EventSub library/API/TwitchApi.cs` (GetSubscriptionsAsync line 117–158; GetAllSubscriptionsAsync line 168–203)
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/CursorPaginationTests.cs` (add)

- [ ] **Step 1: Add the failing aggregation test**

Append to `CursorPaginationTests.cs` (add `using` lines at top: `using Microsoft.Extensions.Logging.Abstractions; using Moq; using System.Net; using Twitch.EventSub.API; using Twitch.EventSub.API.Enums; using Twitch.EventSub.CoreFunctions;`):

```csharp
    private static HttpClient FakeSequenceClient(params (HttpStatusCode code, string body)[] responses)
    {
        var queue = new Queue<(HttpStatusCode, string)>(responses);
        var handler = new StubHandler(() => queue.Dequeue());
        return new HttpClient(handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<(HttpStatusCode, string)> _next;
        public StubHandler(Func<(HttpStatusCode, string)> next) => _next = next;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (code, body) = _next();
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task GetAllSubscriptions_FollowsCursorAcrossPages()
    {
        var page1 = @"{ ""total"": 2, ""data"": [ { ""id"": ""s1"", ""type"": ""channel.update"", ""version"": ""2"", ""condition"": { ""broadcaster_user_id"": ""1"" } } ], ""pagination"": { ""cursor"": ""next"" } }";
        var page2 = @"{ ""total"": 2, ""data"": [ { ""id"": ""s2"", ""type"": ""channel.follow"", ""version"": ""2"", ""condition"": { ""broadcaster_user_id"": ""1"" } } ], ""pagination"": {} }";

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns(FakeSequenceClient((HttpStatusCode.OK, page1), (HttpStatusCode.OK, page2)));
        var api = new TwitchApi(factory.Object);
        using var cts = new CancellationTokenSource();

        var all = await api.GetAllSubscriptionsAsync("cid", "tok", cts, NullLogger.Instance, SubscriptionStatusTypes.Empty);

        var ids = all.SelectMany(r => r.Data).Select(d => d.Id).ToList();
        Assert.Equal(new[] { "s1", "s2" }, ids);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~GetAllSubscriptions_FollowsCursor"`
Expected: FAIL — current loop reads `response.Cursor` (now removed) so it won't compile, or (after Task 1) only returns page 1.

- [ ] **Step 3: Rewrite the two methods**

In `Twitch EventSub library/API/TwitchApi.cs`, replace the `GetSubscriptionsAsync` body's `OK` case and the `GetAllSubscriptionsAsync` method.

Change line 145 from:
```csharp
                        case HttpStatusCode.OK: return JsonConvert.DeserializeObject<GetSubscriptionsResponse>(body);
```
(no change needed to that line; the type now has the new Pagination).

Replace `GetAllSubscriptionsAsync` (168–203) with:

```csharp
        public async Task<List<GetSubscriptionsResponse>> GetAllSubscriptionsAsync(string? clientId, string? accessToken, CancellationTokenSource clSource, ILogger logger, SubscriptionStatusTypes statusSelector = SubscriptionStatusTypes.Enabled, string? url = null)
        {
            var allSubscriptions = new List<GetSubscriptionsResponse>();
            string? afterCursor = null;

            do
            {
                var response = await GetSubscriptionsAsync(clientId, accessToken, statusSelector, clSource, logger, afterCursor, url).ConfigureAwait(false);
                if (response == null)
                {
                    logger.LogInformation("[EventSubClient] - [TwitchApi] Response returned null cause of invalid userId or filter parameter");
                    break;
                }

                allSubscriptions.Add(response);
                afterCursor = response.Pagination.Cursor;
            }
            while (!string.IsNullOrEmpty(afterCursor));

            if (allSubscriptions.Count == 0)
            {
                logger.LogInformation("[EventSubClient] - [TwitchApi] List of subscriptions returned EMPTY!");
            }

            return allSubscriptions;
        }
```

- [ ] **Step 4: Run tests**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~CursorPaginationTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/API/Models/GetSubscriptionsResponse.cs" "Twitch EventSub library/API/TwitchApi.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/CursorPaginationTests.cs
git commit -m "fix(api): read paging cursor from pagination.cursor and follow it across pages"
```

---

### Task 3: Per-request HTTP headers in `TwitchApi` (concurrency-safe)

**Files:**
- Modify: `Twitch EventSub library/API/TwitchApi.cs` (all 4 methods)
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/CursorPaginationTests.cs` (add header-capture test)

- [ ] **Step 1: Write the failing test**

Append to `CursorPaginationTests.cs`:

```csharp
    [Fact]
    public async Task Validate_SetsPerRequestHeaders_NotSharedDefaults()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureHandler(req => { captured = req; return (HttpStatusCode.OK, "{}"); });
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var api = new TwitchApi(factory.Object);
        using var cts = new CancellationTokenSource();

        await api.ValidateTokenAsync("usertoken", cts, NullLogger.Instance);

        Assert.NotNull(captured);
        Assert.Equal("OAuth", captured!.Headers.Authorization!.Scheme);
        Assert.Equal("usertoken", captured.Headers.Authorization.Parameter);
        // DefaultRequestHeaders must remain untouched (no shared mutation)
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode, string)> _fn;
        public CaptureHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (code, body) = _fn(request);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~Validate_SetsPerRequestHeaders"`
Expected: FAIL — current `ValidateTokenAsync` sets `client.DefaultRequestHeaders.Authorization`, so the assertion `Assert.Null(client.DefaultRequestHeaders.Authorization)` fails.

- [ ] **Step 3: Convert all four methods to per-request headers**

In `Twitch EventSub library/API/TwitchApi.cs`, for each method, stop mutating `httpClient.DefaultRequestHeaders` and instead build an `HttpRequestMessage`. Example for `ValidateTokenAsync` (replace 213–241):

```csharp
        public async Task<bool> ValidateTokenAsync(string? accessToken, CancellationTokenSource clSource, ILogger logger, string? url = null)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.TwitchApi);
            using var request = new HttpRequestMessage(HttpMethod.Get, url ?? ValidateUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
            try
            {
                var response = await httpClient.SendAsync(request, clSource.Token).ConfigureAwait(false);
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        logger.LogDebug("[EventSubClient] - [TwitchApi] Validation of Token Successfull {StatusCode}", response.StatusCode);
                        return true;
                    case HttpStatusCode.Unauthorized:
                        var errorMessage = await response.Content.ReadAsStringAsync(clSource.Token);
                        throw new InvalidAccessTokenException($"[EventSubClient] - [TwitchApi] Validation of token failed: {errorMessage} {response.ReasonPhrase}");
                    default:
                        logger.LogWarning("[EventSubClient] - [TwitchApi] ValidateTokenAsync got non-standard status code: {StatusCode}", response.StatusCode);
                        return false;
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "[EventSubClient] - [TwitchApi] ValidateTokenAsync encountered an exception.");
                return false;
            }
        }
```

Apply the same pattern to `SubscribeAsync` (POST, body as `request.Content`, headers `Bearer` + `Client-Id`), `UnSubscribeAsync` (DELETE), and `GetSubscriptionsAsync` (GET). For each: `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken); request.Headers.Add("Client-Id", clientId);` and `request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");` for POST. Use `await httpClient.SendAsync(request, clSource.Token)`.

- [ ] **Step 4: Run tests**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~CursorPaginationTests|FullyQualifiedName~ApiTests"`
Expected: PASS (all). Then full build: `dotnet build "TwitchEventSubWebsocket.sln" -c Debug --nologo` → 0 errors.

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/API/TwitchApi.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/CursorPaginationTests.cs
git commit -m "fix(api): use per-request HttpRequestMessage headers to avoid shared-header races"
```

---

### Task 4: Per-request headers in `TwitchConduitApi`

**Files:**
- Modify: `Twitch EventSub library/APIConduit/TwitchConduitApi.cs` (every method mutating DefaultRequestHeaders)

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/ConduitApiHeaderTests.cs`:

```csharp
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Twitch.EventSub.APIConduit;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class ConduitApiHeaderTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(@"{ ""data"": [ { ""id"": ""c1"", ""shard_count"": 1 } ] }") });
        }
    }

    [Fact]
    public async Task GetConduitIds_SetsPerRequestHeaders_NotSharedDefaults()
    {
        var handler = new CaptureHandler();
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var api = new TwitchApiConduit(factory.Object);

        await api.GetConduitIdsAsync("apptoken", "clientid", CancellationToken.None);

        Assert.Equal("Bearer", handler.Last!.Headers.Authorization!.Scheme);
        Assert.Equal("apptoken", handler.Last.Headers.Authorization.Parameter);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~ConduitApiHeaderTests"`
Expected: FAIL — `GetConduitIdsAsync` sets `DefaultRequestHeaders`.

- [ ] **Step 3: Convert methods to per-request headers**

In `TwitchConduitApi.cs`, replace each `httpClient.DefaultRequestHeaders.Authorization = …; httpClient.DefaultRequestHeaders.Add("Client-Id", …)` + verb call with an `HttpRequestMessage` carrying those headers (and `request.Content` for POST/PATCH), then `httpClient.SendAsync(request, …)`. Apply to: `ConduitCreatorAsync`, `ConduitUpdateAsync`, `ConduitDeleteAsync`, `ConduitGetShardsAsync`, `GetConduitIdsAsync`, `UpdateConduitShardSessionAsync`. Pattern identical to Task 3.

- [ ] **Step 4: Run tests**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~ConduitApiHeaderTests|FullyQualifiedName~Phase4Tests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/APIConduit/TwitchConduitApi.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/ConduitApiHeaderTests.cs
git commit -m "fix(conduit-api): per-request headers to avoid shared-header races"
```

---

## PHASE 2 — Config

### Task 5: Add `DedupWindowSize` option

**Files:**
- Modify: `Twitch EventSub library/EventSubClientOptions.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/OptionsConfigTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/OptionsConfigTests.cs`:

```csharp
using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class OptionsConfigTests
{
    [Fact]
    public void DedupWindowSize_DefaultsTo100()
    {
        var o = new EventSubClientOptions();
        Assert.Equal(100, o.DedupWindowSize);
    }

    [Fact]
    public void KeepaliveTimeoutSeconds_DefaultsTo10()
    {
        var o = new EventSubClientOptions();
        Assert.Equal(10, o.KeepaliveTimeoutSeconds);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~OptionsConfigTests"`
Expected: FAIL — `DedupWindowSize` does not exist.

- [ ] **Step 3: Add the option**

In `Twitch EventSub library/EventSubClientOptions.cs`, after the `KeepaliveTimeoutSeconds` property (line 17), add:

```csharp
    /// <summary>
    /// Number of recent message IDs the shared replay-protection gate remembers for
    /// at-least-once de-duplication across all users. Default 100.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DedupWindowSize { get; set; } = 100;
```

- [ ] **Step 4: Run tests**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~OptionsConfigTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/EventSubClientOptions.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/OptionsConfigTests.cs
git commit -m "feat(options): add DedupWindowSize"
```

---

## PHASE 3 — ShardInbound + ShardSequencer stream type

### Task 6: Introduce `ShardInbound` record

**Files:**
- Create: `Twitch EventSub library/CoreFunctions/ShardInbound.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/ShardInboundTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/ShardInboundTests.cs`:

```csharp
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class ShardInboundTests
{
    [Fact]
    public void Carries_RawAndParsed()
    {
        var parsed = new WebSocketKeepAliveMessage();
        var inbound = new ShardInbound("{\"raw\":true}", parsed);
        Assert.Equal("{\"raw\":true}", inbound.Raw);
        Assert.Same(parsed, inbound.Parsed);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~ShardInboundTests"`
Expected: FAIL — `ShardInbound` does not exist.

- [ ] **Step 3: Create the record**

Create `Twitch EventSub library/CoreFunctions/ShardInbound.cs`:

```csharp
using Twitch.EventSub.Messages;

namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// A single inbound shard frame, carrying both the original JSON string (for the raw-message
/// callback) and the parsed message (for routing and FSM handling).
/// </summary>
public sealed record ShardInbound(string Raw, WebSocketMessage Parsed);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~ShardInboundTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/CoreFunctions/ShardInbound.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/ShardInboundTests.cs
git commit -m "feat(messages): add ShardInbound record carrying raw + parsed"
```

---

### Task 7: `ShardSequencer` publishes `ShardInbound`, serializes handling, exposes keepalive

**Files:**
- Modify: `Twitch EventSub library/CoreFunctions/ShardSequencer.cs`
- Modify: `Twitch EventSub library/CoreFunctions/IShardBinding.cs`, `ShardBinding.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/ShardInboundTests.cs` (add) + update existing Phase2/Phase6 shard tests

- [ ] **Step 1: Write the failing test**

Append to `ShardInboundTests.cs` (add usings: `using Microsoft.Extensions.Logging.Abstractions; using Twitch.EventSub.Messages; using Twitch.EventSub.Messages.SharedContents; using Twitch.EventSub.Messages.WelcomeMessage;`):

```csharp
    [Fact]
    public async Task DriveFromMessage_PublishesShardInbound_WithRaw()
    {
        var shard = new ShardSequencer("s1", NullLogger.Instance);
        await shard.SimulateConnectingForTestAsync();
        ShardInbound? got = null;
        using var sub = shard.Messages.Subscribe(i => got = i);

        var welcome = new WebSocketWelcomeMessage
        {
            Metadata = new WebSocketMessageMetadata { MessageType = "session_welcome", MessageId = "m1", MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
            Payload = new WebSocketWelcomePayload { Session = new WebSocketSession { Id = "sess", KeepAliveTimeoutSeconds = 30 } }
        };
        await shard.DriveFromMessageAsync(new ShardInbound("RAWJSON", welcome), isPending: false);

        Assert.NotNull(got);
        Assert.Equal("RAWJSON", got!.Raw);
        Assert.Equal(30, shard.NegotiatedKeepaliveSeconds);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~DriveFromMessage_PublishesShardInbound"`
Expected: FAIL — `Messages` is `IObservable<WebSocketMessage>`, `DriveFromMessageAsync` takes `(WebSocketMessage,bool)`, and `NegotiatedKeepaliveSeconds` does not exist.

- [ ] **Step 3: Modify `ShardSequencer`**

In `Twitch EventSub library/CoreFunctions/ShardSequencer.cs`:

(a) Change the subject and observable (lines 23, 34):
```csharp
    private readonly Subject<ShardInbound> _messages = new();
    ...
    public IObservable<ShardInbound> Messages => _messages;
```

(b) Add the keepalive property near `SessionId` (after line 31):
```csharp
    public int? NegotiatedKeepaliveSeconds { get; private set; }
```

(c) Change `DriveFromMessageAsync` signature and body to accept `ShardInbound` and publish it; capture keepalive on welcome:
```csharp
    internal async Task DriveFromMessageAsync(ShardInbound inbound, bool isPending)
    {
        if (inbound?.Parsed == null) return;
        var parsed = inbound.Parsed;

        if (isPending)
        {
            if (parsed is WebSocketWelcomeMessage pendingWelcome &&
                pendingWelcome.Payload?.Session?.Id is { Length: > 0 } newSession)
            {
                if (pendingWelcome.Payload.Session.KeepAliveTimeoutSeconds is { } kp) NegotiatedKeepaliveSeconds = kp;
                await HandleNewConnectionWelcomeAsync(newSession);
            }
            return;
        }

        _messages.OnNext(inbound);

        switch (parsed)
        {
            case WebSocketWelcomeMessage welcome
                when welcome.Payload?.Session?.Id is { Length: > 0 } session &&
                     (_machine.State == ShardState.WaitingForWelcome || _machine.State == ShardState.Connecting):
                if (welcome.Payload.Session.KeepAliveTimeoutSeconds is { } k) NegotiatedKeepaliveSeconds = k;
                await HandleWelcomeAsync(session);
                break;

            case WebSocketReconnectMessage reconnect
                when reconnect.Payload?.Session?.ReconnectUrl is { Length: > 0 } url &&
                     Uri.TryCreate(url, UriKind.Absolute, out var reconnectUri) &&
                     _machine.CanFire(ShardTrigger.ReconnectRequested):
                await HandleReconnectAsync(reconnectUri, CancellationToken.None);
                break;
        }
    }
```

(d) Serialize the socket subscription. Replace `SubscribeToClient`'s `client.MessageReceived.Subscribe(async msg => {...})` with a `.Concat()` form. Replace the `msgSub` assignment block (159–185) with:
```csharp
        var msgSub = client.MessageReceived
            .Select(msg => System.Reactive.Linq.Observable.FromAsync(async () =>
            {
                if (msg.MessageType == System.Net.WebSockets.WebSocketMessageType.Text && msg.Text != null)
                {
                    try
                    {
                        var parsed = await MessageProcessing.DeserializeMessageAsync(msg.Text);
                        if (parsed == null) return;
                        _logger.LogDebug("Shard {ShardId} message type={Type} isPending={IsPending}",
                            _shardId, parsed.Metadata?.MessageType, isPending);
                        await DriveFromMessageAsync(new ShardInbound(msg.Text, parsed), isPending);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Shard {ShardId} failed to deserialize message", _shardId);
                    }
                }
            }))
            .Concat()
            .Subscribe();
```

(e) Add `using System.Reactive.Linq;` at the top if not present.

- [ ] **Step 4: Update existing shard tests to the new types**

In `TwitchEventSub_Websocket.Tests/Phase6Tests/ShardSelfDriveTests.cs` and `ShardBindingRoutingTests.cs`, every `shard.DriveFromMessageAsync(MSG, isPending: …)` becomes `shard.DriveFromMessageAsync(new ShardInbound("{}", MSG), isPending: …)`. Where a test subscribes to `shard.Messages` / `binding.UserMessages` and inspects a `WebSocketMessage`, change the lambda variable to a `ShardInbound` and read `.Parsed`. Add `using Twitch.EventSub.CoreFunctions;` where needed.

- [ ] **Step 5: Modify `IShardBinding` + `ShardBinding` (option b: lifecycle-only binding + raw stream)**

`IShardBinding.cs`: remove `UserMessages`; add `IObservable<ShardInbound> ShardStream { get; }` and `int? NegotiatedKeepaliveSeconds { get; }`. Final interface:
```csharp
public interface IShardBinding : IDisposable
{
    string ShardId { get; }
    string SessionId { get; }
    int? NegotiatedKeepaliveSeconds { get; }
    IObservable<ShardInbound> ShardStream { get; }
    event EventHandler OnShardLost;
    event EventHandler<string> OnSessionIdChanged;
}
```
`ShardBinding.cs`: `ShardStream => _sequencer.Messages;` `NegotiatedKeepaliveSeconds => _sequencer.NegotiatedKeepaliveSeconds;` delete `UserMessages` and `IsForUser` (routing moved to `MessagePipeline`). Keep `OnShardLost`/`OnSessionIdChanged` wiring as-is.

- [ ] **Step 6: Run tests + build**

Run: `dotnet build "TwitchEventSubWebsocket.sln" -c Debug --nologo` → expect errors only in `UserSequencer.SetShardBinding` (consumes `UserMessages`) — fixed in Task 9. Run `--filter "FullyQualifiedName~Phase6Tests|FullyQualifiedName~ShardInboundTests|FullyQualifiedName~Phase2Tests"`.
Expected: shard/Phase2/Phase6 tests compile-fail only at the UserSequencer consumer; if Phase6 binding tests reference `UserMessages` typing, they now expect `ShardInbound`. Defer green to Task 9.

- [ ] **Step 7: Commit (after Task 9 builds clean)** — bundle Tasks 7+9.

---

## PHASE 4 — MessagePipeline + shared dedup; delete EventRouter

### Task 8: Create `MessagePipeline` with ordered delivery + condition routing

**Files:**
- Create: `Twitch EventSub library/CoreFunctions/IMessagePipeline.cs`, `MessagePipeline.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/MessagePipelineTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/MessagePipelineTests.cs`:

```csharp
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class MessagePipelineTests
{
    private static ShardInbound Notif(string broadcasterId, string id = "m") =>
        new("{}", new WebSocketNotificationMessage
        {
            Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
            Payload = new WebSocketNotificationPayload
            { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = broadcasterId } } }
        });

    private static ShardInbound Keepalive() =>
        new("{}", new WebSocketKeepAliveMessage
        { Metadata = new WebSocketMessageMetadata { MessageType = "session_keepalive", MessageId = "k", MessageTimestamp = System.DateTime.UtcNow.ToString("o") } });

    [Fact]
    public async Task Notification_RoutedToOwningUserOnly()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var a = new List<ShardInbound>();
        var b = new List<ShardInbound>();
        pipeline.RegisterUser("A", i => { a.Add(i); return Task.CompletedTask; });
        pipeline.RegisterUser("B", i => { b.Add(i); return Task.CompletedTask; });
        pipeline.Attach(subject);

        subject.OnNext(Notif("A"));
        subject.OnNext(Notif("B"));
        await Task.Delay(50);

        Assert.Single(a);
        Assert.Single(b);
    }

    [Fact]
    public async Task ControlMessage_BroadcastToAllUsers()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        int aCount = 0, bCount = 0;
        pipeline.RegisterUser("A", _ => { Interlocked.Increment(ref aCount); return Task.CompletedTask; });
        pipeline.RegisterUser("B", _ => { Interlocked.Increment(ref bCount); return Task.CompletedTask; });
        pipeline.Attach(subject);

        subject.OnNext(Keepalive());
        await Task.Delay(50);

        Assert.Equal(1, aCount);
        Assert.Equal(1, bCount);
    }

    [Fact]
    public async Task Delivery_PreservesArrivalOrder()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var order = new List<string>();
        pipeline.RegisterUser("A", async i =>
        {
            // simulate variable work; .Concat() must still preserve order
            await Task.Delay(i.Parsed.Metadata!.MessageId == "1" ? 30 : 1);
            lock (order) order.Add(i.Parsed.Metadata!.MessageId);
        });
        pipeline.Attach(subject);

        subject.OnNext(Notif("A", "1"));
        subject.OnNext(Notif("A", "2"));
        subject.OnNext(Notif("A", "3"));
        await Task.Delay(200);

        Assert.Equal(new[] { "1", "2", "3" }, order);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~MessagePipelineTests"`
Expected: FAIL — `MessagePipeline` does not exist.

- [ ] **Step 3: Create the interface and class**

`Twitch EventSub library/CoreFunctions/IMessagePipeline.cs`:
```csharp
namespace Twitch.EventSub.CoreFunctions;

/// <summary>
/// Routes ordered shard frames to the owning user. Notifications go to the user whose id matches
/// the subscription condition; connection-level control messages broadcast to all registered users.
/// </summary>
public interface IMessagePipeline
{
    void RegisterUser(string userId, Func<ShardInbound, Task> handler);
    void UnregisterUser(string userId);
    IDisposable Attach(IObservable<ShardInbound> shardStream);
}
```

`Twitch EventSub library/CoreFunctions/MessagePipeline.cs`:
```csharp
using System.Collections.Concurrent;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Twitch.EventSub.Messages.NotificationMessage;

namespace Twitch.EventSub.CoreFunctions;

public sealed class MessagePipeline : IMessagePipeline
{
    private readonly ConcurrentDictionary<string, Func<ShardInbound, Task>> _users = new();
    private readonly ILogger<MessagePipeline>? _logger;

    public MessagePipeline(ILogger<MessagePipeline>? logger = null) => _logger = logger;

    public void RegisterUser(string userId, Func<ShardInbound, Task> handler) => _users[userId] = handler;
    public void UnregisterUser(string userId) => _users.TryRemove(userId, out _);

    public IDisposable Attach(IObservable<ShardInbound> shardStream) =>
        shardStream.Select(i => Observable.FromAsync(() => HandleAsync(i))).Concat().Subscribe();

    private async Task HandleAsync(ShardInbound inbound)
    {
        try
        {
            if (inbound.Parsed is WebSocketNotificationMessage notification)
            {
                var condition = notification.Payload?.Subscription?.Condition;
                var ownerId = condition?.BroadcasterUserId ?? condition?.UserId;
                if (ownerId != null && _users.TryGetValue(ownerId, out var handler))
                {
                    await handler(inbound);
                }
                else
                {
                    _logger?.LogDebug("MessagePipeline: no user for condition broadcaster={B} user={U}",
                        condition?.BroadcasterUserId, condition?.UserId);
                }
                return;
            }

            // Connection-level control message: broadcast to all users on this shard.
            foreach (var handler in _users.Values)
                await handler(inbound);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MessagePipeline handler threw for message {Id}", inbound.Parsed.Metadata?.MessageId);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~MessagePipelineTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/CoreFunctions/IMessagePipeline.cs" "Twitch EventSub library/CoreFunctions/MessagePipeline.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/MessagePipelineTests.cs
git commit -m "feat(messages): add MessagePipeline with ordered delivery and condition routing"
```

---

### Task 9: Wire UserSequencer to the new stream; raw callback; shared dedup; keepalive from config

**Files:**
- Modify: `Twitch EventSub library/User/UserSequencer.cs`
- Modify: `Twitch EventSub library/User/EventProvider.cs`
- Modify: `Twitch EventSub library/EventSubClient.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/RawMessageTests.cs`, `KeepaliveConfigTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/RawMessageTests.cs`:

```csharp
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Twitch.EventSub;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.SharedContents;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class RawMessageTests
{
    private sealed class FakeBinding : IShardBinding
    {
        public readonly Subject<ShardInbound> Subject = new();
        public string ShardId => "s1";
        public string SessionId => "sess";
        public IObservable<ShardInbound> UserMessages => Subject;
        public event EventHandler? OnShardLost { add { } remove { } }
        public event EventHandler<string>? OnSessionIdChanged { add { } remove { } }
        public void Dispose() => Subject.Dispose();
    }

    [Fact]
    public async Task RawCallback_FiresForKeepalive()
    {
        var provider = TestFactory.CreateProvider(out _);
        string? raw = null;
        provider.OnRawMessageAsync += (_, r) => { raw = r; return Task.CompletedTask; };
        var binding = new FakeBinding();
        provider.SetShardBinding(binding);

        binding.Subject.OnNext(new ShardInbound(
            "{\"metadata\":{\"message_type\":\"session_keepalive\"}}",
            new WebSocketKeepAliveMessage { Metadata = new WebSocketMessageMetadata { MessageId = "k1", MessageType = "session_keepalive", MessageTimestamp = System.DateTime.UtcNow.ToString("o") } }));
        await Task.Delay(50);

        Assert.Equal("{\"metadata\":{\"message_type\":\"session_keepalive\"}}", raw);
    }
}

internal static class TestFactory
{
    public static EventProvider CreateProvider(out ReplayProtection rp)
    {
        rp = new ReplayProtection(100);
        return new EventProvider(
            userId: "123", accessToken: "tok", listOfSubs: new System.Collections.Generic.List<SubscriptionTypes>(),
            clientId: "cid", logger: NullLogger.Instance, allowRecovery: false,
            twitchApi: new TwitchApi(Mock.Of<IHttpClientFactory>()),
            conduitOrchestrator: Mock.Of<IConduitOrchestrator>(), appAccessToken: "app",
            shardManager: Mock.Of<IShardManager>(), replayProtection: rp);
    }
}
```

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/KeepaliveConfigTests.cs`:

```csharp
using Twitch.EventSub.CoreFunctions;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class KeepaliveConfigTests
{
    [Fact]
    public void ShardExposesNegotiatedKeepalive_AfterWelcome()
    {
        // Covered behaviorally in ShardInboundTests.DriveFromMessage; this asserts the property exists/defaults null.
        var shard = new ShardSequencer("s1", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        Assert.Null(shard.NegotiatedKeepaliveSeconds);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~RawMessageTests|FullyQualifiedName~KeepaliveConfigTests"`
Expected: FAIL — `EventProvider` has no `replayProtection` parameter; `UserMessages` type mismatch.

- [ ] **Step 3: Modify `UserSequencer`**

(a) Remove the per-instance dedup. Delete line 50 `private readonly ReplayProtection _replayProtection;` and its construction `_replayProtection = new ReplayProtection(10);` (line ~69). Add a constructor parameter `ReplayProtection replayProtection` and assign `_replayProtection = replayProtection;`. Update the `UserSequencer` constructor signature accordingly (add the param before `apiTestingUrl`).

(b) **Pipeline is now the router (option b).** `UserSequencer` no longer subscribes to `ShardBinding.UserMessages` for messages. Instead it exposes a public entry point the pipeline calls:
```csharp
        /// <summary>Entry point invoked by MessagePipeline for each routed frame (and broadcast control frames).</summary>
        public Task HandleInboundAsync(ShardInbound inbound) => ProcessWebSocketMessageAsync(inbound);
```
`SetShardBinding` keeps ONLY the shard-lifecycle wiring (no message subscription):
```csharp
        public void SetShardBinding(IShardBinding binding)
        {
            _shardBinding = binding;
            _shardBinding.OnShardLost += async (_, _) =>
            {
                if (StateMachine.CanFire(UserActions.WebsocketFail))
                    await StateMachine.FireAsync(UserActions.WebsocketFail);
            };
            _shardBinding.OnSessionIdChanged += (_, newId) => { SessionId = newId; };
            // Message delivery now flows through MessagePipeline.RegisterUser (wired by EventProvider), not UserMessages.
        }
```
Because routing moves to the pipeline, `IShardBinding.UserMessages` is **removed** from the interface and `ShardBinding` (the binding becomes lifecycle-only: `ShardId`, `SessionId`, `NegotiatedKeepaliveSeconds`, `OnShardLost`, `OnSessionIdChanged`). Delete `ShardBinding.IsForUser` and the `UserMessages` member. Update `ShardBinding`'s constructor to drop the `Where` filter. (Phase6 `ShardBindingRoutingTests` is replaced by `MessagePipelineTests` + `SyntheticScenarioTests`; remove it.)

(c) Replace `ProcessWebSocketMessageAsync(WebSocketMessage message)` (425) with a `ShardInbound` overload that fires raw first:
```csharp
        private async Task ProcessWebSocketMessageAsync(ShardInbound inbound)
        {
            var message = inbound.Parsed;
            // Raw callback fires for EVERY frame, before any dedup/typing.
            if (OnRawMessageRecievedAsync != null) await OnRawMessageRecievedAsync.TryInvoke(this, inbound.Raw);

            if (message?.Metadata == null) return;
            if (_replayProtection.IsDuplicate(message.Metadata.MessageId) ||
                !_replayProtection.IsUpToDate(message.Metadata.MessageTimestamp))
            {
                _logger.LogDebug("[UserSequencer] Duplicate or outdated message: {MessageId}", message.Metadata.MessageId);
                return;
            }

            switch (message)
            {
                case WebSocketWelcomeMessage welcomeMessage: await WelcomeMessageProcessingAsync(welcomeMessage); return;
                case WebSocketKeepAliveMessage: await KeepAliveMessageProcessingAsync(); return;
                case WebSocketPingMessage: await PingMessageProcessingAsync(); return;
                case WebSocketNotificationMessage notificationMessage: await NotificationMessageProcessingAsync(notificationMessage); return;
                case WebSocketReconnectMessage reconnectMessage: await ReconnectMessageProcessingAsync(reconnectMessage); return;
                case WebSocketRevocationMessage revocationMessage: await RevocationMessageProcessingAsync(revocationMessage); return;
            }
        }
```
(Note: `OnRawMessageRecievedAsync` is `AsyncEventHandler<string?>`; use `.TryInvoke`. Confirm `using Twitch.EventSub.CoreFunctions;` is present.)

(d) Delete the dead `ParseWebSocketMessageAsync` method (436–) entirely — it is the old socket path and now unused.

(e) Keepalive from config: change `AwaitWelcomeMessageAsync` short-circuit to compute `_keepAliveMs` from the shard's negotiated value. The `UserSequencer` already holds `_shardBinding`; expose the shard's keepalive through the binding. Add to `IShardBinding`: `int? NegotiatedKeepaliveSeconds { get; }`; implement in `ShardBinding` as `_sequencer.NegotiatedKeepaliveSeconds`. Then in `AwaitWelcomeMessageAsync`:
```csharp
            if (_shardBinding?.SessionId is { Length: > 0 })
            {
                var seconds = _shardBinding.NegotiatedKeepaliveSeconds ?? _options.KeepaliveTimeoutSeconds;
                _keepAliveMs = seconds * 1000 + 100;
                _logger.LogDebug("[AwaitWelcomeMessageAsync] Shard already has session for UserId: {UserId} — proceeding (keepalive {Ms}ms)", UserId, _keepAliveMs);
                _watchdog.Start(_keepAliveMs);
                await StateMachine.FireAsync(UserActions.WelcomeMessageSuccess);
                return;
            }
```
This requires `UserSequencer` to hold an `EventSubClientOptions` (or just the int). Add constructor param `int keepAliveTimeoutSeconds` and store it as `_optionsKeepaliveSeconds`; use that instead of `_options.KeepaliveTimeoutSeconds` (avoid pulling whole options). Replace the magic `_keepAliveMs = 10_100` initializer (line 41) with `_keepAliveMs = 10_100; // replaced on welcome/proceed`.

- [ ] **Step 4: Modify `EventProvider`**

Add constructor parameters `ReplayProtection replayProtection` and pass it (plus keepalive seconds) to the `UserSequencer`. `EventProvider` does not have options today; thread `int keepAliveTimeoutSeconds` from `EventSubClient`. Update the `Create()` method's `new UserSequencer(...)` call to pass `replayProtection` and `keepAliveTimeoutSeconds`. Store both as fields.

- [ ] **Step 5: Modify `EventSubClient`**

`EventSubClient` constructor: add `ReplayProtection replayProtection` parameter (DI provides the singleton) and store it; also read `options.Value.KeepaliveTimeoutSeconds`. In `AddUserAsync`, pass both into `new EventProvider(...)`.

- [ ] **Step 6: Run tests + build**

Run: `dotnet build "TwitchEventSubWebsocket.sln" -c Debug --nologo` → expect remaining errors only in DI registration (Task 10) and any Phase4 `EventSubClient` constructor test. Run `--filter "FullyQualifiedName~RawMessageTests|FullyQualifiedName~KeepaliveConfigTests|FullyQualifiedName~Phase6Tests"`.
Expected: those pass once Task 10 updates DI + the Phase4 `CreateClientWithMocks` helper. If the Phase4 test `EventSubClientHostedServiceTests.CreateClientWithMocks` fails to compile, update it to pass `new ReplayProtection(100)` as the new arg.

- [ ] **Step 7: Commit (bundle with Task 7 + Task 10)**

```
git add "Twitch EventSub library/User/UserSequencer.cs" "Twitch EventSub library/User/EventProvider.cs" "Twitch EventSub library/EventSubClient.cs" "Twitch EventSub library/CoreFunctions/ShardSequencer.cs" "Twitch EventSub library/CoreFunctions/IShardBinding.cs" "Twitch EventSub library/CoreFunctions/ShardBinding.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/RawMessageTests.cs TwitchEventSub_Websocket.Tests/Phase7Tests/KeepaliveConfigTests.cs TwitchEventSub_Websocket.Tests/Phase6Tests/
git commit -m "feat(messages): ShardInbound stream, raw callback always fires, shared dedup, keepalive from config"
```

---

### Task 10: DI rewire — register pipeline + shared ReplayProtection from options; delete EventRouter

**Files:**
- Modify: `Twitch EventSub library/ServiceCollectionExtensions.cs`
- Delete: `Twitch EventSub library/CoreFunctions/EventRouter.cs`, `Twitch EventSub library/IEventRouter.cs`, `TwitchEventSub_Websocket.Tests/Phase5Tests/EventRouterTests.cs`
- Modify: `Twitch EventSub library/CoreFunctions/ShardManager.cs` if it attaches pipeline (it creates shards) — wire `MessagePipeline.Attach(sequencer.Messages)` per new shard, and `RegisterUser` on bind.

- [ ] **Step 1: Update DI registration**

In `ServiceCollectionExtensions.cs` `AddTwitchEventSubClient` (99–119):
- Replace `services.AddSingleton<ReplayProtection>(sp => new ReplayProtection(100));` with:
```csharp
            services.AddSingleton<ReplayProtection>(sp =>
                new ReplayProtection(sp.GetRequiredService<IOptions<EventSubClientOptions>>().Value.DedupWindowSize));
```
(add `using Microsoft.Extensions.Options;`)
- Remove `services.AddSingleton<IEventRouter, EventRouter>();`
- Add `services.AddSingleton<IMessagePipeline, MessagePipeline>();`

- [ ] **Step 2: Delete EventRouter + its test**

```
git rm "Twitch EventSub library/CoreFunctions/EventRouter.cs" "Twitch EventSub library/IEventRouter.cs" TwitchEventSub_Websocket.Tests/Phase5Tests/EventRouterTests.cs
```

- [ ] **Step 3: Wire pipeline as the production router (option b)**

The `MessagePipeline` singleton is the routing owner. Wire it through the user lifecycle:

(a) `EventProvider` receives `IMessagePipeline` (constructor param, from DI via `EventSubClient`). When a shard binding is acquired in `StartAsync`, attach the shard stream once per shard and register this user:
```csharp
            // After acquiring _shardBinding and applying it to the sequencer:
            _messagePipeline.RegisterUser(_userId, _userSequencer.HandleInboundAsync);
            _pipelineAttachment ??= _messagePipeline.Attach(_shardBinding.ShardStream);
```
This requires `IShardBinding` to expose the raw shard stream for attachment: add `IObservable<ShardInbound> ShardStream { get; }` to `IShardBinding` (implemented as `_sequencer.Messages`). NOTE: a shard shared by multiple users must be `Attach`ed only once — `MessagePipeline.Attach` is idempotent per stream OR `ShardManager` owns the single attachment. **Chosen:** `ShardManager` attaches each shard's stream to the pipeline at shard creation (single attach per shard); `EventProvider` only calls `RegisterUser`/`UnregisterUser`. Revise accordingly:
  - `ShardManager` gets `IMessagePipeline` injected; in the new-shard branch (after `CreateShard`), call `_messagePipeline.Attach(sequencer.Messages)` and store the `IDisposable` in `ShardContext` for disposal when the shard is released.
  - `EventProvider.StartAsync`: `_messagePipeline.RegisterUser(_userId, _userSequencer.HandleInboundAsync);`
  - `EventProvider.StopAsync`: `_messagePipeline.UnregisterUser(_userId);`

(b) `ShardContext` gains `public IDisposable? PipelineAttachment { get; set; }`; disposed in `ReleaseUserFromShardAsync` when the shard is removed.

- [ ] **Step 3b: Update tests for the routing move**
- `Phase6Tests/ShardBindingRoutingTests.cs`: delete (routing now lives in `MessagePipeline`, covered by `MessagePipelineTests`).
- `Phase6Tests/EventProviderBindingTests.cs`: `EventProvider` constructor now takes `IMessagePipeline` and `ReplayProtection` — update the construction helper to pass `new MessagePipeline()` and `new ReplayProtection(100)`.
- `Phase2Tests/ShardManagerTests.cs` + `Phase6Tests/ShardManagerConnectTests.cs`: `ShardManager` constructor/`TestableShardManager` now takes `IMessagePipeline` — pass `new MessagePipeline(NullLogger<MessagePipeline>.Instance)`.

- [ ] **Step 4: Build + full test run**

```
Get-Process -Name "TwitchEventSub.LiveHarness" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build "TwitchEventSubWebsocket.sln" -c Debug --nologo
dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo
```
Expected: 0 build errors; all tests pass (former EventRouter tests removed; new Phase7 tests green).

- [ ] **Step 5: Commit**

```
git add -A
git commit -m "refactor(di): register MessagePipeline + options-sized shared ReplayProtection; remove dead EventRouter"
```

---

## PHASE 5 — Condition-scoped subscription reconciliation (the #1 fix)

### Task 11: Scope `RunCheckAsync` to the user's slice

**Files:**
- Modify: `Twitch EventSub library/User/SubscriptionManager.cs`
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/SubscriptionScopingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `TwitchEventSub_Websocket.Tests/Phase7Tests/SubscriptionScopingTests.cs`:

```csharp
using Twitch.EventSub.API.Models;
using Twitch.EventSub.Messages.SharedContents;
using Twitch.EventSub.User;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class SubscriptionScopingTests
{
    [Fact]
    public void OwnedSlice_MatchesByBroadcasterUserId()
    {
        var all = new List<WebSocketSubscription>
        {
            new() { Id = "a1", Type = "channel.update", Version = "2", Condition = new Condition { BroadcasterUserId = "userA" } },
            new() { Id = "b1", Type = "channel.follow", Version = "2", Condition = new Condition { BroadcasterUserId = "userB" } },
            new() { Id = "a2", Type = "channel.chat.message", Version = "1", Condition = new Condition { BroadcasterUserId = "userA", UserId = "userA" } },
        };

        var slice = SubscriptionManager.OwnedSlice(all, "userA");

        Assert.Equal(new[] { "a1", "a2" }, slice.Select(s => s.Id).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void OwnedSlice_MatchesByUserId_AndModerator()
    {
        var all = new List<WebSocketSubscription>
        {
            new() { Id = "w1", Type = "user.whisper.message", Version = "1", Condition = new Condition { UserId = "userA" } },
            new() { Id = "m1", Type = "channel.follow", Version = "2", Condition = new Condition { BroadcasterUserId = "userB", ModeratorUserId = "userA" } },
            new() { Id = "x1", Type = "channel.update", Version = "2", Condition = new Condition { BroadcasterUserId = "userB" } },
        };

        var slice = SubscriptionManager.OwnedSlice(all, "userA");

        Assert.Equal(new[] { "m1", "w1" }, slice.Select(s => s.Id).OrderBy(x => x).ToArray());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~SubscriptionScopingTests"`
Expected: FAIL — `SubscriptionManager.OwnedSlice` does not exist.

- [ ] **Step 3: Add `OwnedSlice` and use it in `RunCheckAsync`**

In `Twitch EventSub library/User/SubscriptionManager.cs`, add a static helper:

```csharp
        /// <summary>
        /// Returns the subset of conduit subscriptions owned by this user, identified by condition:
        /// broadcaster_user_id, user_id, or moderator_user_id equal to the user id.
        /// </summary>
        public static List<WebSocketSubscription> OwnedSlice(IEnumerable<WebSocketSubscription> all, string userId)
        {
            return all.Where(s =>
                s?.Condition != null &&
                (s.Condition.BroadcasterUserId == userId ||
                 s.Condition.UserId == userId ||
                 s.Condition.ModeratorUserId == userId)).ToList();
        }
```

Then in `RunCheckAsync`, after each `ApiTryGetAllSubscriptionsAsync` call, restrict the working set to the slice. Specifically:

- In the first cleanup loop (73–88), iterate `OwnedSlice(getSubscriptionsResponse.Data, userId)` instead of `getSubscriptionsResponse.Data`.
- In the second reconciliation block (98–140), set `var activeSubscriptions = OwnedSlice(getSubscriptionsResponse.Data, userId);` instead of `getSubscriptionsResponse.Data`. The `extraSubscriptions`/`missingSubscriptions` diffs then operate only on the user's slice — so another user's subs are never seen as "extra".

(Confirm `using Twitch.EventSub.Messages.SharedContents;` is present for `WebSocketSubscription`.)

- [ ] **Step 4: Run tests**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~SubscriptionScopingTests|FullyQualifiedName~Phase3Tests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/User/SubscriptionManager.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/SubscriptionScopingTests.cs
git commit -m "fix(subscriptions): scope reconciliation to the user's condition slice (no cross-user deletion)"
```

---

### Task 12: Exact usage accounting (per-user owned-slice count)

**Files:**
- Modify: `Twitch EventSub library/User/SubscriptionManager.cs` (add `LastReconcileReport`)
- Test: extend `SubscriptionScopingTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `SubscriptionScopingTests.cs`:

```csharp
    [Fact]
    public void ReconcileReport_RecordsOwnedCount()
    {
        var report = new SubscriptionManager.ReconcileReport(userId: "userA", ownedCount: 2, created: 1, removed: 0);
        Assert.Equal("userA", report.UserId);
        Assert.Equal(2, report.OwnedCount);
        Assert.Equal(1, report.Created);
        Assert.Equal(0, report.Removed);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~ReconcileReport_RecordsOwnedCount"`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Add the report type and populate it**

In `SubscriptionManager.cs` add:

```csharp
        /// <summary>Exact per-user subscription accounting from the last reconciliation pass.</summary>
        public sealed record ReconcileReport(string UserId, int OwnedCount, int Created, int Removed);

        /// <summary>The most recent reconciliation report for this user (null until first RunCheckAsync).</summary>
        public ReconcileReport? LastReport { get; private set; }
```

In `RunCheckAsync`, count created/removed during the reconciliation and set `LastReport = new ReconcileReport(userId, ownedSliceCount, created, removed);` before returning true. Log it at information level: `logger.LogInformation("[SubscriptionManager] user {U}: owned={O} created={C} removed={R}", userId, ownedSliceCount, created, removed);`.

- [ ] **Step 4: Run tests**

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~SubscriptionScopingTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```
git add "Twitch EventSub library/User/SubscriptionManager.cs" TwitchEventSub_Websocket.Tests/Phase7Tests/SubscriptionScopingTests.cs
git commit -m "feat(subscriptions): exact per-user usage accounting via ReconcileReport"
```

---

## PHASE 6 — Synthetic + Fuzz suites

### Task 13: Synthetic scenario test

**Files:**
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/SyntheticScenarioTests.cs`

- [ ] **Step 1: Write the test (drives MessagePipeline end-to-end with a scripted sequence)**

Create `SyntheticScenarioTests.cs`:

```csharp
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Twitch.EventSub.Messages.WelcomeMessage;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class SyntheticScenarioTests
{
    private static ShardInbound N(string b, string id) => new("{}", new WebSocketNotificationMessage
    {
        Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
        Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = b } } }
    });
    private static ShardInbound K(string id) => new("{}", new WebSocketKeepAliveMessage
    { Metadata = new WebSocketMessageMetadata { MessageType = "session_keepalive", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") } });

    [Fact]
    public async Task TwoUsers_InterleavedSequence_EachGetsOwnSliceInOrder()
    {
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var a = new List<string>(); var b = new List<string>(); int keepA = 0, keepB = 0;
        pipeline.RegisterUser("A", i => { if (i.Parsed is WebSocketKeepAliveMessage) Interlocked.Increment(ref keepA); else lock (a) a.Add(i.Parsed.Metadata!.MessageId); return Task.CompletedTask; });
        pipeline.RegisterUser("B", i => { if (i.Parsed is WebSocketKeepAliveMessage) Interlocked.Increment(ref keepB); else lock (b) b.Add(i.Parsed.Metadata!.MessageId); return Task.CompletedTask; });
        pipeline.Attach(subject);

        foreach (var m in new[] { K("k1"), N("A", "a1"), N("B", "b1"), N("A", "a2"), K("k2"), N("B", "b2") })
            subject.OnNext(m);
        await Task.Delay(100);

        Assert.Equal(new[] { "a1", "a2" }, a);
        Assert.Equal(new[] { "b1", "b2" }, b);
        Assert.Equal(2, keepA);   // keepalives broadcast to both
        Assert.Equal(2, keepB);
    }
}
```

- [ ] **Step 2: Run — expect PASS** (pipeline already implemented in Task 8)

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~SyntheticScenarioTests"`
Expected: PASS. (If ordering flakes, that's a real bug in Task 8 — fix `.Concat()` usage.)

- [ ] **Step 3: Commit**

```
git add TwitchEventSub_Websocket.Tests/Phase7Tests/SyntheticScenarioTests.cs
git commit -m "test(synthetic): two-user interleaved shard sequence routing + ordering"
```

---

### Task 14: Fuzz test

**Files:**
- Test: `TwitchEventSub_Websocket.Tests/Phase7Tests/FuzzPipelineTests.cs`

- [ ] **Step 1: Write the seeded fuzz test**

Create `FuzzPipelineTests.cs`:

```csharp
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using Twitch.EventSub.API.Models;
using Twitch.EventSub.CoreFunctions;
using Twitch.EventSub.Messages.KeepAliveMessage;
using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.SharedContents;
using Xunit;
using Xunit.Abstractions;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class FuzzPipelineTests
{
    private readonly ITestOutputHelper _out;
    public FuzzPipelineTests(ITestOutputHelper o) => _out = o;

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task RandomInterleavings_NoCrossUserLeak_NoDuplicateDelivery(int seed)
    {
        var rng = new Random(seed);
        var users = new[] { "U0", "U1", "U2" };
        var pipeline = new MessagePipeline(NullLogger<MessagePipeline>.Instance);
        var subject = new Subject<ShardInbound>();
        var delivered = users.ToDictionary(u => u, _ => new List<string>());
        foreach (var u in users)
        {
            var key = u;
            pipeline.RegisterUser(u, i =>
            {
                if (i.Parsed is WebSocketNotificationMessage)
                    lock (delivered) delivered[key].Add(i.Parsed.Metadata!.MessageId);
                return Task.CompletedTask;
            });
        }
        pipeline.Attach(subject);

        var expected = users.ToDictionary(u => u, _ => new List<string>());
        int n = 500;
        for (int i = 0; i < n; i++)
        {
            var kind = rng.Next(100);
            if (kind < 70)
            {
                var u = users[rng.Next(users.Length)];
                var id = $"m{i}";
                expected[u].Add(id);
                subject.OnNext(new ShardInbound("{}", new WebSocketNotificationMessage
                {
                    Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = id, MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
                    Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = u } } }
                }));
            }
            else if (kind < 85)
            {
                // keepalive (broadcast) — must not appear in notification lists
                subject.OnNext(new ShardInbound("{}", new WebSocketKeepAliveMessage
                { Metadata = new WebSocketMessageMetadata { MessageType = "session_keepalive", MessageId = $"k{i}", MessageTimestamp = System.DateTime.UtcNow.ToString("o") } }));
            }
            else
            {
                // notification for an unregistered user — must be dropped, no crash
                subject.OnNext(new ShardInbound("{}", new WebSocketNotificationMessage
                {
                    Metadata = new WebSocketMessageMetadata { MessageType = "notification", MessageId = $"x{i}", MessageTimestamp = System.DateTime.UtcNow.ToString("o") },
                    Payload = new WebSocketNotificationPayload { Subscription = new WebSocketSubscription { Condition = new Condition { BroadcasterUserId = "GHOST" } } }
                }));
            }
        }
        await Task.Delay(300);

        foreach (var u in users)
        {
            // No cross-user leak: everything delivered to u was addressed to u (in arrival order).
            Assert.Equal(expected[u], delivered[u]);
            // No duplicate delivery.
            Assert.Equal(delivered[u].Distinct().Count(), delivered[u].Count);
        }
        _out.WriteLine($"seed={seed} ok");
    }
}
```

- [ ] **Step 2: Run — expect PASS** (on failure, the seed prints for repro)

Run: `dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo --filter "FullyQualifiedName~FuzzPipelineTests"`
Expected: PASS (5 seeds).

- [ ] **Step 3: Commit**

```
git add TwitchEventSub_Websocket.Tests/Phase7Tests/FuzzPipelineTests.cs
git commit -m "test(fuzz): randomized interleavings assert no cross-user leak / no duplicate delivery"
```

---

## PHASE 7 — Final verification

### Task 15: Full suite + live single-user re-verify

- [ ] **Step 1: Full test run**

```
Get-Process -Name "TwitchEventSub.LiveHarness" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet test "TwitchEventSubWebsocket.sln" -c Debug --nologo
```
Expected: all green (142 baseline minus 5 removed EventRouter tests, plus new Phase7 tests).

- [ ] **Step 2: Build harness + live smoke (single user)**

```
dotnet build "TwitchEventSub.LiveHarness/TwitchEventSub.LiveHarness.csproj" -c Debug --nologo
```
Run the harness (user runs it; cached login). Confirm in the log: user reaches Running; `raw:` lines now appear (raw callback fixed); events deliver; Ctrl-C tears down cleanly. Then stop and delete any leftover conduit via API as before.

- [ ] **Step 3: Update memory + audit doc**

Mark in `docs/superpowers/specs/2026-05-31-full-codebase-audit.md` which findings are fixed (1,2,3,4,5,8) and which deferred to Spec B (6, redundancy, event-key dedup). Note #10 (watchdog) retracted and #6-downcast retracted.

- [ ] **Step 4: Final commit**

```
git add -A
git commit -m "docs: mark Spec A findings resolved; note deferrals to Spec B"
```

---

## Self-review notes (addressed) — option (b): pipeline is the production router
- **Spec coverage:** #1 (T11/T12), #2 (T6/T7/T9), #3 (T1/T2), #4 (T7/T9), #5 ordered (T7 `.Concat()` shard-internal + T8 pipeline `.Concat()`), #8 (T3/T4), dedup single shared (T9/T10), config (T5), removals incl. EventRouter + `ShardBinding.IsForUser`/`UserMessages` (T10), three suites (logic T1–T12, synthetic T13, fuzz T14). All-or-nothing untouched. Watchdog untouched.
- **Routing model:** `MessagePipeline` is the sole router. `ShardManager` attaches each shard's `Messages` stream to the pipeline once (single attach per shard); `EventProvider` calls `RegisterUser`/`UnregisterUser`; `UserSequencer.HandleInboundAsync` is the per-user entry. `ShardBinding` becomes lifecycle-only (`ShardStream`, `NegotiatedKeepaliveSeconds`, `OnShardLost`, `OnSessionIdChanged`). No DI-registered-but-unused component.
- **Deferred (Spec B):** event-key (cross-conduit) dedup, `conduit.shard.disabled` + recovery, multi-conduit redundancy, N user instances per conduit.
- **Type consistency:** `ShardInbound(Raw,Parsed)`, `ShardStream`, `NegotiatedKeepaliveSeconds`, `DedupWindowSize`, `OwnedSlice`, `ReconcileReport(UserId,OwnedCount,Created,Removed)`, `IMessagePipeline.RegisterUser/UnregisterUser/Attach`, `UserSequencer.HandleInboundAsync`, `ShardContext.PipelineAttachment` used consistently.
- **Constructor-signature blast radius (option b):** `UserSequencer` (+`ReplayProtection`, +`keepAliveTimeoutSeconds`), `EventProvider` (+`ReplayProtection`, +`IMessagePipeline`), `EventSubClient` (+`ReplayProtection`), `ShardManager` (+`IMessagePipeline`). Tests to update in the same tasks: `Phase4/EventSubClientHostedServiceTests.CreateClientWithMocks`, `Phase6/EventProviderBindingTests`, `Phase6/ShardManagerConnectTests`, `Phase2/ShardManagerTests`; delete `Phase6/ShardBindingRoutingTests` and `Phase5/EventRouterTests`.
- **Concurrency note for executor:** the pipeline broadcasts control frames to all users sequentially inside one `.Concat()` stage — fine for Spec A's per-user-shard reality; revisit fan-out parallelism only if Spec B load needs it.
