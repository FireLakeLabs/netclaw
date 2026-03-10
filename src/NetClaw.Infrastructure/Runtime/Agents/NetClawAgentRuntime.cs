using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class NetClawAgentRuntime : IAgentRuntime
{
    private readonly AgentRuntimeOptions options;
    private readonly IReadOnlyDictionary<AgentProviderKind, ICodingAgentEngine> engines;
    private readonly IGroupRepository groupRepository;
    private readonly ISessionRepository sessionRepository;
    private readonly IAgentToolRegistry toolRegistry;
    private readonly IAgentWorkspaceBuilder workspaceBuilder;

    public NetClawAgentRuntime(
        IEnumerable<ICodingAgentEngine> engines,
        IGroupRepository groupRepository,
        ISessionRepository sessionRepository,
        IAgentWorkspaceBuilder workspaceBuilder,
        IAgentToolRegistry toolRegistry,
        AgentRuntimeOptions options)
    {
        this.engines = engines.ToDictionary(engine => engine.Provider);
        this.groupRepository = groupRepository;
        this.sessionRepository = sessionRepository;
        this.workspaceBuilder = workspaceBuilder;
        this.toolRegistry = toolRegistry;
        this.options = options;
    }

    public async Task<ContainerExecutionResult> ExecuteAsync(
        ContainerInput input,
        Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        RegisteredGroup? group = await groupRepository.GetByJidAsync(input.ChatJid, cancellationToken);
        AgentProviderKind provider = options.GetDefaultProvider();

        if (group is null)
        {
            return new ContainerExecutionResult(
                ContainerRunStatus.Error,
                null,
                input.SessionId,
                "Group not registered.",
                BuildContainerName(provider, input.GroupFolder));
        }

        if (!engines.TryGetValue(provider, out ICodingAgentEngine? engine))
        {
            return new ContainerExecutionResult(
                ContainerRunStatus.Error,
                null,
                input.SessionId,
                $"No coding agent engine is registered for provider '{provider}'.",
                BuildContainerName(provider, group.Folder));
        }

        ContainerInput effectiveInput = input with { IsMain = group.IsMain };
        AgentWorkspaceContext workspace = await workspaceBuilder.BuildAsync(group, effectiveInput, cancellationToken);
        AgentSessionReference? session = await ResolveSessionAsync(provider, group.Folder, effectiveInput.SessionId, workspace, cancellationToken);
        AgentExecutionRequest request = new(provider, group, effectiveInput, workspace, session, toolRegistry.GetTools(group, effectiveInput));

        AgentExecutionResult executionResult = await engine.ExecuteAsync(
            request,
            onStreamEvent is null ? null : (agentEvent, ct) => onStreamEvent(TranslateStreamEvent(agentEvent), ct),
            cancellationToken);

        if (executionResult.Session is not null)
        {
            await sessionRepository.UpsertAsync(
                new SessionState(group.Folder, new SessionId(executionResult.Session.SessionId)),
                cancellationToken);
        }

        return new ContainerExecutionResult(
            executionResult.Status,
            executionResult.Result,
            executionResult.Session is null ? null : new SessionId(executionResult.Session.SessionId),
            executionResult.Error,
            BuildContainerName(provider, group.Folder));
    }

    private async Task<AgentSessionReference?> ResolveSessionAsync(
        AgentProviderKind provider,
        GroupFolder groupFolder,
        SessionId? requestedSessionId,
        AgentWorkspaceContext workspace,
        CancellationToken cancellationToken)
    {
        if (requestedSessionId is { } explicitSessionId)
        {
            return new AgentSessionReference(provider, explicitSessionId.Value, workspace.WorkspaceDirectory);
        }

        SessionId? persistedSessionId = await sessionRepository.GetByGroupFolderAsync(groupFolder, cancellationToken);
        return persistedSessionId is null ? null : new AgentSessionReference(provider, persistedSessionId.Value.Value, workspace.WorkspaceDirectory);
    }

    private static ContainerStreamEvent TranslateStreamEvent(AgentStreamEvent agentEvent)
    {
        ContainerRunStatus status = agentEvent.Kind == AgentEventKind.Error ? ContainerRunStatus.Error : ContainerRunStatus.Running;
        SessionId? sessionId = agentEvent.Session is null ? null : new SessionId(agentEvent.Session.SessionId);

        return new ContainerStreamEvent(
            TranslateEventKind(agentEvent.Kind),
            new ContainerOutput(status, agentEvent.Content, sessionId, agentEvent.Error),
            agentEvent.ObservedAt);
    }

    private static ContainerEventKind TranslateEventKind(AgentEventKind kind)
    {
        return kind switch
        {
            AgentEventKind.SessionStarted => ContainerEventKind.SessionStarted,
            AgentEventKind.TextDelta => ContainerEventKind.TextDelta,
            AgentEventKind.MessageCompleted => ContainerEventKind.MessageCompleted,
            AgentEventKind.ToolStarted => ContainerEventKind.ToolStarted,
            AgentEventKind.ToolCompleted => ContainerEventKind.ToolCompleted,
            AgentEventKind.ReasoningDelta => ContainerEventKind.ReasoningDelta,
            AgentEventKind.Idle => ContainerEventKind.Idle,
            AgentEventKind.Error => ContainerEventKind.Error,
            _ => ContainerEventKind.Error
        };
    }

    private static ContainerName BuildContainerName(AgentProviderKind provider, GroupFolder groupFolder)
    {
        string safeGroup = new string(groupFolder.Value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray()).Trim('-');
        return new ContainerName($"agent-{provider.ToString().ToLowerInvariant()}-{safeGroup}");
    }
}