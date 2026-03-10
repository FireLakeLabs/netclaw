namespace NetClaw.Domain.Entities;

public sealed record ContainerConfig
{
    public ContainerConfig(IReadOnlyList<AdditionalMount>? additionalMounts = null, TimeSpan? timeout = null)
    {
        AdditionalMounts = additionalMounts ?? [];
        Timeout = timeout;

        if (timeout is { } actualTimeout && actualTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Container timeout must be positive.");
        }
    }

    public IReadOnlyList<AdditionalMount> AdditionalMounts { get; }

    public TimeSpan? Timeout { get; }
}