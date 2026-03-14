using NetClaw.Application.Ipc;
using NetClaw.Domain.Contracts.Ipc;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using TaskStatusEnum = NetClaw.Domain.Enums.TaskStatus;

namespace NetClaw.Application.Tests.Ipc;

public sealed class IpcCommandProcessorTests
{
    [Fact]
    public async Task ProcessAsync_AllowsMainGroupToRegisterGroups()
    {
        InMemoryGroupRepository groupRepository = new(new Dictionary<ChatJid, RegisteredGroup>());
        InMemoryTaskRepository taskRepository = new();
        IpcCommandProcessor processor = new(groupRepository, taskRepository, (_, _, _) => Task.CompletedTask);

        await processor.ProcessAsync(
            new GroupFolder("main"),
            isMainGroup: true,
            new IpcRegisterGroupCommand(new ChatJid("group@jid"), "Team", new GroupFolder("team"), "@Andy", RequiresTrigger: true, IsMain: false, ContainerConfig: null));

        Assert.Single(groupRepository.Groups);
    }

    [Fact]
    public async Task ProcessAsync_BlocksNonMainRegistration()
    {
        InMemoryGroupRepository groupRepository = new(new Dictionary<ChatJid, RegisteredGroup>());
        InMemoryTaskRepository taskRepository = new();
        IpcCommandProcessor processor = new(groupRepository, taskRepository, (_, _, _) => Task.CompletedTask);

        await processor.ProcessAsync(
            new GroupFolder("team"),
            isMainGroup: false,
            new IpcRegisterGroupCommand(new ChatJid("group@jid"), "Team", new GroupFolder("team"), "@Andy", RequiresTrigger: true, IsMain: false, ContainerConfig: null));

        Assert.Empty(groupRepository.Groups);
    }

    [Fact]
    public async Task ProcessAsync_CreatesTaskForAuthorizedGroup()
    {
        InMemoryGroupRepository groupRepository = new(new Dictionary<ChatJid, RegisteredGroup>
        {
            [new ChatJid("team@jid")] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
        });
        InMemoryTaskRepository taskRepository = new();
        IpcCommandProcessor processor = new(groupRepository, taskRepository, (_, _, _) => Task.CompletedTask);

        await processor.ProcessAsync(
            new GroupFolder("team"),
            isMainGroup: false,
            new IpcTaskCommand(null, "Prompt", ScheduleType.Interval, "1000", TaskContextMode.Group, new ChatJid("team@jid")));

        Assert.Single(taskRepository.Tasks);
        Assert.Equal(TaskStatusEnum.Active, taskRepository.Tasks[0].Status);
    }

    [Fact]
    public async Task ProcessAsync_BlocksUnauthorizedMessageForwarding()
    {
        List<string> messages = [];
        InMemoryGroupRepository groupRepository = new(new Dictionary<ChatJid, RegisteredGroup>
        {
            [new ChatJid("team@jid")] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
        });
        IpcCommandProcessor processor = new(groupRepository, new InMemoryTaskRepository(), (jid, text, _) =>
        {
            messages.Add($"{jid.Value}:{text}");
            return Task.CompletedTask;
        });

        await processor.ProcessAsync(new GroupFolder("other"), isMainGroup: false, new IpcMessageCommand(new ChatJid("team@jid"), "hello"));

        Assert.Empty(messages);
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

    private sealed class InMemoryTaskRepository : ITaskRepository
    {
        public List<ScheduledTask> Tasks { get; } = [];

        public Task AppendRunLogAsync(TaskRunLog log, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
        {
            Tasks.Add(task);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScheduledTask>>(Tasks);

        public Task<ScheduledTask?> GetByIdAsync(TaskId taskId, CancellationToken cancellationToken = default)
            => Task.FromResult<ScheduledTask?>(Tasks.FirstOrDefault(task => task.Id == taskId));

        public Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScheduledTask>>(Tasks);

        public Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TaskRunLog>> GetRunLogsAsync(TaskId taskId, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TaskRunLog>>([]);
    }
}
