namespace FireLakeLabs.NetClaw.Infrastructure.Configuration;

public sealed record AssistantIdentityOptions
{
    public string? Name { get; init; }

    public string DefaultTrigger { get; init; } = "assistant";

    public bool HasOwnNumber { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DefaultTrigger))
        {
            throw new InvalidOperationException("Assistant default trigger is required.");
        }
    }
}
