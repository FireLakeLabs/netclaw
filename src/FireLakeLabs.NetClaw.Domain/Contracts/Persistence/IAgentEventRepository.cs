using FireLakeLabs.NetClaw.Domain.Entities;

namespace FireLakeLabs.NetClaw.Domain.Contracts.Persistence;

public interface IAgentEventRepository
{
    Task StoreAsync(AgentActivityEvent activityEvent, CancellationToken cancellationToken = default);

    Task StoreBatchAsync(IReadOnlyList<AgentActivityEvent> events, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentActivityEvent>> GetRecentAsync(int limit = 100, DateTimeOffset? since = null, string? groupFolder = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentActivityEvent>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentActivityEvent>> GetByTaskRunAsync(string taskId, DateTimeOffset runAt, CancellationToken cancellationToken = default);
}
