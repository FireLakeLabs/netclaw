using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NetClaw.Application.Execution;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Ipc;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Persistence.Sqlite;
using NetClaw.Setup;

namespace NetClaw.IntegrationTests;

public sealed class EndToEndIntegrationTests
{
    [Fact]
    public async Task SetupRegistration_IsVisibleThroughHostRepositories()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);
            SetupResult registration = await runner.RunAsync(SetupCommand.Parse([
                "--step", "register",
                "--jid", "team@jid",
                "--name", "Team",
                "--trigger", "@Andy",
                "--folder", "team"
            ]));

            Assert.Equal(0, registration.ExitCode);

            using IHost host = CreateHost(projectRoot);
            await host.StartAsync();

            IGroupRepository repository = host.Services.GetRequiredService<IGroupRepository>();
            RegisteredGroup? group = await repository.GetByJidAsync(new ChatJid("team@jid"));

            Assert.NotNull(group);
            Assert.Equal("Team", group.Name);
            Assert.Equal("team", group.Folder.Value);

            await host.StopAsync();
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task IpcSchedulingAndHostScheduler_PersistTaskLifecycle()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            using IHost host = CreateHost(projectRoot);
            await host.StartAsync();

            IGroupRepository groupRepository = host.Services.GetRequiredService<IGroupRepository>();
            await groupRepository.UpsertAsync(
                new ChatJid("team@jid"),
                new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow));

            IIpcCommandProcessor processor = host.Services.GetRequiredService<IIpcCommandProcessor>();
            await processor.ProcessAsync(
                new GroupFolder("team"),
                isMainGroup: false,
                new IpcTaskCommand(
                    TaskId: null,
                    Prompt: "Ping",
                    ScheduleType: ScheduleType.Once,
                    ScheduleValue: DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                    ContextMode: TaskContextMode.Group,
                    TargetJid: new ChatJid("team@jid")));

            ITaskRepository taskRepository = host.Services.GetRequiredService<ITaskRepository>();
            IReadOnlyList<ScheduledTask> createdTasks = await taskRepository.GetAllAsync();
            Assert.Single(createdTasks);

            ITaskSchedulerService schedulerService = host.Services.GetRequiredService<ITaskSchedulerService>();
            await schedulerService.RunDueTasksAsync(DateTimeOffset.UtcNow);

            ScheduledTask? updatedTask = await taskRepository.GetByIdAsync(createdTasks[0].Id);
            Assert.NotNull(updatedTask);
            Assert.Equal(NetClaw.Domain.Enums.TaskStatus.Completed, updatedTask.Status);

            SqliteConnectionFactory connectionFactory = host.Services.GetRequiredService<SqliteConnectionFactory>();
            await using SqliteConnection connection = connectionFactory.OpenConnection();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM task_run_logs;";
            long logCount = (long)(await command.ExecuteScalarAsync())!;

            Assert.Equal(1L, logCount);

            await host.StopAsync();
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task IpcWatcher_PollsTaskFilesAndPersistsScheduledTask()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            using IHost host = CreateHost(projectRoot);
            await host.StartAsync();

            IGroupRepository groupRepository = host.Services.GetRequiredService<IGroupRepository>();
            await groupRepository.UpsertAsync(
                new ChatJid("team@jid"),
                new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow));

            string taskDirectory = Path.Combine(projectRoot, "data", "ipc", "team", "tasks");
            Directory.CreateDirectory(taskDirectory);

            string taskFilePath = Path.Combine(taskDirectory, "task.json");
            await File.WriteAllTextAsync(
                taskFilePath,
                """
                {
                  "type": "schedule_task",
                  "prompt": "Ping from IPC",
                  "schedule_type": "once",
                  "schedule_value": "2026-03-10T00:00:00Z",
                  "context_mode": "group",
                  "targetJid": "team@jid"
                }
                """);

            IIpcCommandWatcher watcher = host.Services.GetRequiredService<IIpcCommandWatcher>();
            await watcher.PollOnceAsync();

            ITaskRepository taskRepository = host.Services.GetRequiredService<ITaskRepository>();
            IReadOnlyList<ScheduledTask> tasks = await taskRepository.GetAllAsync();

            Assert.Single(tasks);
            Assert.Equal("Ping from IPC", tasks[0].Prompt);
            Assert.Equal(TaskContextMode.Group, tasks[0].ContextMode);
            Assert.False(File.Exists(taskFilePath));

            await host.StopAsync();
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task SetupVerify_ReportsSuccessAfterBootstrapArtifactsAreCreated()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);
            await File.WriteAllTextAsync(Path.Combine(projectRoot, ".env"), "ANTHROPIC_API_KEY=test-key\n");

            await runner.RunAsync(SetupCommand.Parse([
                "--step", "register",
                "--jid", "team@jid",
                "--name", "Team",
                "--trigger", "@Andy",
                "--folder", "team"
            ]));
            await runner.RunAsync(SetupCommand.Parse(["--step", "mounts", "--empty"]));
            await runner.RunAsync(SetupCommand.Parse(["--step", "service", "--service-mode", "script"]));

            SetupResult verification = await runner.RunAsync(SetupCommand.Parse(["--step", "verify"]));

            Assert.Equal(0, verification.ExitCode);
            Assert.Equal("success", verification.Status["OVERALL_STATUS"]);
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task MessageLoop_EnqueuesStoredMessagesAndRoutesRuntimeReply()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();
        FakeAgentRuntime fakeRuntime = new();
        FakeChannel fakeChannel = new(new ChatJid("team@jid"));

        try
        {
            using IHost host = CreateHost(projectRoot, services =>
            {
                services.AddSingleton<IAgentRuntime>(fakeRuntime);
                services.AddSingleton<IReadOnlyList<IChannel>>([fakeChannel]);
            });
            await host.StartAsync();

            IGroupRepository groupRepository = host.Services.GetRequiredService<IGroupRepository>();
            await groupRepository.UpsertAsync(
                new ChatJid("team@jid"),
                new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow));

            IMessageRepository messageRepository = host.Services.GetRequiredService<IMessageRepository>();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await messageRepository.StoreMessageAsync(new StoredMessage(
                "message-1",
                new ChatJid("team@jid"),
                "sender-1",
                "User",
                "@Andy please respond",
                now,
                isFromMe: false,
                isBotMessage: false));

            InboundMessagePollingService pollingService = host.Services.GetRequiredService<InboundMessagePollingService>();
            await pollingService.PollOnceAsync();

            await fakeChannel.SendCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Single(fakeChannel.Messages);
            Assert.Equal("assistant reply", fakeChannel.Messages[0]);
            Assert.Contains("@Andy please respond", fakeRuntime.LastPrompt);

            IRouterStateRepository routerStateRepository = host.Services.GetRequiredService<IRouterStateRepository>();
            RouterStateEntry? state = await routerStateRepository.GetAsync("last_agent_timestamp:team@jid");
            Assert.NotNull(state);

            await host.StopAsync();
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    private static IHost CreateHost(string projectRoot, Action<IServiceCollection>? configureServices = null)
    {
        IHostBuilder builder = NetClaw.Host.Program.CreateHostBuilder([])
            .ConfigureAppConfiguration(configurationBuilder =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["NetClaw:ProjectRoot"] = projectRoot,
                    ["NetClaw:MessageLoop:PollInterval"] = "00:10:00",
                    ["NetClaw:MessageLoop:Timezone"] = "UTC",
                    ["NetClaw:Scheduler:PollInterval"] = "00:10:00"
                });
            });

        if (configureServices is not null)
        {
            builder = builder.ConfigureServices((_, services) => configureServices(services));
        }

        return builder.Build();
    }

    private static SetupRunner CreateRunner(string projectRoot, string homeDirectory)
    {
        return new SetupRunner(
            SetupPaths.Create(projectRoot, homeDirectory),
            new NetClaw.Infrastructure.FileSystem.PhysicalFileSystem(),
            new NetClaw.Infrastructure.Runtime.ProcessCommandRunner(),
            new NetClaw.Infrastructure.Runtime.PlatformDetectionService());
    }

    private static string CreateTemporaryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"netclaw-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class FakeAgentRuntime : IAgentRuntime
    {
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<ContainerExecutionResult> ExecuteAsync(ContainerInput input, Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            LastPrompt = input.Prompt;
            Completion.TrySetResult(true);

            return Task.FromResult(new ContainerExecutionResult(
                ContainerRunStatus.Success,
                "assistant reply",
                new SessionId("session-1"),
                null,
                new ContainerName("agent-fake-team")));
        }
    }

    private sealed class FakeChannel : IChannel
    {
        private readonly ChatJid ownedJid;

        public FakeChannel(ChatJid ownedJid)
        {
            this.ownedJid = ownedJid;
        }

        public List<string> Messages { get; } = [];

    public TaskCompletionSource<bool> SendCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ChannelName Name => new("fake");

        public bool IsConnected => true;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool Owns(ChatJid chatJid) => chatJid == ownedJid;

        public Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default)
        {
            Messages.Add(text);
            SendCompletion.TrySetResult(true);
            return Task.CompletedTask;
        }

        public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}