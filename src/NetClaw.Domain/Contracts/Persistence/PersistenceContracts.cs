using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Contracts.Persistence;

public interface IMessageRepository
{
    Task StoreMessageAsync(StoredMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default);

    Task StoreChatMetadataAsync(ChatInfo chatInfo, CancellationToken cancellationToken = default);
}

public interface IGroupRepository
{
    Task UpsertAsync(ChatJid chatJid, RegisteredGroup group, CancellationToken cancellationToken = default);

    Task<RegisteredGroup?> GetByJidAsync(ChatJid chatJid, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<ChatJid, RegisteredGroup>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface ISessionRepository
{
    Task<IReadOnlyDictionary<GroupFolder, SessionId>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SessionId?> GetByGroupFolderAsync(GroupFolder groupFolder, CancellationToken cancellationToken = default);

    Task UpsertAsync(SessionState sessionState, CancellationToken cancellationToken = default);
}

public interface ITaskRepository
{
    Task CreateAsync(ScheduledTask task, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<ScheduledTask?> GetByIdAsync(TaskId taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);

    Task AppendRunLogAsync(TaskRunLog log, CancellationToken cancellationToken = default);
}

public interface IRouterStateRepository
{
    Task<RouterStateEntry?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(RouterStateEntry entry, CancellationToken cancellationToken = default);
}