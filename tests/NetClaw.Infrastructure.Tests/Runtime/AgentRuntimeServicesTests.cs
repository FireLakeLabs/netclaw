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
    public async Task CopilotPlaceholderEngine_ReturnsStructuredNotImplementedError()
    {
        CopilotCodingAgentEngine engine = new();
        AgentExecutionRequest request = new(
            AgentProviderKind.Copilot,
            new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow),
            new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"),
            new AgentWorkspaceContext(new GroupFolder("team"), "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([new AgentInstructionDocument("AGENTS.md", "# A", true)])),
            null,
            []);

        AgentExecutionResult result = await engine.ExecuteAsync(request);

        Assert.Equal(ContainerRunStatus.Error, result.Status);
        Assert.Equal("Copilot engine is not implemented yet.", result.Error);
        Assert.True(engine.Capabilities.SupportsWorkspaceInstructions);
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

    private sealed class TestCodingAgentEngine : ICodingAgentEngine
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
    }

    private sealed class TestWorkspaceBuilder : IAgentWorkspaceBuilder
    {
        public Task<AgentWorkspaceContext> BuildAsync(RegisteredGroup group, ContainerInput input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentWorkspaceContext(group.Folder, "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([new AgentInstructionDocument("AGENTS.md", "# A", true)])));
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