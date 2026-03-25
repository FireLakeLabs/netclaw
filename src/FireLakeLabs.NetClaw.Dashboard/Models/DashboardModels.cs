using FireLakeLabs.NetClaw.Domain.Enums;

namespace FireLakeLabs.NetClaw.Dashboard.Models;

public sealed record AgentActivityEventDto(
    long Id,
    string GroupFolder,
    string ChatJid,
    string? SessionId,
    string EventKind,
    string? Content,
    string? ToolName,
    string? Error,
    bool IsScheduledTask,
    string? TaskId,
    DateTimeOffset ObservedAt,
    DateTimeOffset CapturedAt);

public sealed record QueueStateDto(
    int ActiveExecutions,
    int MaxConcurrentExecutions,
    int WaitingGroupCount,
    IReadOnlyList<GroupQueueStateDto> Groups);

public sealed record GroupQueueStateDto(
    string ChatJid,
    bool Active,
    bool IsTaskExecution,
    bool PendingMessages,
    int PendingTaskCount,
    bool IdleWaiting,
    int RetryCount,
    IReadOnlyList<string> RunningTaskIds);

public sealed record ChatSummaryDto(
    string Jid,
    string Name,
    DateTimeOffset LastMessageTime,
    string? Channel,
    bool IsGroup);

public sealed record MessageDto(
    string Id,
    string ChatJid,
    string Sender,
    string SenderName,
    string Content,
    DateTimeOffset Timestamp,
    bool IsFromMe,
    bool IsBotMessage,
    IReadOnlyList<FileAttachmentDto>? Attachments = null);

public sealed record FileAttachmentDto(
    string FileId,
    string FileName,
    long FileSizeBytes,
    string? MimeType);

public sealed record TaskDto(
    string Id,
    string GroupFolder,
    string ChatJid,
    string Prompt,
    string ScheduleType,
    string ScheduleValue,
    string ContextMode,
    DateTimeOffset? NextRun,
    DateTimeOffset? LastRun,
    string? LastResult,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record TaskRunDto(
    string TaskId,
    DateTimeOffset RunAt,
    long DurationMs,
    string Status,
    string? Result,
    string? Error);

public sealed record GroupDto(
    string Jid,
    string Name,
    string Folder,
    string Trigger,
    bool RequiresTrigger,
    bool IsMain,
    DateTimeOffset AddedAt,
    string? SessionId);

public sealed record SystemHealthDto(
    DateTimeOffset ServerTime,
    long UptimeSeconds,
    IReadOnlyList<ChannelStatusDto> Channels,
    QueueStateDto QueueState);

public sealed record ChannelStatusDto(
    string Name,
    bool IsConnected);

public sealed record RouterStateDto(
    string Key,
    string Value);

public sealed record WorkerHeartbeatDto(
    DateTimeOffset ServerTime,
    QueueStateDto QueueState,
    IReadOnlyList<ChannelStatusDto> Channels);

public sealed record WorkspaceTreeEntryDto(
    string Name,
    string RelativePath,
    bool IsDirectory,
    long? SizeBytes,
    DateTimeOffset? LastModified,
    IReadOnlyList<WorkspaceTreeEntryDto>? Children);

public sealed record WorkspaceFileDto(
    string RelativePath,
    string Content,
    long SizeBytes,
    DateTimeOffset LastModified);
