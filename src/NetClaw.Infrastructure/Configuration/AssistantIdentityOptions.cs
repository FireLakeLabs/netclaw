namespace NetClaw.Infrastructure.Configuration;

public sealed record AssistantIdentityOptions
{
    public string Name { get; init; } = "Andy";

    public bool HasOwnNumber { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Assistant name is required.");
        }
    }
}
