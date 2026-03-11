namespace NetClaw.Domain.Enums;

public enum ContainerEventKind
{
    SessionStarted = 0,
    TextDelta = 1,
    MessageCompleted = 2,
    ToolStarted = 3,
    ToolCompleted = 4,
    ReasoningDelta = 5,
    Idle = 6,
    Error = 7
}
