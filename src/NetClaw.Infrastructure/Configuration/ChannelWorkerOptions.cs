namespace NetClaw.Infrastructure.Configuration;

public sealed record ChannelWorkerOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public bool InitialSyncOnStart { get; init; } = true;

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Channel poll interval must be positive.");
        }
    }
}