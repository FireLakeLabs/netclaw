namespace FireLakeLabs.NetClaw.Dashboard.Services;

public sealed class DashboardStateService
{
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;

    public TimeSpan Uptime => DateTimeOffset.UtcNow - startedAt;
}
