namespace FireLakeLabs.NetClaw.Domain.ValueObjects;

public readonly record struct ChatJid
{
    public ChatJid(string value)
    {
        Value = RequireNonEmpty(value, nameof(value), "Chat JID is required.");
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string RequireNonEmpty(string value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, paramName);
        }

        return value.Trim();
    }
}
