using System.Text.Json;

namespace TwitchEventSub.LiveHarness;

/// <summary>
/// Read/write access to the project's user-secrets <c>secrets.json</c>.
/// The runtime configuration provider is read-only, so this manages the flat key/value file
/// directly (keys use the same "Section:Key" form as <c>dotnet user-secrets set</c>).
/// Existing keys (e.g. the manually-set client secret) are preserved on save.
/// </summary>
public sealed class TokenStore
{
    private readonly string _path;
    private readonly Dictionary<string, string> _values;

    public TokenStore(string userSecretsId)
    {
        // Matches the SDK layout: %APPDATA%\Microsoft\UserSecrets\<id>\secrets.json on Windows,
        // ~/.microsoft/usersecrets/<id>/secrets.json elsewhere.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = !string.IsNullOrEmpty(appData)
            ? Path.Combine(appData, "Microsoft", "UserSecrets")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microsoft", "usersecrets");
        _path = Path.Combine(root, userSecretsId, "secrets.json");
        _values = Load(_path);
    }

    private static Dictionary<string, string> Load(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return dict;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String)
                    dict[p.Name] = p.Value.GetString()!;
        }
        catch { /* corrupt/empty file → start fresh, never throw */ }
        return dict;
    }

    public string? Get(string key) =>
        _values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    public DateTimeOffset? GetDate(string key) =>
        Get(key) is { } s && DateTimeOffset.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
            ? d : null;

    public void Set(string key, string value) => _values[key] = value;

    public void SetDate(string key, DateTimeOffset value) => _values[key] = value.ToString("o");

    public void Remove(string key) => _values.Remove(key);

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
    }
}
