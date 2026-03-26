using System.Collections.Concurrent;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// File-based group repository. Stores all registered groups in <c>data/groups.json</c>.
/// On every upsert, also rebuilds <c>data/chat-groups.json</c> (the ChatJid → GroupFolder mapping).
/// </summary>
public sealed class FileGroupRepository : IGroupRepository
{
    private readonly FileStoragePaths _paths;

    // keyed by ChatJid.Value
    private readonly ConcurrentDictionary<string, (ChatJid Jid, RegisteredGroup Group)> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileGroupRepository(FileStoragePaths paths)
    {
        _paths = paths;
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.GroupsFilePath)!);
        Load();
    }

    public Task<RegisteredGroup?> GetByJidAsync(ChatJid chatJid, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(chatJid.Value, out (ChatJid, RegisteredGroup Group) entry);
        return Task.FromResult<RegisteredGroup?>(entry.Group);
    }

    public Task<IReadOnlyDictionary<ChatJid, RegisteredGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<ChatJid, RegisteredGroup> dict = _cache.Values
            .ToDictionary(e => e.Jid, e => e.Group);
        return Task.FromResult(dict);
    }

    public async Task UpsertAsync(ChatJid chatJid, RegisteredGroup group, CancellationToken cancellationToken = default)
    {
        _cache[chatJid.Value] = (chatJid, group);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await FlushGroupsAsync(cancellationToken);
            await FlushChatGroupsAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void Load()
    {
        if (!File.Exists(_paths.GroupsFilePath))
        {
            return;
        }

        string json = File.ReadAllText(_paths.GroupsFilePath);
        List<GroupRecord>? records = JsonSerializer.Deserialize<List<GroupRecord>>(json, FileSystemJsonOptions.Config);
        if (records is null)
        {
            return;
        }

        foreach (GroupRecord r in records)
        {
            RegisteredGroup group = ToGroup(r);
            _cache[r.Jid] = (new ChatJid(r.Jid), group);
        }
    }

    private Task FlushGroupsAsync(CancellationToken cancellationToken)
    {
        List<GroupRecord> records = _cache.Values
            .Select(e => FromGroup(e.Jid, e.Group))
            .ToList();
        return FileAtomicWriter.WriteJsonAsync(_paths.GroupsFilePath, records, FileSystemJsonOptions.Config, cancellationToken);
    }

    private Task FlushChatGroupsAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, string> mapping = _cache.Values
            .ToDictionary(e => e.Jid.Value, e => e.Group.Folder.Value);
        return FileAtomicWriter.WriteJsonAsync(_paths.ChatGroupsFilePath, mapping, FileSystemJsonOptions.Config, cancellationToken);
    }

    private static RegisteredGroup ToGroup(GroupRecord r) =>
        new(r.Name, new GroupFolder(r.Folder), r.Trigger, r.AddedAt, r.ContainerConfig, r.RequiresTrigger, r.IsMain);

    private static GroupRecord FromGroup(ChatJid jid, RegisteredGroup g) =>
        new(jid.Value, g.Name, g.Folder.Value, g.Trigger, g.AddedAt, g.ContainerConfig, g.RequiresTrigger, g.IsMain);

    private sealed record GroupRecord(
        string Jid,
        string Name,
        string Folder,
        string Trigger,
        DateTimeOffset AddedAt,
        ContainerConfig? ContainerConfig,
        bool RequiresTrigger,
        bool IsMain);
}
