namespace NetClaw.Infrastructure.Configuration;

public sealed record MessageLoopOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public string Timezone { get; init; } = "UTC";

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Message loop poll interval must be positive.");
        }

        if (string.IsNullOrWhiteSpace(Timezone))
        {
            throw new InvalidOperationException("Message loop timezone is required.");
        }
    }
}