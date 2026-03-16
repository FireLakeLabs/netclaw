using NetClaw.Domain.Contracts.Containers;

namespace NetClaw.AgentRunner;

public interface IAgentProvider
{
    Task<ContainerOutput> ExecuteAsync(AgentRunnerContext context, Action<ContainerOutput> onStreamOutput, CancellationToken cancellationToken);
}
