using System.Collections.Concurrent;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// File-based agent event repository. Events are stored in daily JSONL files:
/// <c>data/events/{groupFolder}/{yyyy-MM-dd}.jsonl</c>.
///
/// Event IDs are allocated from a <see cref="PersistentCounter"/> backed by
/// <c>data/events/next-id.txt</c>, ensuring uniqueness across process restarts.
/// Per-group semaphores serialize concurrent writes to the same day file.
/// </summary>
public sealed class FileAgentEventRepository : IAgentEventRepository
{
    private readonly FileStoragePaths _paths;
    private readonly PersistentCounter _idCounter;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _groupLocks = new(StringComparer.Ordinal);

    private FileAgentEventRepository(FileStoragePaths paths, PersistentCounter idCounter)
    {
        _paths = paths;
        _idCounter = idCounter;
    }

    /// <summary>
    /// Asynchronous factory — initializes the persistent ID counter before use.
    /// </summary>
    public static async Task<FileAgentEventRepository> CreateAsync(FileStoragePaths paths, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.EventsDirectory);

        PersistentCounter counter = await PersistentCounter.InitializeAsync(
            paths.EventCounterFilePath,
            () => ScanMaxExistingIdAsync(paths),
            cancellationToken);

        return new FileAgentEventRepository(paths, counter);
    }

    public async Task StoreAsync(AgentActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        AgentActivityEvent withId = WithId(activityEvent, _idCounter.Next());
        await AppendEventAsync(withId, cancellationToken);
        await _idCounter.FlushAsync(cancellationToken);
    }

    public async Task StoreBatchAsync(IReadOnlyList<AgentActivityEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return;
        }

        // Assign all IDs atomically before grouping.
        List<AgentActivityEvent> withIds = events.Select(e => WithId(e, _idCounter.Next())).ToList();

        // Group by (groupFolder, date) to minimize lock acquisitions.
        IEnumerable<IGrouping<(string GroupFolder, DateOnly Date), AgentActivityEvent>> grouped = withIds
            .GroupBy(e => (e.GroupFolder, DateOnly.FromDateTime(e.ObservedAt.UtcDateTime)));

        foreach (IGrouping<(string GroupFolder, DateOnly Date), AgentActivityEvent> group in grouped)
        {
            (string groupFolder, DateOnly date) = group.Key;
            string dailyFile = _paths.EventsDailyFilePath(groupFolder, date);
            SemaphoreSlim groupLock = _groupLocks.GetOrAdd(groupFolder, _ => new SemaphoreSlim(1, 1));

            await groupLock.WaitAsync(cancellationToken);
            try
            {
                Directory.CreateDirectory(_paths.EventsGroupDirectory(groupFolder));
                await JsonlFileAppender.AppendManyAsync(dailyFile, group.Select(ToRecord), FileSystemJsonOptions.Jsonl, cancellationToken);
            }
            finally
            {
                groupLock.Release();
            }
        }

        await _idCounter.FlushAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentActivityEvent>> GetRecentAsync(
        int limit = 100,
        DateTimeOffset? since = null,
        string? groupFolder = null,
        CancellationToken cancellationToken = default)
    {
        DateOnly sinceDate = since.HasValue ? DateOnly.FromDateTime(since.Value.UtcDateTime) : DateOnly.MinValue;
        IEnumerable<string> groupDirs = GetGroupDirs(groupFolder);

        List<AgentActivityEvent> events = [];
        foreach (string groupDir in groupDirs)
        {
            foreach (string file in GetDailyFilesFrom(groupDir, sinceDate))
            {
                IReadOnlyList<EventRecord> records = await JsonlFileReader.ReadFilteredAsync<EventRecord>(
                    file,
                    r => since is null || r.ObservedAt > since,
                    FileSystemJsonOptions.Jsonl,
                    cancellationToken);
                events.AddRange(records.Select(ToEvent));
            }
        }

        return events
            .OrderByDescending(e => e.ObservedAt)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<AgentActivityEvent>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> groupDirs = GetGroupDirs(null);
        List<AgentActivityEvent> events = [];

        foreach (string groupDir in groupDirs)
        {
            foreach (string file in GetDailyFilesFrom(groupDir, DateOnly.MinValue))
            {
                IReadOnlyList<EventRecord> records = await JsonlFileReader.ReadFilteredAsync<EventRecord>(
                    file,
                    r => r.SessionId == sessionId,
                    FileSystemJsonOptions.Jsonl,
                    cancellationToken);
                events.AddRange(records.Select(ToEvent));
            }
        }

        return events.OrderBy(e => e.ObservedAt).ToList();
    }

    public async Task<IReadOnlyList<AgentActivityEvent>> GetByTaskRunAsync(string taskId, DateTimeOffset runAt, CancellationToken cancellationToken = default)
    {
        DateOnly runDate = DateOnly.FromDateTime(runAt.UtcDateTime);
        IEnumerable<string> groupDirs = GetGroupDirs(null);
        List<AgentActivityEvent> events = [];

        foreach (string groupDir in groupDirs)
        {
            foreach (string file in GetDailyFilesFrom(groupDir, runDate))
            {
                IReadOnlyList<EventRecord> records = await JsonlFileReader.ReadFilteredAsync<EventRecord>(
                    file,
                    r => r.TaskId == taskId && r.ObservedAt >= runAt,
                    FileSystemJsonOptions.Jsonl,
                    cancellationToken);
                events.AddRange(records.Select(ToEvent));
            }
        }

        return events.OrderBy(e => e.ObservedAt).ToList();
    }

    private async Task AppendEventAsync(AgentActivityEvent e, CancellationToken cancellationToken)
    {
        string groupFolder = e.GroupFolder;
        string dailyFile = _paths.EventsDailyFilePath(groupFolder, DateOnly.FromDateTime(e.ObservedAt.UtcDateTime));
        SemaphoreSlim groupLock = _groupLocks.GetOrAdd(groupFolder, _ => new SemaphoreSlim(1, 1));

        await groupLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_paths.EventsGroupDirectory(groupFolder));
            await JsonlFileAppender.AppendAsync(dailyFile, ToRecord(e), FileSystemJsonOptions.Jsonl, cancellationToken);
        }
        finally
        {
            groupLock.Release();
        }
    }

    private IEnumerable<string> GetGroupDirs(string? groupFolder)
    {
        if (!Directory.Exists(_paths.EventsDirectory))
        {
            return [];
        }

        if (groupFolder is not null)
        {
            string dir = _paths.EventsGroupDirectory(groupFolder);
            return Directory.Exists(dir) ? [dir] : [];
        }

        return Directory.GetDirectories(_paths.EventsDirectory);
    }

    private static IEnumerable<string> GetDailyFilesFrom(string groupDir, DateOnly sinceDate) =>
        Directory.GetFiles(groupDir, "*.jsonl")
            .Where(f =>
            {
                string name = Path.GetFileNameWithoutExtension(f);
                return DateOnly.TryParseExact(name, "yyyy-MM-dd", out DateOnly fileDate) && fileDate >= sinceDate;
            })
            .OrderBy(f => f);

    private static async Task<long> ScanMaxExistingIdAsync(FileStoragePaths paths)
    {
        if (!Directory.Exists(paths.EventsDirectory))
        {
            return 0L;
        }

        long max = 0L;
        foreach (string groupDir in Directory.GetDirectories(paths.EventsDirectory))
        {
            foreach (string file in Directory.GetFiles(groupDir, "*.jsonl"))
            {
                IReadOnlyList<EventRecord> records = await JsonlFileReader.ReadAllAsync<EventRecord>(
                    file, FileSystemJsonOptions.Jsonl);
                foreach (EventRecord r in records)
                {
                    if (r.Id > max)
                    {
                        max = r.Id;
                    }
                }
            }
        }

        return max;
    }

    private static AgentActivityEvent WithId(AgentActivityEvent e, long id) =>
        new(id, e.GroupFolder, e.ChatJid, e.SessionId, e.EventKind, e.Content, e.ToolName, e.Error, e.IsScheduledTask, e.TaskId, e.ObservedAt, e.CapturedAt);

    private static AgentActivityEvent ToEvent(EventRecord r) =>
        new(r.Id, r.GroupFolder, r.ChatJid, r.SessionId, r.EventKind, r.Content, r.ToolName, r.Error, r.IsScheduledTask, r.TaskId, r.ObservedAt, r.CapturedAt);

    private static EventRecord ToRecord(AgentActivityEvent e) =>
        new(e.Id, e.GroupFolder, e.ChatJid, e.SessionId, e.EventKind, e.Content, e.ToolName, e.Error, e.IsScheduledTask, e.TaskId, e.ObservedAt, e.CapturedAt);

    private sealed record EventRecord(
        long Id,
        string GroupFolder,
        string ChatJid,
        string? SessionId,
        ContainerEventKind EventKind,
        string? Content,
        string? ToolName,
        string? Error,
        bool IsScheduledTask,
        string? TaskId,
        DateTimeOffset ObservedAt,
        DateTimeOffset CapturedAt);
}
