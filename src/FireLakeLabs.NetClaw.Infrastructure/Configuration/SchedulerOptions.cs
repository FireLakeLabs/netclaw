namespace FireLakeLabs.NetClaw.Infrastructure.Configuration;

public sealed record SchedulerOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(1);

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Scheduler poll interval must be positive.");
        }
    }
}
