namespace NetClaw.Domain.ValueObjects;

public readonly record struct ContainerName
{
    public ContainerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Container name is required.", nameof(value));
        }

        string trimmed = value.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Container name cannot contain whitespace.", nameof(value));
        }

        Value = trimmed;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
