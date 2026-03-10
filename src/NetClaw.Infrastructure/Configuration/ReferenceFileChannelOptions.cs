namespace NetClaw.Infrastructure.Configuration;

public sealed record ReferenceFileChannelOptions
{
    public bool Enabled { get; init; }

    public required string RootDirectory { get; init; }

    public bool ClaimAllChats { get; init; } = true;

    public void Validate()
    {
        if (Enabled && string.IsNullOrWhiteSpace(RootDirectory))
        {
            throw new InvalidOperationException("Reference file channel root directory is required when the channel is enabled.");
        }
    }
}