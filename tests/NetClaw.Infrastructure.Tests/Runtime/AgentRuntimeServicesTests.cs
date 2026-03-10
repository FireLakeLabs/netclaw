using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;
using NetClaw.Infrastructure.Paths;
using NetClaw.Infrastructure.Runtime.Agents;

namespace NetClaw.Infrastructure.Tests.Runtime;

public sealed class AgentRuntimeServicesTests
{
    [Fact]
    public async Task CopilotEngine_CreatesSessionAndReturnsResponse()
    {
        FakeCopilotClientAdapter client = new(new FakeCopilotSessionAdapter("copilot-session-1", "/runtime/workspace", "done"));
        CopilotCodingAgentEngine engine = new(
            new FakeCopilotClientPool(client),
            new AgentRuntimeOptions
            {
                CopilotClientName = "NetClaw",
                CopilotConfigDirectory = "/runtime/config",
                CopilotModel = "gpt-5.4",
                CopilotReasoningEffort = "high"
            });

        AgentExecutionRequest request = new(
            AgentProviderKind.Copilot,
            new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow),
            new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"),
            new AgentWorkspaceContext(new GroupFolder("team"), "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([new AgentInstructionDocument("AGENTS.md", "# A", true)])),
            null,
            []);

        AgentExecutionResult result = await engine.ExecuteAsync(request);

        Assert.Equal(ContainerRunStatus.Success, result.Status);
        Assert.Equal("done", result.Result);
        Assert.Equal("copilot-session-1", result.Session?.SessionId);
        Assert.NotNull(client.CreatedConfiguration);
        Assert.Equal("gpt-5.4", client.CreatedConfiguration!.Model);
        Assert.Equal("/workspace/group", client.CreatedConfiguration.WorkingDirectory);
        Assert.NotNull(client.CreatedConfiguration.SystemMessage);
        Assert.Contains("instruction_document", client.CreatedConfiguration.SystemMessage);
        Assert.True(engine.Capabilities.SupportsWorkspaceInstructions);
    }

    [Fact]
    public async Task CopilotEngine_ResumesExistingSession()
    {
        FakeCopilotClientAdapter client = new(new FakeCopilotSessionAdapter("persisted-session", "/runtime/workspace", "done"));
        CopilotCodingAgentEngine engine = new(
            new FakeCopilotClientPool(client),
            new AgentRuntimeOptions
            {
                CopilotConfigDirectory = "/runtime/config"
            });

        AgentExecutionRequest request = new(
            AgentProviderKind.Copilot,
            new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow),
            new ContainerInput("Prompt", new SessionId("persisted-session"), new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"),
            new AgentWorkspaceContext(new GroupFolder("team"), "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([new AgentInstructionDocument("AGENTS.md", "# A", true)])),
            new AgentSessionReference(AgentProviderKind.Copilot, "persisted-session", "/workspace/runtime/team"),
            []);

        AgentExecutionResult result = await engine.ExecuteAsync(request);

        Assert.Equal(ContainerRunStatus.Success, result.Status);
        Assert.Equal("persisted-session", client.ResumedSessionId);
    }

    [Fact]
    public async Task CopilotEngine_InteractiveSession_CloseCancelsInFlightPrompt()
    {
        BlockingCopilotSessionAdapter session = new("copilot-session-1", "/runtime/workspace");
        FakeCopilotClientAdapter client = new(session);
        CopilotCodingAgentEngine engine = new(
            new FakeCopilotClientPool(client),
            new AgentRuntimeOptions
            {
                CopilotConfigDirectory = "/runtime/config",
                InteractiveIdleTimeout = TimeSpan.FromMinutes(5)
            });

        AgentExecutionRequest request = new(
            AgentProviderKind.Copilot,
            new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow),
            new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"),
            new AgentWorkspaceContext(new GroupFolder("team"), "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([new AgentInstructionDocument("AGENTS.md", "# A", true)])),
            null,
            []);

        await using IInteractiveAgentSession interactiveSession = await engine.StartInteractiveSessionAsync(request);
        await session.PromptStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        interactiveSession.RequestClose();
        AgentExecutionResult result = await interactiveSession.Completion.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(ContainerRunStatus.Interrupted, result.Status);
        Assert.Equal("Interactive session interrupted.", result.Error);
        Assert.True(await session.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task WorkspaceBuilder_ProducesAgensDocumentAndDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), $"netclaw-agent-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "groups", "global"));

        try
        {
            StorageOptions storageOptions = StorageOptions.Create(root);
            NetClawAgentWorkspaceBuilder builder = new(
                new GroupPathResolver(storageOptions, new PhysicalFileSystem()),
                storageOptions,
                new PhysicalFileSystem(),
                new AssistantIdentityOptions { Name = "NetClaw" });

            AgentWorkspaceContext context = await builder.BuildAsync(
                new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow),
                new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "NetClaw"));

            Assert.Equal(Path.Combine(root, "groups", "team"), context.WorkingDirectory);
            Assert.Equal(Path.Combine(root, "data", "sessions", "team"), context.SessionDirectory);
            Assert.Equal(Path.Combine(root, "data", "agent-workspaces", "team"), context.WorkspaceDirectory);
            Assert.Single(context.AdditionalDirectories);
            Assert.Equal("AGENTS.md", context.Instructions.Documents[0].RelativePath);
            Assert.True(Directory.Exists(context.WorkingDirectory));
            Assert.True(Directory.Exists(context.SessionDirectory));
            Assert.True(Directory.Exists(context.WorkspaceDirectory));
            Assert.True(File.Exists(Path.Combine(context.WorkingDirectory, "AGENTS.md")));
            Assert.True(File.Exists(Path.Combine(context.WorkspaceDirectory, "AGENTS.md")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AgentRuntime_UsesConfiguredEngineAndPersistsReturnedSession()
    {
        InMemoryGroupRepository groupRepository = new();
        InMemorySessionRepository sessionRepository = new();
        RegisteredGroup group = new("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow);
        await groupRepository.UpsertAsync(new ChatJid("team@jid"), group);

        NetClawAgentRuntime runtime = new(
            [new TestCodingAgentEngine()],
            groupRepository,
            sessionRepository,
            new TestWorkspaceBuilder(),
            new NetClawAgentToolRegistry(),
            new AgentRuntimeOptions { DefaultProvider = "copilot", KeepContainerBoundary = true });

        ContainerExecutionResult result = await runtime.ExecuteAsync(
            new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"));

        Assert.Equal(ContainerRunStatus.Success, result.Status);
        Assert.Equal("done", result.Result);
        Assert.Equal("copilot-session-1", result.NewSessionId?.Value);
        Assert.Equal("copilot-session-1", sessionRepository.Stored[new GroupFolder("team")].Value);
    }

    private sealed class TestCodingAgentEngine : IInteractiveCodingAgentEngine
    {
        public AgentProviderKind Provider => AgentProviderKind.Copilot;

        public AgentCapabilityProfile Capabilities => new(
            AgentProviderKind.Copilot,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            false,
            true,
            true,
            true,
            false);

        public Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentExecutionResult(ContainerRunStatus.Success, "done", new AgentSessionReference(AgentProviderKind.Copilot, "copilot-session-1", request.Workspace.WorkspaceDirectory), null));
        }

        public Task<IInteractiveAgentSession> StartInteractiveSessionAsync(AgentExecutionRequest request, Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IInteractiveAgentSession>(new TestInteractiveAgentSession(request.Workspace.WorkspaceDirectory));
        }
    }

    private sealed class TestInteractiveAgentSession : IInteractiveAgentSession
    {
        public TestInteractiveAgentSession(string workspaceDirectory)
        {
            Session = new AgentSessionReference(AgentProviderKind.Copilot, "copilot-session-1", workspaceDirectory);
            Completion = Task.FromResult(new AgentExecutionResult(ContainerRunStatus.Success, "done", Session, null));
        }

        public AgentSessionReference Session { get; }

        public Task<AgentExecutionResult> Completion { get; }

        public bool TryPostInput(string text) => true;

        public void RequestClose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestWorkspaceBuilder : IAgentWorkspaceBuilder
    {
        public Task<AgentWorkspaceContext> BuildAsync(RegisteredGroup group, ContainerInput input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentWorkspaceContext(group.Folder, "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([new AgentInstructionDocument("AGENTS.md", "# A", true)])));
        }
    }

    private sealed class FakeCopilotClientPool : ICopilotClientPool
    {
        private readonly ICopilotClientAdapter client;

        public FakeCopilotClientPool(ICopilotClientAdapter client)
        {
            this.client = client;
        }

        public Task<ICopilotClientAdapter> GetClientAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(client);
        }
    }

    private sealed class FakeCopilotClientAdapter : ICopilotClientAdapter
    {
        private readonly ICopilotSessionAdapter session;

        public FakeCopilotClientAdapter(ICopilotSessionAdapter session)
        {
            this.session = session;
        }

        public CopilotSessionConfiguration? CreatedConfiguration { get; private set; }

        public string? ResumedSessionId { get; private set; }

        public Task<ICopilotSessionAdapter> CreateSessionAsync(CopilotSessionConfiguration configuration, Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            CreatedConfiguration = configuration;
            return Task.FromResult(session);
        }

        public Task<ICopilotSessionAdapter> ResumeSessionAsync(string sessionId, CopilotSessionConfiguration configuration, Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            ResumedSessionId = sessionId;
            CreatedConfiguration = configuration;
            return Task.FromResult(session);
        }
    }

    private sealed class FakeCopilotSessionAdapter : ICopilotSessionAdapter
    {
        private readonly string result;

        public FakeCopilotSessionAdapter(string sessionId, string? workspacePath, string result)
        {
            SessionId = sessionId;
            WorkspacePath = workspacePath;
            this.result = result;
        }

        public string SessionId { get; }

        public string? WorkspacePath { get; }

        public Task<string?> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(result);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingCopilotSessionAdapter : ICopilotSessionAdapter
    {
        public BlockingCopilotSessionAdapter(string sessionId, string? workspacePath)
        {
            SessionId = sessionId;
            WorkspacePath = workspacePath;
        }

        public string SessionId { get; }

        public string? WorkspacePath { get; }

        public TaskCompletionSource<bool> PromptStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string?> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            PromptStarted.TrySetResult(true);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult(true);
                throw;
            }

            return "done";
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryGroupRepository : IGroupRepository
    {
        private readonly Dictionary<ChatJid, RegisteredGroup> groups = [];

        public Task<IReadOnlyDictionary<ChatJid, RegisteredGroup>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<ChatJid, RegisteredGroup>>(groups);

        public Task<RegisteredGroup?> GetByJidAsync(ChatJid chatJid, CancellationToken cancellationToken = default) => Task.FromResult(groups.TryGetValue(chatJid, out RegisteredGroup? group) ? group : null);

        public Task UpsertAsync(ChatJid chatJid, RegisteredGroup group, CancellationToken cancellationToken = default)
        {
            groups[chatJid] = group;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySessionRepository : ISessionRepository
    {
        public Dictionary<GroupFolder, SessionId> Stored { get; } = [];

        public Task<IReadOnlyDictionary<GroupFolder, SessionId>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<GroupFolder, SessionId>>(Stored);

        public Task<SessionId?> GetByGroupFolderAsync(GroupFolder groupFolder, CancellationToken cancellationToken = default) => Task.FromResult<SessionId?>(Stored.TryGetValue(groupFolder, out SessionId sessionId) ? sessionId : null);

        public Task UpsertAsync(SessionState sessionState, CancellationToken cancellationToken = default)
        {
            Stored[sessionState.GroupFolder] = sessionState.SessionId;
            return Task.CompletedTask;
        }
    }
}