using FireLakeLabs.NetClaw.Application.Scheduling;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using TaskStatusEnum = FireLakeLabs.NetClaw.Domain.Enums.TaskStatus;

namespace FireLakeLabs.NetClaw.Application.Tests.Scheduling;

public sealed class TaskSchedulerServiceTests
{
    [Fact]
    public void ComputeNextRun_ForIntervalTask_AvoidsDrift()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ScheduledTask task = new(
            new TaskId("task-1"),
            new GroupFolder("team"),
            new ChatJid("team@jid"),
            "Prompt",
            ScheduleType.Interval,
            "1000",
            TaskContextMode.Group,
            now.AddMilliseconds(-2500),
            null,
            null,
            TaskStatusEnum.Active,
            now.AddMinutes(-1));

        TaskSchedulerService service = CreateService(new InMemoryTaskRepository([task]));
        DateTimeOffset? nextRun = service.ComputeNextRun(task, now);

        Assert.NotNull(nextRun);
        Assert.True(nextRun > now);
    }

    [Fact]
    public async Task RunDueTasksAsync_UpdatesTasksAndLogsRuns()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ScheduledTask task = new(
            new TaskId("task-1"),
            new GroupFolder("team"),
            new ChatJid("team@jid"),
            "Prompt",
            ScheduleType.Once,
            now.ToString("O"),
            TaskContextMode.Group,
            now.AddSeconds(-1),
            null,
            null,
            TaskStatusEnum.Active,
            now.AddMinutes(-1));

        InMemoryTaskRepository taskRepository = new([task]);
        InMemoryGroupRepository groupRepository = new(new Dictionary<ChatJid, RegisteredGroup>
        {
            [new ChatJid("team@jid")] = new RegisteredGroup("Team", new GroupFolder("team"), "@assistant", now)
        });
        InMemorySessionRepository sessionRepository = new(new Dictionary<GroupFolder, SessionId>
        {
            [new GroupFolder("team")] = new SessionId("session-1")
        });
        List<string> sentMessages = [];
        TaskSchedulerService service = new(
            taskRepository,
            groupRepository,
            sessionRepository,
            (_, sessionId, _) => Task.FromResult<(string?, string?)>((sessionId?.Value, null)),
            (chatJid, message, _) =>
            {
                sentMessages.Add($"{chatJid.Value}:{message}");
                return Task.CompletedTask;
            });

        await service.RunDueTasksAsync(now);

        Assert.Single(sentMessages);
        Assert.Single(taskRepository.RunLogs);
        Assert.Equal(TaskStatusEnum.Completed, taskRepository.Tasks[0].Status);
    }

    [Fact]
    public async Task RunDueTasksAsync_PersistsRunWhenMessageDeliveryFails()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ScheduledTask task = new(
            new TaskId("task-1"),
            new GroupFolder("team"),
            new ChatJid("team@jid"),
            "Prompt",
            ScheduleType.Once,
            now.ToString("O"),
            TaskContextMode.Group,
            now.AddSeconds(-1),
            null,
            null,
            TaskStatusEnum.Active,
            now.AddMinutes(-1));

        InMemoryTaskRepository taskRepository = new([task]);
        InMemoryGroupRepository groupRepository = new(new Dictionary<ChatJid, RegisteredGroup>
        {
            [new ChatJid("team@jid")] = new RegisteredGroup("Team", new GroupFolder("team"), "@assistant", now)
        });
        InMemorySessionRepository sessionRepository = new(new Dictionary<GroupFolder, SessionId>());
        TaskSchedulerService service = new(
            taskRepository,
            groupRepository,
            sessionRepository,
            (_, _, _) => Task.FromResult<(string?, string?)>(("assistant reply", null)),
            (_, _, _) => throw new InvalidOperationException("No connected channel owns chat JID 'team@jid'."));

        await service.RunDueTasksAsync(now);

        Assert.Single(taskRepository.RunLogs);
        Assert.Equal(ContainerRunStatus.Error, taskRepository.RunLogs[0].Status);
        Assert.NotNull(taskRepository.RunLogs[0].Error);
        Assert.Contains("No connected channel owns chat JID", taskRepository.RunLogs[0].Error!);
        Assert.Equal(TaskStatusEnum.Completed, taskRepository.Tasks[0].Status);
    }

    private static TaskSchedulerService CreateService(InMemoryTaskRepository taskRepository)
    {
        return new TaskSchedulerService(
            taskRepository,
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>()),
            new InMemorySessionRepository(new Dictionary<GroupFolder, SessionId>()),
            (_, _, _) => Task.FromResult<(string?, string?)>((null, null)),
            (_, _, _) => Task.CompletedTask);
    }

    private sealed class InMemoryTaskRepository : ITaskRepository
    {
        public InMemoryTaskRepository(IReadOnlyList<ScheduledTask> tasks)
        {
            Tasks = tasks.ToList();
        }

        public List<ScheduledTask> Tasks { get; }

        public List<TaskRunLog> RunLogs { get; } = [];

        public Task AppendRunLogAsync(TaskRunLog log, CancellationToken cancellationToken = default)
        {
            RunLogs.Add(log);
            return Task.CompletedTask;
        }

        public Task CreateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
        {
            Tasks.Add(task);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScheduledTask>>(Tasks);

        public Task<ScheduledTask?> GetByIdAsync(TaskId taskId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ScheduledTask?>(Tasks.FirstOrDefault(task => task.Id == taskId));
        }

        public Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ScheduledTask> dueTasks = Tasks.Where(task => task.NextRun is not null && task.NextRun <= now && task.Status == TaskStatusEnum.Active).ToList();
            return Task.FromResult(dueTasks);
        }

        public Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
        {
            int index = Tasks.FindIndex(current => current.Id == task.Id);
            Tasks[index] = task;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TaskRunLog>> GetRunLogsAsync(TaskId taskId, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TaskRunLog>>(RunLogs.Where(l => l.TaskId == taskId).Take(limit).ToList());
    }

    private sealed class InMemoryGroupRepository : IGroupRepository
    {
        public InMemoryGroupRepository(IReadOnlyDictionary<ChatJid, RegisteredGroup> groups)
        {
            Groups = new Dictionary<ChatJid, RegisteredGroup>(groups);
        }

        public Dictionary<ChatJid, RegisteredGroup> Groups { get; }

        public Task<IReadOnlyDictionary<ChatJid, RegisteredGroup>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<ChatJid, RegisteredGroup>>(Groups);

        public Task<RegisteredGroup?> GetByJidAsync(ChatJid chatJid, CancellationToken cancellationToken = default)
            => Task.FromResult(Groups.TryGetValue(chatJid, out RegisteredGroup? group) ? group : null);

        public Task UpsertAsync(ChatJid chatJid, RegisteredGroup group, CancellationToken cancellationToken = default)
        {
            Groups[chatJid] = group;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySessionRepository : ISessionRepository
    {
        public InMemorySessionRepository(IReadOnlyDictionary<GroupFolder, SessionId> sessions)
        {
            Sessions = new Dictionary<GroupFolder, SessionId>(sessions);
        }

        public Dictionary<GroupFolder, SessionId> Sessions { get; }

        public Task<IReadOnlyDictionary<GroupFolder, SessionId>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<GroupFolder, SessionId>>(Sessions);

        public Task<SessionId?> GetByGroupFolderAsync(GroupFolder groupFolder, CancellationToken cancellationToken = default)
            => Task.FromResult<SessionId?>(Sessions.TryGetValue(groupFolder, out SessionId sessionId) ? sessionId : null);

        public Task UpsertAsync(SessionState sessionState, CancellationToken cancellationToken = default)
        {
            Sessions[sessionState.GroupFolder] = sessionState.SessionId;
            return Task.CompletedTask;
        }
    }
}
