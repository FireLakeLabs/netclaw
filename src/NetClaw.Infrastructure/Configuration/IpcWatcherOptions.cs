namespace NetClaw.Infrastructure.Configuration;

public sealed record IpcWatcherOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("IPC poll interval must be positive.");
        }
    }
}