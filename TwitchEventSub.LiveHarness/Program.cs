using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Twitch.EventSub;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.Interfaces;
using Twitch.EventSub.User;
using TwitchEventSub.LiveHarness;

// ── Self-diagnostic mode ──────────────────────────────────────────────────
// `dotnet run -- diagnose` connects a raw WebSocket and dumps frames (no auth needed).
if (args.Length > 0 && args[0] == "diagnose")
{
    return await Diagnostics.RunAsync();
}

// ── Config ────────────────────────────────────────────────────────────────
const string UserSecretsId = "twitch-eventsub-liveharness";

// Keys used in user-secrets (secrets.json) for cached, resilient credential storage.
const string KAppToken = "Twitch:AppAccessToken";
const string KAppExpiry = "Twitch:AppTokenExpiresAt";
const string KUserToken = "Twitch:UserAccessToken";
const string KRefresh = "Twitch:RefreshToken";
const string KUserId = "Twitch:UserId";
const string KLogin = "Twitch:UserLogin";

var config = new ConfigurationBuilder()
    .AddUserSecrets<TwitchAuth>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Client id and secret come from user-secrets or environment — never hardcoded.
//   dotnet user-secrets set "Twitch:ClientId" <id>     --project TwitchEventSub.LiveHarness
//   dotnet user-secrets set "Twitch:ClientSecret" <s>  --project TwitchEventSub.LiveHarness
var clientId = config["Twitch:ClientId"] ?? Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
var clientSecret = config["Twitch:ClientSecret"] ?? Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
if (string.IsNullOrWhiteSpace(clientId))
{
    Console.Error.WriteLine(
        "Missing client id. Set it once with:\n" +
        "  dotnet user-secrets set \"Twitch:ClientId\" <value> --project TwitchEventSub.LiveHarness\n" +
        "or set the TWITCH_CLIENT_ID environment variable.");
    return 1;
}
if (string.IsNullOrWhiteSpace(clientSecret))
{
    Console.Error.WriteLine(
        "Missing client secret. Set it once with:\n" +
        "  dotnet user-secrets set \"Twitch:ClientSecret\" <value> --project TwitchEventSub.LiveHarness\n" +
        "or set the TWITCH_CLIENT_SECRET environment variable.");
    return 1;
}

// ── Logging (console + file) ──────────────────────────────────────────────
var logPath = Path.Combine(AppContext.BaseDirectory, $"harness-{DateTime.Now:yyyyMMdd-HHmmss}.log");
var logFile = new StreamWriter(logPath, append: true) { AutoFlush = true };
void Log(string msg)
{
    var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
    Console.WriteLine(line);
    lock (logFile) logFile.WriteLine(line);
}
Log($"Live harness starting. Log file: {logPath}");
Log($"Client id: {clientId}");

// Scopes for the chosen events: channel.update / stream.* need none;
// channel.chat.message needs user:read:chat; channel.follow v2 needs moderator:read:followers.
// channel.chat.message over a conduit requires user:bot in addition to user:read:chat.
string[] scopes = ["user:read:chat", "user:bot", "moderator:read:followers"];

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; Log("Ctrl-C received — shutting down…"); cts.Cancel(); };

var store = new TokenStore(UserSecretsId);
var auth = new TwitchAuth(clientId, clientSecret, Log);

// ── App access token (cached until ~1 min before expiry) ──────────────────
string appToken;
try
{
    var cachedApp = store.Get(KAppToken);
    var appExpiry = store.GetDate(KAppExpiry);
    if (cachedApp != null && appExpiry is { } exp && exp > DateTimeOffset.UtcNow.AddMinutes(1))
    {
        appToken = cachedApp;
        Log($"App access token reused from cache (expires {exp:u}).");
    }
    else
    {
        var (token, expiresIn) = await auth.GetAppTokenAsync(cts.Token);
        appToken = token;
        store.Set(KAppToken, token);
        store.SetDate(KAppExpiry, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        store.Save();
        Log("App access token obtained (client_credentials) and cached.");
    }
}
catch (Exception ex)
{
    Log("Failed to obtain app access token: " + ex.Message);
    return 1;
}

// ── User access token (reuse cached → refresh → browser login) ────────────
string? userToken = null, refreshToken = store.Get(KRefresh), userId = null, userLogin = null;

bool HasAllScopes(string[] granted) => scopes.All(s => granted.Contains(s, StringComparer.OrdinalIgnoreCase));

async Task<bool> TryAdoptAsync(string token)
{
    var v = await auth.ValidateAsync(token, cts.Token);
    if (!v.Valid || v.UserId == null || !HasAllScopes(v.Scopes)) return false;
    userToken = token; userId = v.UserId; userLogin = v.Login;
    return true;
}

try
{
    // 1) Cached access token still valid (with required scopes)?
    if (store.Get(KUserToken) is { } cachedUser && await TryAdoptAsync(cachedUser))
        Log($"User access token reused from cache (user {userLogin}, id={userId}).");

    // 2) Otherwise refresh.
    if (userToken == null && refreshToken != null)
    {
        try
        {
            var (access, refresh) = await auth.RefreshAsync(refreshToken, cts.Token);
            refreshToken = refresh;
            if (await TryAdoptAsync(access))
                Log($"User access token refreshed (user {userLogin}, id={userId}).");
        }
        catch (Exception ex) { Log("Refresh failed, falling back to browser login: " + ex.Message); }
    }

    // 3) Otherwise full browser login.
    if (userToken == null)
    {
        var (access, refresh) = await auth.BrowserLoginAsync(scopes, cts.Token);
        refreshToken = refresh;
        if (!await TryAdoptAsync(access))
        {
            Log("Login succeeded but token validation failed (missing scopes?).");
            return 1;
        }
        Log($"Logged in as {userLogin} (id={userId}).");
    }
}
catch (Exception ex)
{
    Log("Authentication failed: " + ex.Message);
    return 1;
}

// Persist the freshest user credentials.
store.Set(KUserToken, userToken!);
if (refreshToken != null) store.Set(KRefresh, refreshToken);
store.Set(KUserId, userId!);
if (userLogin != null) store.Set(KLogin, userLogin);
store.Save();

// ── Host + library wiring ─────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder();
// Override with HARNESS_LOGLEVEL=Debug to see shard-level message receipt (keepalives/notifications).
var levelName = Environment.GetEnvironmentVariable("HARNESS_LOGLEVEL") ?? "Information";
builder.Logging.SetMinimumLevel(Enum.TryParse<LogLevel>(levelName, true, out var lvl) ? lvl : LogLevel.Information);
// Override with HARNESS_REDUNDANCY=2 (1–3) to run the same subscriptions across N conduits.
var redundancy = int.TryParse(Environment.GetEnvironmentVariable("HARNESS_REDUNDANCY"), out var rf) ? Math.Clamp(rf, 1, 3) : 1;
Log($"Redundancy factor: {redundancy}");
builder.Services.AddTwitchEventSub(o =>
{
    o.ClientId = clientId;
    o.AppAccessToken = appToken;
    o.RedundancyFactor = redundancy;
});
using var host = builder.Build();

await host.StartAsync(cts.Token);
Log("Host started — conduit initialized.");

var client = host.Services.GetRequiredService<IEventSubClient>();

var subs = new List<SubscriptionTypes>
{
    SubscriptionTypes.ChannelUpdate,
    SubscriptionTypes.StreamOnline,
    SubscriptionTypes.StreamOffline,
    SubscriptionTypes.ChannelChatMessage,
    SubscriptionTypes.ChannelFollow,
};

if (!await client.AddUserAsync(userId!, userToken!, subs, allowRecovery: false))
{
    Log("AddUserAsync failed.");
    await host.StopAsync();
    return 1;
}

// Events live on the concrete EventProvider; the indexer returns IEventProvider, so downcast.
if (client[userId!] is not EventProvider provider)
{
    Log("Could not resolve EventProvider for user.");
    await host.StopAsync();
    return 1;
}

static string Dump(object e)
{
    try { return JsonSerializer.Serialize(e, new JsonSerializerOptions { WriteIndented = false }); }
    catch { return e.ToString() ?? "<null>"; }
}
Task Print(string label, object e) { Log($"EVENT {label}: {Dump(e)}"); return Task.CompletedTask; }

provider.OnUpdateEventAsync        += (_, e) => Print("channel.update", e);
provider.OnStreamOnlineEventAsync  += (_, e) => Print("stream.online", e);
provider.OnStreamOfflineEventAsync += (_, e) => Print("stream.offline", e);
provider.OnChatEventAsync          += (_, e) => Print("channel.chat.message", e);
provider.OnFollowEventAsync        += (_, e) => Print("channel.follow", e);
provider.OnRawMessageAsync         += (_, raw) => { Log($"raw: {raw}"); return Task.CompletedTask; };
provider.OnUnexpectedConnectionTermination += (_, reason) => Log($"connection terminated: {reason}");

// Refresh handler: mint a new user token, persist it, and push it into the client.
provider.OnRefreshTokenAsync += async (_, e) =>
{
    try
    {
        Log("Access token refresh requested.");
        if (refreshToken == null) { Log("No refresh token available."); return; }
        var (access, refresh) = await auth.RefreshAsync(refreshToken, cts.Token);
        userToken = access; refreshToken = refresh;
        store.Set(KUserToken, access);
        store.Set(KRefresh, refresh);
        store.Save();
        client.UpdateUser(userId!, access, subs);
        Log("Access token refreshed, cached, and pushed to client.");
    }
    catch (Exception ex) { Log("Token refresh failed: " + ex.Message); }
};

await client.StartAsync(userId!);
Log("User started. Watching for events…");
Log("");
Log("Trigger events to verify:");
Log("  - channel.update         -> change your stream title/category in the Twitch dashboard");
Log("  - channel.chat.message   -> type a message in your own channel's chat");
Log("  - stream.online/offline  -> start/stop a stream");
Log("  - channel.follow         -> have someone follow your channel");
Log("");
Log("Press Ctrl-C to stop and tear down the conduit.");

// ── Wait until cancelled ──────────────────────────────────────────────────
try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (TaskCanceledException) { /* expected on Ctrl-C */ }

// ── Teardown ──────────────────────────────────────────────────────────────
Log("Tearing down…");
await client.DeleteUserAsync(userId!);
await host.StopAsync(CancellationToken.None); // ConduitOrchestrator.TeardownAsync deletes the conduit
Log("Clean shutdown complete.");
return 0;
