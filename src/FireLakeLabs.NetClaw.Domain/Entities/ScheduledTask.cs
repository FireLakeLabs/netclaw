using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using TaskStatusEnum = FireLakeLabs.NetClaw.Domain.Enums.TaskStatus;

namespace FireLakeLabs.NetClaw.Domain.Entities;

public sealed record ScheduledTask
{
    public ScheduledTask(
        TaskId id,
        GroupFolder groupFolder,
        ChatJid chatJid,
        string prompt,
        ScheduleType scheduleType,
        string scheduleValue,
        TaskContextMode contextMode,
        DateTimeOffset? nextRun,
        DateTimeOffset? lastRun,
        string? lastResult,
        TaskStatusEnum status,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Task prompt is required.", nameof(prompt));
        }

        if (string.IsNullOrWhiteSpace(scheduleValue))
        {
            throw new ArgumentException("Schedule value is required.", nameof(scheduleValue));
        }

        if (nextRun is not null && nextRun < createdAt.AddYears(-1))
        {
            throw new ArgumentOutOfRangeException(nameof(nextRun), "Next run is outside the supported range.");
        }

        Id = id;
        GroupFolder = groupFolder;
        ChatJid = chatJid;
        Prompt = prompt.Trim();
        ScheduleType = scheduleType;
        ScheduleValue = scheduleValue.Trim();
        ContextMode = contextMode;
        NextRun = nextRun;
        LastRun = lastRun;
        LastResult = string.IsNullOrWhiteSpace(lastResult) ? null : lastResult.Trim();
        Status = status;
        CreatedAt = createdAt;
    }

    public TaskId Id { get; }

    public GroupFolder GroupFolder { get; }

    public ChatJid ChatJid { get; }

    public string Prompt { get; }

    public ScheduleType ScheduleType { get; }

    public string ScheduleValue { get; }

    public TaskContextMode ContextMode { get; }

    public DateTimeOffset? NextRun { get; }

    public DateTimeOffset? LastRun { get; }

    public string? LastResult { get; }

    public TaskStatusEnum Status { get; }

    public DateTimeOffset CreatedAt { get; }
}
