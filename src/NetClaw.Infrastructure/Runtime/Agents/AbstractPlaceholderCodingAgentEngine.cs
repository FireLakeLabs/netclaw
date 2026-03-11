using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Runtime.Agents;

public abstract class AbstractPlaceholderCodingAgentEngine : ICodingAgentEngine
{
    protected AbstractPlaceholderCodingAgentEngine(AgentProviderKind provider, AgentCapabilityProfile capabilities)
    {
        Provider = provider;
        Capabilities = capabilities;
    }

    public AgentProviderKind Provider { get; }

    public AgentCapabilityProfile Capabilities { get; }

    public virtual async Task<AgentExecutionResult> ExecuteAsync(
        AgentExecutionRequest request,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        string error = $"{Provider} engine is not implemented yet.";

        if (onStreamEvent is not null)
        {
            await onStreamEvent(new AgentStreamEvent(AgentEventKind.Error, null, null, request.Session, error, DateTimeOffset.UtcNow), cancellationToken);
        }

        return new AgentExecutionResult(ContainerRunStatus.Error, null, request.Session, error);
    }
}
