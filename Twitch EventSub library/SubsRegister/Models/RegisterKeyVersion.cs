namespace Twitch.EventSub.SubsRegister.Models;

/// <summary>
/// Represents a composite key for routing to RegisterItems using both the RegisterKey and Version
/// </summary>
public readonly struct RegisterKeyVersion(string key, string version) : IEquatable<RegisterKeyVersion>
{
    private string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));
    private string Version { get; } = version ?? throw new ArgumentNullException(nameof(version));

    public bool Equals(RegisterKeyVersion other) =>
        string.Equals(Key, other.Key, StringComparison.Ordinal) &&
        string.Equals(Version, other.Version, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is RegisterKeyVersion other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Key, Version);

    public static bool operator ==(RegisterKeyVersion left, RegisterKeyVersion right) =>
        left.Equals(right);

    public static bool operator !=(RegisterKeyVersion left, RegisterKeyVersion right) =>
        !left.Equals(right);

    public override string ToString() =>
        $"{Key} v{Version}";

    public static RegisterKeyVersion Create(string key, string version) =>
        new(key, version);

    public static RegisterKeyVersion FromRegisterKey(RegisterKeys registerKey, string version) =>
        new(registerKey.ToEventString(), version);
}