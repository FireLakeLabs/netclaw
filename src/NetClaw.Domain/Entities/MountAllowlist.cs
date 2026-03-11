namespace NetClaw.Domain.Entities;

public sealed record MountAllowlist
{
    public MountAllowlist(IReadOnlyList<AllowedRoot> allowedRoots, IReadOnlyList<string> blockedPatterns, bool nonMainReadOnly)
    {
        AllowedRoots = allowedRoots ?? throw new ArgumentNullException(nameof(allowedRoots));
        BlockedPatterns = blockedPatterns ?? throw new ArgumentNullException(nameof(blockedPatterns));
        NonMainReadOnly = nonMainReadOnly;
    }

    public IReadOnlyList<AllowedRoot> AllowedRoots { get; }

    public IReadOnlyList<string> BlockedPatterns { get; }

    public bool NonMainReadOnly { get; }
}
