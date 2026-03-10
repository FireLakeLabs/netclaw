using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Entities;

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