using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Entities;

public sealed record TaskRunLog
{
    public TaskRunLog(TaskId taskId, DateTimeOffset runAt, TimeSpan duration, ContainerRunStatus status, string? result, string? error)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Task duration cannot be negative.");
        }

        if (status == ContainerRunStatus.Error && string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Error details are required for failed task runs.", nameof(error));
        }

        TaskId = taskId;
        RunAt = runAt;
        Duration = duration;
        Status = status;
        Result = string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
    }

    public TaskId TaskId { get; }

    public DateTimeOffset RunAt { get; }

    public TimeSpan Duration { get; }

    public ContainerRunStatus Status { get; }

    public string? Result { get; }

    public string? Error { get; }
}