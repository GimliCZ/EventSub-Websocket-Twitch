# TwitchEventSub.LiveHarness

Interactive, local-only tool to smoke-test the conduit transport against **real Twitch**.
It logs you in via browser, opens a conduit + sharded WebSocket through the library, and prints
events to the console and to `harness-<timestamp>.log` (in the build output dir).

> Local manual test tool only — not packed/shipped. Uses a temporary test client id that will be
> removed after testing.

## One-time setup

1. **Trust the ASP.NET Core dev certificate** (Kestrel serves the `https://localhost:5000` redirect):

   ```
   dotnet dev-certs https --trust
   ```

2. **Set the client id and secret** (never committed; stored via user-secrets):

   ```
   dotnet user-secrets set "Twitch:ClientId" <your-client-id> --project TwitchEventSub.LiveHarness
   dotnet user-secrets set "Twitch:ClientSecret" <your-secret> --project TwitchEventSub.LiveHarness
   ```

   (Alternatively set the `TWITCH_CLIENT_ID` / `TWITCH_CLIENT_SECRET` environment variables.)

3. **Twitch dev console** — the client's OAuth Redirect URL must be exactly:

   ```
   https://localhost:5000
   ```

## Run

```
dotnet run --project TwitchEventSub.LiveHarness
```

- A browser opens for Twitch login (scopes: `user:read:chat`, `moderator:read:followers`).
- After login, the harness subscribes to: `channel.update`, `stream.online`, `stream.offline`,
  `channel.chat.message`, `channel.follow` (v2).
- Trigger events to verify:
  - **channel.update** — change your stream title/category in the Twitch dashboard
  - **channel.chat.message** — type in your own channel's chat
  - **stream.online/offline** — start/stop a stream
  - **channel.follow** — have someone follow your channel
- **Ctrl-C** stops the user, deletes it, and tears down the conduit (Twitch auto-removes its subscriptions).

## Credential caching

Tokens are cached in user-secrets (`secrets.json`) so re-runs don't require logging in again:

- **App access token** — reused until ~1 min before expiry, then re-minted.
- **User access + refresh token** — on startup the harness tries, in order: reuse the cached user
  token (if valid and has the required scopes) → refresh via the refresh token → full browser login.
  After any of these it persists the freshest tokens.
- **Client id** — read from `Twitch:ClientId` (user-secrets) or `TWITCH_CLIENT_ID`; required, no default.

To force a fresh login, clear the cached keys:

```
dotnet user-secrets remove "Twitch:UserAccessToken" --project TwitchEventSub.LiveHarness
dotnet user-secrets remove "Twitch:RefreshToken"     --project TwitchEventSub.LiveHarness
```

## How it works

`TwitchAuth` exposes granular OAuth operations directly against Twitch (app token, /oauth2/validate,
refresh, browser login), capturing the redirect with a short-lived Kestrel server on
`https://localhost:5000`. `TokenStore` reads/writes the user-secrets file. `Program.cs` resolves
credentials (cache → refresh → login), wires the library via `AddTwitchEventSub`, starts the host
(which creates the conduit), adds the user, and prints events.
