using System.Net.WebSockets;
using System.Text;

namespace TwitchEventSub.LiveHarness;

/// <summary>
/// Self-diagnostic: connects a raw <see cref="ClientWebSocket"/> to the Twitch EventSub endpoint and
/// dumps every frame for a few seconds. Twitch sends <c>session_welcome</c> immediately on connect
/// with no authentication, so this isolates "does the welcome arrive on the wire?" from any library
/// state-machine wiring. Run with: <c>dotnet run --project TwitchEventSub.LiveHarness -- diagnose</c>.
/// </summary>
public static class Diagnostics
{
    public static async Task<int> RunAsync()
    {
        var url = "wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds=10";
        void Log(string m) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {m}");

        Log($"DIAGNOSE: connecting raw ClientWebSocket to {url}");
        using var ws = new ClientWebSocket();
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await ws.ConnectAsync(new Uri(url), connectCts.Token);
            Log($"DIAGNOSE: connected. State={ws.State}");
        }
        catch (Exception ex)
        {
            Log("DIAGNOSE: connect FAILED: " + ex.Message);
            return 1;
        }

        var buffer = new byte[16 * 1024];
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var frames = 0;
        try
        {
            while (ws.State == WebSocketState.Open && !readCts.IsCancellationRequested)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), readCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log($"DIAGNOSE: server CLOSE status={result.CloseStatus} desc={result.CloseStatusDescription}");
                        return 0;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                frames++;
                Log($"DIAGNOSE: frame #{frames}: {sb}");
            }
        }
        catch (OperationCanceledException)
        {
            Log($"DIAGNOSE: read window elapsed. Total frames received: {frames}");
        }
        catch (Exception ex)
        {
            Log("DIAGNOSE: read error: " + ex.Message);
        }

        Log($"DIAGNOSE: done. State={ws.State}, frames={frames}");
        return 0;
    }
}
