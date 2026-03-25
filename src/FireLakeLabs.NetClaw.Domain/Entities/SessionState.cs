using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Entities;

public sealed record SessionState
{
    public SessionState(GroupFolder groupFolder, SessionId sessionId)
    {
        GroupFolder = groupFolder;
        SessionId = sessionId;
    }

    public GroupFolder GroupFolder { get; }

    public SessionId SessionId { get; }
}
