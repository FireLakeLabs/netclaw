using FireLakeLabs.NetClaw.Domain.Enums;

namespace FireLakeLabs.NetClaw.Domain.Entities;

public sealed record AgentActivityEvent
{
    public AgentActivityEvent(
        long id,
        string groupFolder,
        string chatJid,
        string? sessionId,
        ContainerEventKind eventKind,
        string? content,
        string? toolName,
        string? error,
        bool isScheduledTask,
        string? taskId,
        DateTimeOffset observedAt,
        DateTimeOffset capturedAt)
    {
        if (string.IsNullOrWhiteSpace(groupFolder))
        {
            throw new ArgumentException("Group folder is required.", nameof(groupFolder));
        }

        if (string.IsNullOrWhiteSpace(chatJid))
        {
            throw new ArgumentException("Chat JID is required.", nameof(chatJid));
        }

        Id = id;
        GroupFolder = groupFolder.Trim();
        ChatJid = chatJid.Trim();
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        EventKind = eventKind;
        Content = content;
        ToolName = string.IsNullOrWhiteSpace(toolName) ? null : toolName.Trim();
        Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        IsScheduledTask = isScheduledTask;
        TaskId = string.IsNullOrWhiteSpace(taskId) ? null : taskId.Trim();
        ObservedAt = observedAt;
        CapturedAt = capturedAt;
    }

    public long Id { get; }

    public string GroupFolder { get; }

    public string ChatJid { get; }

    public string? SessionId { get; }

    public ContainerEventKind EventKind { get; }

    public string? Content { get; }

    public string? ToolName { get; }

    public string? Error { get; }

    public bool IsScheduledTask { get; }

    public string? TaskId { get; }

    public DateTimeOffset ObservedAt { get; }

    public DateTimeOffset CapturedAt { get; }
}
