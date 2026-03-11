namespace NetClaw.Domain.ValueObjects;

public readonly record struct ChannelName
{
    public ChannelName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Channel name is required.", nameof(value));
        }

        string trimmed = value.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Channel name cannot contain whitespace.", nameof(value));
        }

        Value = trimmed.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
