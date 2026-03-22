# Twitch EventSub Websocket
<p align="center">
  <a href="https://www.nuget.org/packages/Twitch.EventSub.Websocket/" target="_blank">
    <img src="https://img.shields.io/nuget/v/Twitch.EventSub.Websocket.svg?label=NuGet%20v" alt="NuGet version" style="max-height:300px;" />
    <img src="https://img.shields.io/nuget/dt/Twitch.EventSub.Websocket.svg?label=Downloads" alt="NuGet downloads" style="max-height:300px;" />
  </a>
  <img src="https://img.shields.io/badge/Platform-.NET%2010-orange.svg" style="max-height: 300px;" alt=".NET 10" />
  <img src="https://img.shields.io/github/license/GimliCZ/TwitchEventSub_Websocket" alt="License" />
  <br />
  <img src="https://img.shields.io/github/issues/GimliCZ/TwitchEventSub_Websocket" alt="Issues" />
  <img src="https://img.shields.io/github/stars/GimliCZ/TwitchEventSub_Websocket" alt="Stars" />
  <img src="https://img.shields.io/github/forks/GimliCZ/TwitchEventSub_Websocket" alt="Forks" />
  <img src="https://img.shields.io/github/last-commit/GimliCZ/TwitchEventSub_Websocket" alt="Last Commit" />
</p>

# About
* Handles multiple user communications with Twitch EventSub via websocket
* For more information on Twitch EventSub, refer to the [Twitch EventSub Documentation](https://dev.twitch.tv/docs/eventsub/).

---

## Migrating from v2 to v3

Version 3.0.0 introduces **Dependency Injection** as the primary setup path and replaces static API classes with injectable singletons. The following breaking changes require action.

### 1. Constructor — `EventSubClient` no longer accepts `clientId` directly

```csharp
// v2
var client = new EventSubClient("your-client-id", logger);

// v3 — use DI (see Setup below), or supply options manually
var client = new EventSubClient(
    Options.Create(new EventSubClientOptions { ClientId = "your-client-id" }),
    logger,
    twitchApi);
```

### 2. `TwitchApi` and `TwitchApiConduit` are no longer static

If you called these classes directly:

```csharp
// v2
await TwitchApi.SubscribeAsync(...);
await TwitchApiConduit.ConduitCreatorAsync(...);

// v3 — resolve from DI or construct with IHttpClientFactory
var api = new TwitchApi(httpClientFactory);
await api.SubscribeAsync(...);
```

In practice, you should not need to call these directly — `IEventSubClient` covers all normal usage.

### 3. DI registration replaces manual construction

```csharp
// v2
services.AddSingleton<IEventSubClient>(new EventSubClient("client-id", logger));

// v3
services.AddTwitchEventSub(options => options.ClientId = "your-client-id");
```

---

## Setup (v3)

### Quick start — single line DI registration

```csharp
// Program.cs / Startup.cs
services.AddTwitchEventSub(options =>
{
    options.ClientId = "your-client-id";
});
```

This registers:
- Two named `HttpClient` instances (`EventSubWebsocketTwitchApi`, `EventSubWebsocketTwitchApiConduit`) with standard resilience pipelines (retry, circuit breaker, timeout) and telemetry enrichment
- `TwitchApi` and `TwitchApiConduit` as singletons
- `IEventSubClient` as a singleton
- Logging if not already registered

### Advanced — custom HttpClient configuration

Use this when you need to configure the HTTP clients yourself (custom timeouts, proxy, etc.) before registering the client.

```csharp
services.Configure<EventSubClientOptions>(o => o.ClientId = "your-client-id");

services.AddHttpClient(HttpClientNames.TwitchApi, client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 5;
});

services.AddHttpClient(HttpClientNames.TwitchApiConduit);

services.AddSingleton<TwitchApi>();
services.AddSingleton<TwitchApiConduit>();
services.AddTwitchEventSubClient();
```

The named client name constants are available on `HttpClientNames`:

```csharp
HttpClientNames.TwitchApi          // "EventSubWebsocketTwitchApi"
HttpClientNames.TwitchApiConduit   // "EventSubWebsocketTwitchApiConduit"
```

---

## Implementation

* **Client Id** is the identifier of your Twitch application
* **User Id** is the identifier of a Twitch user
* **AccessToken** is a bearer token obtained for the user

`IEventSubClient` is the primary interface — inject it wherever you need it.

#### SETUP
```csharp
public async Task<bool> SetupAsync(string userId)
{
    var listOfSubs = new List<SubscriptionType>
    {
        SubscriptionType.ChannelFollow
    };
    _listOfSubs = listOfSubs;

    var resultAdd = await _eventSubClient.AddUserAsync(
        userId,
        GetApiToken(),
        _listOfSubs,
        allowRecovery: true).ConfigureAwait(false);

    if (resultAdd)
    {
        SetupEvents(userId);
    }
    return resultAdd;
}
```

#### EVENT SUBSCRIPTIONS
```csharp
private void SetupEvents(string userId)
{
    var provider = _eventSubClient[userId];
    if (provider == null)
    {
        _logger.LogError("EventSub Provider returned null for user {UserId}", userId);
        return;
    }

    provider.OnRefreshTokenAsync -= EventSubClientOnRefreshTokenAsync;
    provider.OnRefreshTokenAsync += EventSubClientOnRefreshTokenAsync;
    provider.OnFollowEventAsync -= EventSubClientOnFollowEventAsync;
    provider.OnFollowEventAsync += EventSubClientOnFollowEventAsync;
    provider.OnUnexpectedConnectionTermination -= EventSubClientOnUnexpectedConnectionTermination;
    provider.OnUnexpectedConnectionTermination += EventSubClientOnUnexpectedConnectionTermination;

#if DEBUG
    provider.OnRawMessageAsync -= EventSubClientOnRawMessageAsync;
    provider.OnRawMessageAsync += EventSubClientOnRawMessageAsync;
#endif
}
```

#### START
```csharp
await _eventSubClient.StartAsync(userId).ConfigureAwait(false);
```

#### STOP
```csharp
await _eventSubClient.StopAsync(userId).ConfigureAwait(false);
```

#### AUTHORIZATION
* EventSub does not provide token refresh capabilities — you must implement your own.
* Subscribe to `OnRefreshTokenAsync` to be notified when a token refresh is needed.

```csharp
provider.OnRefreshTokenAsync -= EventSubClientOnRefreshTokenAsync;
provider.OnRefreshTokenAsync += EventSubClientOnRefreshTokenAsync;
```

```csharp
private async Task EventSubClientOnRefreshTokenAsync(object sender, RefreshRequestArgs e)
{
    _logger.LogInformation("EventSub requesting token refresh for user {UserId}", e.UserId);
    _eventSubClient.UpdateUser(
        e.UserId,
        await GetNewAccessTokenAsync(),
        _listOfSubs);
}
```

#### RECOVERY
Listen to `IsConnected` and `OnUnexpectedConnectionTermination` to detect failures and recover.

```csharp
private async void RecoveryRoutineAsync(string userId)
{
    try
    {
        if (_eventSubClient.IsConnected(userId))
        {
            _logger.LogDebug("EventSubClient is already connected, skipping recovery");
            return;
        }

        var provider = _eventSubClient[userId];
        if (provider != null)
        {
            provider.OnRefreshTokenAsync -= EventSubClientOnRefreshTokenAsync;
            provider.OnFollowEventAsync -= EventSubClientOnFollowEventAsync;
            provider.OnUnexpectedConnectionTermination -= EventSubClientOnUnexpectedConnectionTermination;
            provider.OnRawMessageAsync -= EventSubClientOnRawMessageAsync;
        }

        var deleted = await _eventSubClient.DeleteUserAsync(userId);
        if (!deleted)
        {
            _logger.LogWarning("EventSub user was not gracefully terminated during recovery");
        }

        await SetupAsync(userId).ConfigureAwait(false);
        await _eventSubClient.StartAsync(userId).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "EventSub recovery routine failed");
    }
}
```

## STATE DIAGRAM
![Alt text](https://github.com/GimliCZ/TwitchEventSub_Websocket/blob/feature/ReworkAndConduit/graphviz.png)

## License
This project is available under the MIT license. See the LICENSE file for more info.
