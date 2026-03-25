namespace FireLakeLabs.NetClaw.Domain.Entities;

public sealed record RouterStateEntry
{
    public RouterStateEntry(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Router state key is required.", nameof(key));
        }

        Value = value ?? throw new ArgumentNullException(nameof(value));
        Key = key.Trim();
    }

    public string Key { get; }

    public string Value { get; }
}
