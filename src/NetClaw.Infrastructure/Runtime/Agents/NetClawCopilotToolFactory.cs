using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cronos;
using Microsoft.Extensions.AI;
using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using TaskStatusEnum = NetClaw.Domain.Enums.TaskStatus;

namespace NetClaw.Infrastructure.Runtime.Agents;

public interface ICopilotToolFactory
{
    IReadOnlyList<AIFunction> CreateTools(AgentExecutionRequest request);
}

public sealed class NetClawCopilotToolFactory : ICopilotToolFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = false
    };

    private readonly IGroupRepository groupRepository;
    private readonly ISessionRepository sessionRepository;
    private readonly ITaskRepository taskRepository;
    private readonly IGroupExecutionQueue groupExecutionQueue;
    private readonly Func<ChatJid, string, CancellationToken, Task> sendMessageAsync;

    public NetClawCopilotToolFactory(
        IGroupRepository groupRepository,
        ISessionRepository sessionRepository,
        ITaskRepository taskRepository,
        IGroupExecutionQueue groupExecutionQueue,
        Func<ChatJid, string, CancellationToken, Task> sendMessageAsync)
    {
        this.groupRepository = groupRepository;
        this.sessionRepository = sessionRepository;
        this.taskRepository = taskRepository;
        this.groupExecutionQueue = groupExecutionQueue;
        this.sendMessageAsync = sendMessageAsync;
    }

    public IReadOnlyList<AIFunction> CreateTools(AgentExecutionRequest request)
    {
        ToolInvocationContext context = new(
            request,
            groupRepository,
            sessionRepository,
            taskRepository,
            groupExecutionQueue,
            sendMessageAsync);

        List<AIFunction> tools = [];
        foreach (AgentToolDefinition tool in request.Tools)
        {
            tools.Add(tool.Name switch
            {
                "send_group_message" => AIFunctionFactory.Create(
                    (Func<string, string?, CancellationToken, Task<string>>)context.SendGroupMessageAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "list_registered_groups" => AIFunctionFactory.Create(
                    (Func<CancellationToken, Task<string>>)context.ListRegisteredGroupsAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "schedule_group_task" => AIFunctionFactory.Create(
                    (Func<string, string, string, string?, string?, string?, CancellationToken, Task<string>>)context.ScheduleGroupTaskAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "list_scheduled_tasks" => AIFunctionFactory.Create(
                    (Func<string?, bool, CancellationToken, Task<string>>)context.ListScheduledTasksAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "pause_scheduled_task" => AIFunctionFactory.Create(
                    (Func<string, CancellationToken, Task<string>>)context.PauseScheduledTaskAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "resume_scheduled_task" => AIFunctionFactory.Create(
                    (Func<string, CancellationToken, Task<string>>)context.ResumeScheduledTaskAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "cancel_scheduled_task" => AIFunctionFactory.Create(
                    (Func<string, CancellationToken, Task<string>>)context.CancelScheduledTaskAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "lookup_session_state" => AIFunctionFactory.Create(
                    (Func<string?, CancellationToken, Task<string>>)context.LookupSessionStateAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "close_group_input" => AIFunctionFactory.Create(
                    (Func<string?, CancellationToken, Task<string>>)context.CloseGroupInputAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                "register_group" => AIFunctionFactory.Create(
                    (Func<string, string, string, string, bool, bool, CancellationToken, Task<string>>)context.RegisterGroupAsync,
                    tool.Name,
                    tool.Description,
                    JsonOptions),
                _ => throw new InvalidOperationException($"No Copilot tool handler is registered for '{tool.Name}'.")
            });
        }

        return tools;
    }

    private sealed class ToolInvocationContext
    {
        private readonly AgentExecutionRequest request;
        private readonly IGroupRepository groupRepository;
        private readonly ISessionRepository sessionRepository;
        private readonly ITaskRepository taskRepository;
        private readonly IGroupExecutionQueue groupExecutionQueue;
        private readonly Func<ChatJid, string, CancellationToken, Task> sendMessageAsync;

        public ToolInvocationContext(
            AgentExecutionRequest request,
            IGroupRepository groupRepository,
            ISessionRepository sessionRepository,
            ITaskRepository taskRepository,
            IGroupExecutionQueue groupExecutionQueue,
            Func<ChatJid, string, CancellationToken, Task> sendMessageAsync)
        {
            this.request = request;
            this.groupRepository = groupRepository;
            this.sessionRepository = sessionRepository;
            this.taskRepository = taskRepository;
            this.groupExecutionQueue = groupExecutionQueue;
            this.sendMessageAsync = sendMessageAsync;
        }

        public async Task<string> SendGroupMessageAsync(string text, string? targetJid = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Message text is required.", nameof(text));
            }

            ChatJid destination = await ResolveTargetChatJidAsync(targetJid, requireRegisteredGroup: !string.IsNullOrWhiteSpace(targetJid), cancellationToken);
            await sendMessageAsync(destination, text.Trim(), cancellationToken);

            return Serialize(new
            {
                sent = true,
                chatJid = destination.Value,
                text = text.Trim()
            });
        }

        public async Task<string> ListRegisteredGroupsAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<ChatJid, RegisteredGroup> groups = await groupRepository.GetAllAsync(cancellationToken);
            return Serialize(groups
                .OrderBy(pair => pair.Value.IsMain ? 0 : 1)
                .ThenBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new
                {
                    jid = pair.Key.Value,
                    name = pair.Value.Name,
                    folder = pair.Value.Folder.Value,
                    trigger = pair.Value.Trigger,
                    requiresTrigger = pair.Value.RequiresTrigger,
                    isMain = pair.Value.IsMain,
                    addedAt = pair.Value.AddedAt
                }));
        }

        public async Task<string> ScheduleGroupTaskAsync(
            string prompt,
            string scheduleType,
            string scheduleValue,
            string? targetJid = null,
            string? contextMode = null,
            string? taskId = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Task prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(scheduleType))
            {
                throw new ArgumentException("Schedule type is required.", nameof(scheduleType));
            }

            if (string.IsNullOrWhiteSpace(scheduleValue))
            {
                throw new ArgumentException("Schedule value is required.", nameof(scheduleValue));
            }

            (ChatJid chatJid, RegisteredGroup group) = await ResolveTargetGroupAsync(targetJid, cancellationToken);
            ScheduleType parsedScheduleType = ParseScheduleType(scheduleType);
            TaskContextMode parsedContextMode = ParseContextMode(contextMode);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset? nextRun = ComputeInitialNextRun(parsedScheduleType, scheduleValue, now);
            if (nextRun is null)
            {
                throw new InvalidOperationException("Unable to compute the initial next run for the scheduled task.");
            }

            ScheduledTask task = new(
                string.IsNullOrWhiteSpace(taskId) ? new TaskId($"task-{Guid.NewGuid():N}") : new TaskId(taskId),
                group.Folder,
                chatJid,
                prompt,
                parsedScheduleType,
                scheduleValue,
                parsedContextMode,
                nextRun,
                null,
                null,
                TaskStatusEnum.Active,
                now);

            await taskRepository.CreateAsync(task, cancellationToken);

            return Serialize(new
            {
                taskId = task.Id.Value,
                chatJid = task.ChatJid.Value,
                groupFolder = task.GroupFolder.Value,
                scheduleType = task.ScheduleType.ToString().ToLowerInvariant(),
                scheduleValue = task.ScheduleValue,
                contextMode = task.ContextMode.ToString().ToLowerInvariant(),
                nextRun = task.NextRun,
                status = task.Status.ToString().ToLowerInvariant()
            });
        }

        public async Task<string> ListScheduledTasksAsync(
            string? targetJid = null,
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ScheduledTask> tasks = await taskRepository.GetAllAsync(cancellationToken);
            IReadOnlyDictionary<ChatJid, RegisteredGroup> groups = await groupRepository.GetAllAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(targetJid))
            {
                (_, RegisteredGroup targetGroup) = await ResolveTargetGroupAsync(targetJid, cancellationToken);
                tasks = tasks.Where(task => task.GroupFolder == targetGroup.Folder).ToList();
            }
            else if (!request.Group.IsMain)
            {
                tasks = tasks.Where(task => task.GroupFolder == request.Group.Folder).ToList();
            }

            if (!includeInactive)
            {
                tasks = tasks.Where(task => task.Status is TaskStatusEnum.Active or TaskStatusEnum.Paused).ToList();
            }

            return Serialize(tasks
                .OrderBy(task => task.GroupFolder.Value, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(task => task.CreatedAt)
                .Select(task => new
                {
                    taskId = task.Id.Value,
                    prompt = task.Prompt,
                    chatJid = task.ChatJid.Value,
                    groupFolder = task.GroupFolder.Value,
                    groupName = groups.TryGetValue(task.ChatJid, out RegisteredGroup? matchingGroup) ? matchingGroup.Name : null,
                    scheduleType = task.ScheduleType.ToString().ToLowerInvariant(),
                    scheduleValue = task.ScheduleValue,
                    contextMode = task.ContextMode.ToString().ToLowerInvariant(),
                    nextRun = task.NextRun,
                    lastRun = task.LastRun,
                    lastResult = task.LastResult,
                    status = task.Status.ToString().ToLowerInvariant(),
                    createdAt = task.CreatedAt
                }));
        }

        public Task<string> PauseScheduledTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            return UpdateScheduledTaskStatusAsync(taskId, TaskStatusEnum.Paused, "Paused via agent tool", cancellationToken);
        }

        public Task<string> ResumeScheduledTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            return UpdateScheduledTaskStatusAsync(taskId, TaskStatusEnum.Active, "Resumed via agent tool", cancellationToken);
        }

        public Task<string> CancelScheduledTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            return UpdateScheduledTaskStatusAsync(taskId, TaskStatusEnum.Cancelled, "Cancelled via agent tool", cancellationToken);
        }

        public async Task<string> LookupSessionStateAsync(string? targetJid = null, CancellationToken cancellationToken = default)
        {
            (_, RegisteredGroup group) = await ResolveTargetGroupAsync(targetJid, cancellationToken);
            SessionId? sessionId = await sessionRepository.GetByGroupFolderAsync(group.Folder, cancellationToken);

            return Serialize(new
            {
                groupFolder = group.Folder.Value,
                chatJid = string.IsNullOrWhiteSpace(targetJid) ? request.Input.ChatJid.Value : targetJid.Trim(),
                sessionId = sessionId?.Value,
                hasSession = sessionId is not null
            });
        }

        public async Task<string> CloseGroupInputAsync(string? targetJid = null, CancellationToken cancellationToken = default)
        {
            ChatJid chatJid = await ResolveTargetChatJidAsync(targetJid, requireRegisteredGroup: true, cancellationToken);
            groupExecutionQueue.CloseInput(chatJid);

            return Serialize(new
            {
                closed = true,
                chatJid = chatJid.Value
            });
        }

        public async Task<string> RegisterGroupAsync(
            string jid,
            string name,
            string folder,
            string trigger,
            bool requiresTrigger = true,
            bool isMain = false,
            CancellationToken cancellationToken = default)
        {
            EnsureMainGroup();

            ChatJid chatJid = new(jid);
            RegisteredGroup group = new(
                name,
                new GroupFolder(folder),
                trigger,
                DateTimeOffset.UtcNow,
                containerConfig: null,
                requiresTrigger,
                isMain);

            await groupRepository.UpsertAsync(chatJid, group, cancellationToken);

            return Serialize(new
            {
                registered = true,
                jid = chatJid.Value,
                name = group.Name,
                folder = group.Folder.Value,
                trigger = group.Trigger,
                requiresTrigger = group.RequiresTrigger,
                isMain = group.IsMain
            });
        }

        private async Task<(ChatJid ChatJid, RegisteredGroup Group)> ResolveTargetGroupAsync(string? targetJid, CancellationToken cancellationToken)
        {
            ChatJid chatJid = await ResolveTargetChatJidAsync(targetJid, requireRegisteredGroup: true, cancellationToken);
            RegisteredGroup? group = await groupRepository.GetByJidAsync(chatJid, cancellationToken);
            if (group is null)
            {
                throw new InvalidOperationException($"No registered group exists for chat '{chatJid.Value}'.");
            }

            return (chatJid, group);
        }

        private async Task<string> UpdateScheduledTaskStatusAsync(
            string taskId,
            TaskStatusEnum targetStatus,
            string actionResult,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                throw new ArgumentException("Task ID is required.", nameof(taskId));
            }

            ScheduledTask? task = await taskRepository.GetByIdAsync(new TaskId(taskId), cancellationToken);
            if (task is null)
            {
                throw new InvalidOperationException($"No scheduled task exists with ID '{taskId}'.");
            }

            EnsureTaskAccessible(task);

            if (targetStatus == TaskStatusEnum.Paused && task.Status is TaskStatusEnum.Completed or TaskStatusEnum.Cancelled)
            {
                return SerializeTaskMutation(task, changed: false, reason: $"Task is already terminal with status '{task.Status.ToString().ToLowerInvariant()}'.");
            }

            if (targetStatus == TaskStatusEnum.Active && task.Status is TaskStatusEnum.Completed or TaskStatusEnum.Cancelled)
            {
                return SerializeTaskMutation(task, changed: false, reason: $"Task with status '{task.Status.ToString().ToLowerInvariant()}' cannot be resumed.");
            }

            if (targetStatus == TaskStatusEnum.Cancelled && task.Status == TaskStatusEnum.Cancelled)
            {
                return SerializeTaskMutation(task, changed: false, reason: "Task is already cancelled.");
            }

            if (task.Status == targetStatus)
            {
                return SerializeTaskMutation(task, changed: false, reason: $"Task is already '{task.Status.ToString().ToLowerInvariant()}'.");
            }

            DateTimeOffset? nextRun = targetStatus switch
            {
                TaskStatusEnum.Cancelled => null,
                TaskStatusEnum.Active when task.NextRun is null => ComputeInitialNextRun(task.ScheduleType, task.ScheduleValue, DateTimeOffset.UtcNow),
                _ => task.NextRun
            };

            ScheduledTask updatedTask = new(
                task.Id,
                task.GroupFolder,
                task.ChatJid,
                task.Prompt,
                task.ScheduleType,
                task.ScheduleValue,
                task.ContextMode,
                nextRun,
                task.LastRun,
                actionResult,
                targetStatus,
                task.CreatedAt);

            await taskRepository.UpdateAsync(updatedTask, cancellationToken);
            return SerializeTaskMutation(updatedTask, changed: true, reason: null);
        }

        private void EnsureTaskAccessible(ScheduledTask task)
        {
            if (!request.Group.IsMain && task.GroupFolder != request.Group.Folder)
            {
                throw new InvalidOperationException("Only the main group can manage scheduled tasks for a different group.");
            }
        }

        private static string SerializeTaskMutation(ScheduledTask task, bool changed, string? reason)
        {
            return Serialize(new
            {
                changed,
                reason,
                taskId = task.Id.Value,
                chatJid = task.ChatJid.Value,
                groupFolder = task.GroupFolder.Value,
                status = task.Status.ToString().ToLowerInvariant(),
                nextRun = task.NextRun,
                lastRun = task.LastRun,
                lastResult = task.LastResult
            });
        }

        private async Task<ChatJid> ResolveTargetChatJidAsync(string? targetJid, bool requireRegisteredGroup, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetJid))
            {
                return request.Input.ChatJid;
            }

            ChatJid chatJid = new(targetJid);
            if (!request.Group.IsMain && chatJid != request.Input.ChatJid)
            {
                throw new InvalidOperationException("Only the main group can target a different registered group.");
            }

            if (!requireRegisteredGroup)
            {
                return chatJid;
            }

            RegisteredGroup? group = await groupRepository.GetByJidAsync(chatJid, cancellationToken);
            if (group is null)
            {
                throw new InvalidOperationException($"No registered group exists for chat '{chatJid.Value}'.");
            }

            return chatJid;
        }

        private void EnsureMainGroup()
        {
            if (!request.Group.IsMain)
            {
                throw new InvalidOperationException("This tool is only available from the main group.");
            }
        }

        private static ScheduleType ParseScheduleType(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "cron" => ScheduleType.Cron,
                "interval" => ScheduleType.Interval,
                "once" => ScheduleType.Once,
                _ => throw new InvalidOperationException($"Unsupported schedule type '{value}'.")
            };
        }

        private static TaskContextMode ParseContextMode(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "group" => TaskContextMode.Group,
                "isolated" => TaskContextMode.Isolated,
                null or "" => TaskContextMode.Isolated,
                _ => throw new InvalidOperationException($"Unsupported task context mode '{value}'.")
            };
        }

        private static DateTimeOffset? ComputeInitialNextRun(ScheduleType scheduleType, string scheduleValue, DateTimeOffset now)
        {
            return scheduleType switch
            {
                ScheduleType.Cron => CronExpression.Parse(scheduleValue).GetNextOccurrence(now.UtcDateTime, inclusive: false) switch
                {
                    DateTime utcDateTime => new DateTimeOffset(utcDateTime, TimeSpan.Zero),
                    _ => null
                },
                ScheduleType.Interval when long.TryParse(scheduleValue, out long milliseconds) && milliseconds > 0 => now.AddMilliseconds(milliseconds),
                ScheduleType.Once when DateTimeOffset.TryParse(scheduleValue, out DateTimeOffset timestamp) => timestamp,
                _ => null
            };
        }

        private static string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
    }
}
