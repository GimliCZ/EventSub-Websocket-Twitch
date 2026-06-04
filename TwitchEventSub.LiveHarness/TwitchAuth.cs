using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TwitchEventSub.LiveHarness;

/// <summary>
/// Low-level Twitch OAuth operations for the live harness (granular so the caller can cache):
///  - <see cref="GetAppTokenAsync"/>: client_credentials → app access token + lifetime
///  - <see cref="ValidateAsync"/>: /oauth2/validate → validity, user id/login, scopes
///  - <see cref="RefreshAsync"/>: refresh_token → new access + refresh token
///  - <see cref="BrowserLoginAsync"/>: authorization_code via a short-lived Kestrel server on
///    https://localhost:5000 (ASP.NET Core dev cert) → access + refresh token
/// All calls go directly to Twitch; this does not use the library under test.
/// </summary>
public sealed class TwitchAuth(string clientId, string clientSecret, Action<string> log)
{
    // Must exactly match the redirect URI registered on the Twitch client.
    private const string RedirectUri = "https://localhost:5000";
    private static readonly HttpClient Http = new();

    public sealed record Validation(bool Valid, string? UserId, string? Login, string[] Scopes, int ExpiresIn);

    public async Task<(string token, int expiresInSeconds)> GetAppTokenAsync(CancellationToken ct)
    {
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "client_credentials"
        });
        using var resp = await Http.PostAsync("https://id.twitch.tv/oauth2/token", body, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"client_credentials failed: {(int)resp.StatusCode} {json}");
        var root = JsonDocument.Parse(json).RootElement;
        var token = root.GetProperty("access_token").GetString()!;
        var expires = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
        return (token, expires);
    }

    /// <summary>Validates a user token via /oauth2/validate. Returns Valid=false (never throws) on 401.</summary>
    public async Task<Validation> ValidateAsync(string userToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        req.Headers.Add("Authorization", $"OAuth {userToken}");
        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            return new Validation(false, null, null, [], 0);
        var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).RootElement;
        var scopes = root.TryGetProperty("scopes", out var s) && s.ValueKind == JsonValueKind.Array
            ? s.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            : [];
        return new Validation(
            true,
            root.TryGetProperty("user_id", out var u) ? u.GetString() : null,
            root.TryGetProperty("login", out var l) ? l.GetString() : null,
            scopes,
            root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 0);
    }

    /// <summary>Exchanges a refresh token for a fresh access + refresh token.</summary>
    public async Task<(string access, string refresh)> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        using var resp = await Http.PostAsync("https://id.twitch.tv/oauth2/token", body, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"refresh_token failed: {(int)resp.StatusCode} {json}");
        var root = JsonDocument.Parse(json).RootElement;
        return (root.GetProperty("access_token").GetString()!,
                root.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? refreshToken : refreshToken);
    }

    /// <summary>Full interactive Authorization Code login. Opens the browser and captures the redirect.</summary>
    public async Task<(string access, string refresh)> BrowserLoginAsync(string[] scopes, CancellationToken ct)
    {
        var codeTcs = new TaskCompletionSource<(string? code, string? error)>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Full builder (not slim) so Kestrel HTTPS configuration is wired and the https:// URL uses the dev cert.
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(RedirectUri);
        var app = builder.Build();
        app.MapGet("/", (HttpContext httpCtx) =>
        {
            var code = httpCtx.Request.Query["code"].ToString();
            var error = httpCtx.Request.Query["error"].ToString();
            codeTcs.TrySetResult(
                (string.IsNullOrEmpty(code) ? null : code,
                 string.IsNullOrEmpty(error) ? null : error));
            var msg = string.IsNullOrEmpty(error)
                ? "<h2>Login complete.</h2><p>You can close this tab and return to the console.</p>"
                : $"<h2>Login failed: {System.Net.WebUtility.HtmlEncode(error)}</h2>";
            return Results.Content($"<html><body style='font-family:sans-serif'>{msg}</body></html>", "text/html");
        });

        await app.StartAsync(ct);
        try
        {
            var scope = Uri.EscapeDataString(string.Join(' ', scopes));
            var authUrl =
                $"https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={clientId}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope={scope}";

            log("Opening browser for Twitch login. If it doesn't open, paste this URL:");
            log("  " + authUrl);
            TryOpenBrowser(authUrl);

            var completed = await Task.WhenAny(codeTcs.Task, Task.Delay(TimeSpan.FromMinutes(5), ct));
            if (completed != codeTcs.Task)
                throw new TimeoutException("Timed out waiting for Twitch login redirect (5 min).");

            var (code, error) = await codeTcs.Task;
            if (error != null) throw new InvalidOperationException($"Authorization denied: {error}");
            if (string.IsNullOrEmpty(code)) throw new InvalidOperationException("No authorization code returned.");

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = RedirectUri
            });
            using var resp = await Http.PostAsync("https://id.twitch.tv/oauth2/token", body, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"authorization_code exchange failed: {(int)resp.StatusCode} {json}");
            var root = JsonDocument.Parse(json).RootElement;
            return (root.GetProperty("access_token").GetString()!,
                    root.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? "" : "");
        }
        finally
        {
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
        }
    }

    private void TryOpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { log("(could not auto-open browser: " + ex.Message + ")"); }
    }
}
