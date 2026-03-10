using NetClaw.Domain.Contracts.Ipc;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using TaskStatusEnum = NetClaw.Domain.Enums.TaskStatus;

namespace NetClaw.Application.Ipc;

public sealed class IpcCommandProcessor : IIpcCommandProcessor
{
    private readonly IGroupRepository groupRepository;
    private readonly ITaskRepository taskRepository;
    private readonly Func<ChatJid, string, CancellationToken, Task> sendMessageAsync;

    public IpcCommandProcessor(
        IGroupRepository groupRepository,
        ITaskRepository taskRepository,
        Func<ChatJid, string, CancellationToken, Task> sendMessageAsync)
    {
        this.groupRepository = groupRepository;
        this.taskRepository = taskRepository;
        this.sendMessageAsync = sendMessageAsync;
    }

    public async Task ProcessAsync(GroupFolder sourceGroup, bool isMainGroup, IpcCommand command, CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case IpcMessageCommand messageCommand:
                await ProcessMessageAsync(sourceGroup, isMainGroup, messageCommand, cancellationToken);
                break;
            case IpcTaskCommand taskCommand:
                await ProcessTaskAsync(sourceGroup, isMainGroup, taskCommand, cancellationToken);
                break;
            case IpcRegisterGroupCommand registerGroupCommand:
                await ProcessRegisterGroupAsync(isMainGroup, registerGroupCommand, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported IPC command type '{command.Type}'.");
        }
    }

    private async Task ProcessMessageAsync(GroupFolder sourceGroup, bool isMainGroup, IpcMessageCommand command, CancellationToken cancellationToken)
    {
        RegisteredGroup? targetGroup = await groupRepository.GetByJidAsync(command.ChatJid, cancellationToken);
        if (targetGroup is null)
        {
            return;
        }

        if (!isMainGroup && targetGroup.Folder != sourceGroup)
        {
            return;
        }

        await sendMessageAsync(command.ChatJid, command.Text, cancellationToken);
    }

    private async Task ProcessTaskAsync(GroupFolder sourceGroup, bool isMainGroup, IpcTaskCommand command, CancellationToken cancellationToken)
    {
        RegisteredGroup? targetGroup = await groupRepository.GetByJidAsync(command.TargetJid, cancellationToken);
        if (targetGroup is null)
        {
            return;
        }

        if (!isMainGroup && targetGroup.Folder != sourceGroup)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset? nextRun = command.ScheduleType switch
        {
            ScheduleType.Cron => Cronos.CronExpression.Parse(command.ScheduleValue).GetNextOccurrence(now.UtcDateTime, inclusive: false) switch
            {
                DateTime utcDateTime => new DateTimeOffset(utcDateTime, TimeSpan.Zero),
                _ => null
            },
            ScheduleType.Interval when long.TryParse(command.ScheduleValue, out long milliseconds) && milliseconds > 0 => now.AddMilliseconds(milliseconds),
            ScheduleType.Once when DateTimeOffset.TryParse(command.ScheduleValue, out DateTimeOffset timestamp) => timestamp,
            _ => null
        };

        if (nextRun is null)
        {
            return;
        }

        ScheduledTask task = new(
            command.TaskId ?? new TaskId($"task-{Guid.NewGuid():N}"),
            targetGroup.Folder,
            command.TargetJid,
            command.Prompt,
            command.ScheduleType,
            command.ScheduleValue,
            command.ContextMode,
            nextRun,
            null,
            null,
            TaskStatusEnum.Active,
            now);

        await taskRepository.CreateAsync(task, cancellationToken);
    }

    private async Task ProcessRegisterGroupAsync(bool isMainGroup, IpcRegisterGroupCommand command, CancellationToken cancellationToken)
    {
        if (!isMainGroup)
        {
            return;
        }

        RegisteredGroup group = new(
            command.Name,
            command.Folder,
            command.Trigger,
            DateTimeOffset.UtcNow,
            command.ContainerConfig,
            command.RequiresTrigger,
            command.IsMain);

        await groupRepository.UpsertAsync(command.Jid, group, cancellationToken);
    }
}