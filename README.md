# Twitch EventSub Websocket
<p align="center">
  <a href="https://www.nuget.org/packages/Twitch.EventSub.Websocket/2.2.0" target="_blank">
    <img src="https://img.shields.io/nuget/v/Twitch.EventSub.Websocket.svg?label=NuGet%20v" alt="NuGet version" style="max-height:300px;" />
    <img src="https://img.shields.io/nuget/dt/Twitch.EventSub.Websocket.svg?label=Downloads" alt="NuGet downloads" style="max-height:300px;" />
  </a>
  <img src="https://img.shields.io/badge/Platform-.NET%2010-orange.svg" style="max-height: 300px;" alt=".NET 9" />
  <img src="https://img.shields.io/github/license/GimliCZ/TwitchEventSub_Websocket" alt="License" />
  <br />
  <img src="https://img.shields.io/github/issues/GimliCZ/TwitchEventSub_Websocket" alt="Issues" />
  <img src="https://img.shields.io/github/stars/GimliCZ/TwitchEventSub_Websocket" alt="Stars" />
  <img src="https://img.shields.io/github/forks/GimliCZ/TwitchEventSub_Websocket" alt="Forks" />
  <img src="https://img.shields.io/github/last-commit/GimliCZ/TwitchEventSub_Websocket" alt="Last Commit" />
</p>

# About
* Handles multiple user communications with Twitch Eventsub via websocket 
* For more information on Twitch EventSub, refer to the [Twitch EventSub Documentation](https://dev.twitch.tv/docs/eventsub/).

## Implementation
* **Client Id** is identifier of your application
* **User Id** is identifier of twitch user
* **AccessToken** is token requested via bearer token of user
* TwitchEventSub Library contains set of enums **SubscriptionType** which are setting contens of list of subscriptions

#### INITIALIZATION
```csharp
EventSubClient(ClientId, logger);
```
#### SETUP
```csharp
public async Task<bool> SetupAsync(string UserId)
{
    var listOfSubs = new List<SubscriptionType>
    {
        // Add requested subscriptions
        SubscriptionType.ChannelFollow
    };
    _listOfSubs = listOfSubs;

    var resultAdd = await _eventSubClient.AddUserAsync(
        UserId,
        GetApiToken(),
        _listOfSubs).ConfigureAwait(false);

    if (resultAdd) 
    { 
        SetupEvents();
    }
    return resultAdd;
}
```
#### EVENT SUBSCRIPTIONS
```csharp
private void SetupEvents()
{
    var provider = _eventSubClient[_eventSubUserId];
    if (provider == null)
    {
        _logger.LogError("EventSub Provider returned null for user {UserId}", _eventSubUserId);
        return;
    }

    provider.OnRefreshTokenAsync -= EventSubClientOnRefreshTokenAsync;
    provider.OnRefreshTokenAsync += EventSubClientOnRefreshTokenAsync;
    provider.OnFollowEventAsync -= EventSubClientOnFollowEventAsync;
    provider.OnFollowEventAsync += EventSubClientOnFollowEventAsync;
    provider.OnUnexpectedConnectionTermination -= EventSubClientOnUnexpectedConnectionTermination;
    provider.OnUnexpectedConnectionTermination += EventSubClientOnUnexpectedConnectionTermination;

#if DEBUG
    // Print RAW messages only during DEBUG
    provider.OnRawMessageAsync -= EventSubClientOnRawMessageAsync;
    provider.OnRawMessageAsync += EventSubClientOnRawMessageAsync;
#endif
}
```

#### START FUNCTION
```csharp
await _eventSubClient.StartAsync(ownerId).ConfigureAwait(false);
```

#### STOP FUNCTION
```csharp
await _eventSubClient.StopAsync(_eventSubUserId).ConfigureAwait(false);
```

#### AUTHORIZATION
* **EventSub does not provide refresh token capabilities. You have to provide your own.**
* To function properly, you are required to subscribe to **EventSubClientOnRefreshToken** event in order to refresh token
```csharp
provider.OnRefreshTokenAsync -= EventSubClientOnRefreshTokenAsync;
provider.OnRefreshTokenAsync += EventSubClientOnRefreshTokenAsync;
```
* Then run your token refreshing function and pass new token
```csharp
private async Task EventSubClientOnRefreshTokenAsync(object sender, InvalidAccessTokenException e)
{
    _logger.LogInformation("Event Sub Attempting to refresh access token");
    _eventSubClient.UpdateUser(
        UserId,
        newAccessToken(),
        _listOfSubs
    );
}
```
#### RECOVERY
* During processing of users you may listen to **IsConnected** flag and **EventSubClientOnFollowEventAsync** to make sure client is working.
* On unexpected client can happen during stream termination, so make sure you detect it and stop client before you get external disconnect.
* Then you can do two things, You may completely remove entire user object. Or you may try to Start it again (Works only in disposed state). 
``` csharp
private async void RecoveryRoutineAsync()
{
    try
    {
        if (_eventSubClient.IsConnected(_eventSubUserId))
        {
            _logger.LogDebug("EventSubClient is already connected, skip recovery procedure");
            return;
        }

        _eventSubClient[_eventSubUserId].OnRefreshTokenAsync -= EventSubClientOnRefreshTokenAsync;
        _eventSubClient[_eventSubUserId].OnFollowEventAsync -= EventSubClientOnFollowEventAsync;
        _eventSubClient[_eventSubUserId].OnUnexpectedConnectionTermination -= EventSubClientOnUnexpectedConnectionTermination;
        _eventSubClient[_eventSubUserId].OnRawMessageAsync -= EventSubClientOnRawMessageAsync;

        var deletionCheck = await _eventSubClient.DeleteUserAsync(_eventSubUserId);
        if (!deletionCheck)
        {
            _logger.LogWarning("EventSub user was NOT gracefully terminated during reconnect attempt");
        }

        await SetupAsync(_eventSubUserId).ConfigureAwait(false);
        await _eventSubClient.StartAsync(ownerId).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "EventSubClientRecoveryRoutineAsync failed");
    }
}
```
## STATE DIAGRAM
![Alt text](https://github.com/GimliCZ/TwitchEventSub_Websocket/blob/feature/ReworkAndConduit/graphviz.png)

## License
This project is available under the MIT license. See the LICENSE file for more info.
