using Cronos;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using TaskStatusEnum = NetClaw.Domain.Enums.TaskStatus;

namespace NetClaw.Application.Scheduling;

public sealed class TaskSchedulerService : ITaskSchedulerService
{
    private readonly Func<ScheduledTask, SessionId?, CancellationToken, Task<(string? Result, string? Error)>> executeTaskAsync;
    private readonly IGroupRepository groupRepository;
    private readonly ISessionRepository sessionRepository;
    private readonly ITaskRepository taskRepository;
    private readonly Func<ChatJid, string, CancellationToken, Task> sendMessageAsync;
    private readonly TimeZoneInfo timeZone;

    public TaskSchedulerService(
        ITaskRepository taskRepository,
        IGroupRepository groupRepository,
        ISessionRepository sessionRepository,
        Func<ScheduledTask, SessionId?, CancellationToken, Task<(string? Result, string? Error)>> executeTaskAsync,
        Func<ChatJid, string, CancellationToken, Task> sendMessageAsync,
        TimeZoneInfo? timeZone = null)
    {
        this.taskRepository = taskRepository;
        this.groupRepository = groupRepository;
        this.sessionRepository = sessionRepository;
        this.executeTaskAsync = executeTaskAsync;
        this.sendMessageAsync = sendMessageAsync;
        this.timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    public DateTimeOffset? ComputeNextRun(ScheduledTask task, DateTimeOffset now)
    {
        if (task.ScheduleType == ScheduleType.Once)
        {
            return null;
        }

        if (task.ScheduleType == ScheduleType.Cron)
        {
            CronExpression expression = CronExpression.Parse(task.ScheduleValue);
            return expression.GetNextOccurrence(now.UtcDateTime, timeZone, inclusive: false) switch
            {
                DateTime utcDateTime => new DateTimeOffset(utcDateTime, TimeSpan.Zero),
                _ => null
            };
        }

        if (!long.TryParse(task.ScheduleValue, out long intervalMilliseconds) || intervalMilliseconds <= 0)
        {
            return now.AddMinutes(1);
        }

        DateTimeOffset anchor = task.NextRun ?? now;
        DateTimeOffset nextRun = anchor.AddMilliseconds(intervalMilliseconds);
        while (nextRun <= now)
        {
            nextRun = nextRun.AddMilliseconds(intervalMilliseconds);
        }

        return nextRun;
    }

    public async Task RunDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScheduledTask> dueTasks = await taskRepository.GetDueTasksAsync(now, cancellationToken);
        IReadOnlyDictionary<ChatJid, RegisteredGroup> groups = await groupRepository.GetAllAsync(cancellationToken);

        foreach (ScheduledTask task in dueTasks)
        {
            KeyValuePair<ChatJid, RegisteredGroup>? matchingGroup = groups.FirstOrDefault(pair => pair.Value.Folder == task.GroupFolder);
            if (matchingGroup is null || matchingGroup.Value.Value is null)
            {
                ScheduledTask pausedTask = new(
                    task.Id,
                    task.GroupFolder,
                    task.ChatJid,
                    task.Prompt,
                    task.ScheduleType,
                    task.ScheduleValue,
                    task.ContextMode,
                    task.NextRun,
                    task.LastRun,
                    "Error: Group not found",
                    TaskStatusEnum.Paused,
                    task.CreatedAt);
                await taskRepository.UpdateAsync(pausedTask, cancellationToken);
                await taskRepository.AppendRunLogAsync(new TaskRunLog(task.Id, now, TimeSpan.Zero, ContainerRunStatus.Error, null, "Group not found"), cancellationToken);
                continue;
            }

            SessionId? sessionId = task.ContextMode == TaskContextMode.Group
                ? await sessionRepository.GetByGroupFolderAsync(task.GroupFolder, cancellationToken)
                : null;

            DateTimeOffset startedAt = now;
            (string? Result, string? Error) executionResult = await executeTaskAsync(task, sessionId, cancellationToken);
            TimeSpan duration = DateTimeOffset.UtcNow - startedAt;
            string? deliveryError = null;

            if (!string.IsNullOrWhiteSpace(executionResult.Result))
            {
                try
                {
                    await sendMessageAsync(task.ChatJid, executionResult.Result, cancellationToken);
                }
                catch (Exception exception)
                {
                    deliveryError = exception.Message;
                }
            }

            DateTimeOffset? nextRun = ComputeNextRun(task, now);
            TaskStatusEnum status = task.ScheduleType == ScheduleType.Once ? TaskStatusEnum.Completed : TaskStatusEnum.Active;
            string? combinedError = CombineErrors(executionResult.Error, deliveryError);
            ScheduledTask updatedTask = new(
                task.Id,
                task.GroupFolder,
                task.ChatJid,
                task.Prompt,
                task.ScheduleType,
                task.ScheduleValue,
                task.ContextMode,
                nextRun,
                now,
                combinedError is null ? executionResult.Result ?? "Completed" : $"Error: {combinedError}",
                status,
                task.CreatedAt);

            await taskRepository.UpdateAsync(updatedTask, cancellationToken);
            await taskRepository.AppendRunLogAsync(
                new TaskRunLog(task.Id, now, duration, combinedError is null ? ContainerRunStatus.Success : ContainerRunStatus.Error, executionResult.Result, combinedError),
                cancellationToken);
        }
    }

    private static string? CombineErrors(string? executionError, string? deliveryError)
    {
        if (string.IsNullOrWhiteSpace(executionError))
        {
            return string.IsNullOrWhiteSpace(deliveryError) ? null : deliveryError;
        }

        if (string.IsNullOrWhiteSpace(deliveryError))
        {
            return executionError;
        }

        return $"{executionError}; message delivery failed: {deliveryError}";
    }
}
