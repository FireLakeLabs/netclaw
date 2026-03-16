using Microsoft.Extensions.Logging;
using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class ContainerExecutionService : IContainerExecutionService
{
    private readonly ContainerizedAgentEngine engine;
    private readonly IAgentWorkspaceBuilder workspaceBuilder;
    private readonly IAgentToolRegistry toolRegistry;
    private readonly ILogger<ContainerExecutionService> logger;

    public ContainerExecutionService(
        ContainerizedAgentEngine engine,
        IAgentWorkspaceBuilder workspaceBuilder,
        IAgentToolRegistry toolRegistry,
        ILogger<ContainerExecutionService> logger)
    {
        this.engine = engine;
        this.workspaceBuilder = workspaceBuilder;
        this.toolRegistry = toolRegistry;
        this.logger = logger;
    }

    public async Task<ContainerExecutionResult> RunAsync(
        ContainerExecutionRequest request,
        Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        AgentWorkspaceContext workspace = await workspaceBuilder.BuildAsync(request.Group, request.Input, cancellationToken);
        IReadOnlyList<AgentToolDefinition> tools = toolRegistry.GetTools(request.Group, request.Input);

        AgentExecutionRequest agentRequest = new(
            engine.Provider,
            request.Group,
            request.Input,
            workspace,
            null,
            tools);

        Func<AgentStreamEvent, CancellationToken, Task>? agentStreamHandler = null;
        if (onStreamEvent is not null)
        {
            agentStreamHandler = (agentEvent, ct) =>
            {
                ContainerStreamEvent containerEvent = new(
                    TranslateEventKind(agentEvent.Kind),
                    new ContainerOutput(
                        agentEvent.Kind == AgentEventKind.Error ? ContainerRunStatus.Error : ContainerRunStatus.Running,
                        agentEvent.Content,
                        null,
                        agentEvent.Error),
                    agentEvent.ObservedAt);
                return onStreamEvent(containerEvent, ct);
            };
        }

        AgentExecutionResult result = await engine.ExecuteAsync(agentRequest, agentStreamHandler, cancellationToken);

        return new ContainerExecutionResult(
            result.Status,
            result.Result,
            result.Session is not null ? new Domain.ValueObjects.SessionId(result.Session.SessionId) : null,
            result.Error,
            request.ContainerName);
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
}
