using FireLakeLabs.NetClaw.Domain.Contracts.Containers;

namespace FireLakeLabs.NetClaw.AgentRunner;

public interface IAgentProvider
{
    Task<ContainerOutput> ExecuteAsync(AgentRunnerContext context, Action<ContainerOutput> onStreamOutput, CancellationToken cancellationToken);
}
