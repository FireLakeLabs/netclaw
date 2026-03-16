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
    private readonly IContainerExecutionService containerExecutionService;
    private readonly IGroupRepository groupRepository;
    private readonly ISessionRepository sessionRepository;
    private readonly IAgentWorkspaceBuilder workspaceBuilder;

    public NetClawAgentRuntime(
        IContainerExecutionService containerExecutionService,
        IGroupRepository groupRepository,
        ISessionRepository sessionRepository,
        IAgentWorkspaceBuilder workspaceBuilder,
        AgentRuntimeOptions options)
    {
        this.containerExecutionService = containerExecutionService;
        this.groupRepository = groupRepository;
        this.sessionRepository = sessionRepository;
        this.workspaceBuilder = workspaceBuilder;
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

        ContainerInput effectiveInput = input with { IsMain = group.IsMain };
        ContainerName containerName = BuildContainerName(provider, group.Folder);

        ContainerExecutionRequest request = new(group, effectiveInput, [], containerName);

        ContainerExecutionResult result = await containerExecutionService.RunAsync(request, onStreamEvent, cancellationToken);

        if (result.NewSessionId is { } newSessionId)
        {
            await sessionRepository.UpsertAsync(new SessionState(group.Folder, newSessionId), cancellationToken);
        }

        return result;
    }

    public async Task<IInteractiveContainerSession> StartInteractiveSessionAsync(
        ContainerInput input,
        Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        RegisteredGroup? group = await groupRepository.GetByJidAsync(input.ChatJid, cancellationToken);
        AgentProviderKind provider = options.GetDefaultProvider();

        if (group is null)
        {
            return new FailedInteractiveContainerSession(BuildContainerName(provider, input.GroupFolder), "Group not registered.");
        }

        // For interactive sessions, the container execution service handles the lifecycle.
        // We still return a session wrapper that delegates to the container.
        ContainerInput effectiveInput = input with { IsMain = group.IsMain };
        ContainerName containerName = BuildContainerName(provider, group.Folder);

        ContainerExecutionRequest request = new(group, effectiveInput, [], containerName);
        ContainerExecutionResult result = await containerExecutionService.RunAsync(request, onStreamEvent, cancellationToken);

        if (result.NewSessionId is { } newSessionId)
        {
            await sessionRepository.UpsertAsync(new SessionState(group.Folder, newSessionId), cancellationToken);
        }

        return new CompletedInteractiveContainerSession(containerName, result);
    }

    private static ContainerName BuildContainerName(AgentProviderKind provider, GroupFolder groupFolder)
    {
        string safeGroup = new string(groupFolder.Value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray()).Trim('-');
        return new ContainerName($"netclaw-{provider.ToString().ToLowerInvariant()}-{safeGroup}");
    }

    private sealed class CompletedInteractiveContainerSession : IInteractiveContainerSession
    {
        public CompletedInteractiveContainerSession(ContainerName containerName, ContainerExecutionResult result)
        {
            ContainerName = containerName;
            SessionId = result.NewSessionId;
            Completion = Task.FromResult(result);
        }

        public SessionId? SessionId { get; }

        public ContainerName ContainerName { get; }

        public Task<ContainerExecutionResult> Completion { get; }

        public bool TryPostInput(string text) => false;

        public void RequestClose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailedInteractiveContainerSession : IInteractiveContainerSession
    {
        public FailedInteractiveContainerSession(ContainerName containerName, string error)
        {
            ContainerName = containerName;
            Completion = Task.FromResult(new ContainerExecutionResult(ContainerRunStatus.Error, null, null, error, containerName));
        }

        public SessionId? SessionId => null;

        public ContainerName ContainerName { get; }

        public Task<ContainerExecutionResult> Completion { get; }

        public bool TryPostInput(string text) => false;

        public void RequestClose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
