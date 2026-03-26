using System.Collections.Concurrent;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using TaskStatusEnum = FireLakeLabs.NetClaw.Domain.Enums.TaskStatus;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// File-based task repository. Each task lives in <c>data/tasks/{taskId}/</c> with
/// <c>config.json</c> for the task definition and <c>runs.jsonl</c> for run history.
/// All tasks are loaded into memory at startup.
/// </summary>
public sealed class FileTaskRepository : ITaskRepository
{
    private readonly FileStoragePaths _paths;
    private readonly ConcurrentDictionary<string, ScheduledTask> _cache = new(StringComparer.Ordinal);

    // Per-task lock for run log appends
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _runLogLocks = new(StringComparer.Ordinal);

    public FileTaskRepository(FileStoragePaths paths)
    {
        _paths = paths;
        Directory.CreateDirectory(_paths.TasksDirectory);
        LoadAll();
    }

    public async Task CreateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        string taskDir = _paths.TaskDirectory(task.Id.Value);
        Directory.CreateDirectory(taskDir);

        await FileAtomicWriter.WriteJsonAsync(
            _paths.TaskConfigFilePath(task.Id.Value),
            ToRecord(task),
            FileSystemJsonOptions.Config,
            cancellationToken);

        _cache[task.Id.Value] = task;
    }

    public Task<ScheduledTask?> GetByIdAsync(TaskId taskId, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(taskId.Value, out ScheduledTask? task);
        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScheduledTask> tasks = _cache.Values.ToList();
        return Task.FromResult(tasks);
    }

    public Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScheduledTask> due = _cache.Values
            .Where(t => t.Status == TaskStatusEnum.Active && t.NextRun is not null && t.NextRun <= now)
            .ToList();
        return Task.FromResult(due);
    }

    public async Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        await FileAtomicWriter.WriteJsonAsync(
            _paths.TaskConfigFilePath(task.Id.Value),
            ToRecord(task),
            FileSystemJsonOptions.Config,
            cancellationToken);

        _cache[task.Id.Value] = task;
    }

    public async Task AppendRunLogAsync(TaskRunLog log, CancellationToken cancellationToken = default)
    {
        string runsFile = _paths.TaskRunsFilePath(log.TaskId.Value);
        SemaphoreSlim runLock = _runLogLocks.GetOrAdd(log.TaskId.Value, _ => new SemaphoreSlim(1, 1));

        await runLock.WaitAsync(cancellationToken);
        try
        {
            await JsonlFileAppender.AppendAsync(runsFile, ToRunRecord(log), FileSystemJsonOptions.Jsonl, cancellationToken);
        }
        finally
        {
            runLock.Release();
        }
    }

    public async Task<IReadOnlyList<TaskRunLog>> GetRunLogsAsync(TaskId taskId, int limit = 50, CancellationToken cancellationToken = default)
    {
        string runsFile = _paths.TaskRunsFilePath(taskId.Value);
        string[] tailLines = await JsonlFileReader.ReadTailLinesAsync(runsFile, limit, cancellationToken);

        List<TaskRunLog> logs = [];
        foreach (string line in tailLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                RunLogRecord? record = JsonSerializer.Deserialize<RunLogRecord>(line, FileSystemJsonOptions.Jsonl);
                if (record is not null)
                {
                    logs.Add(ToRunLog(taskId, record));
                }
            }
            catch (JsonException)
            {
                // Tolerate partial lines
            }
        }

        // Return in reverse order (most recent first)
        logs.Reverse();
        return logs;
    }

    private void LoadAll()
    {
        if (!Directory.Exists(_paths.TasksDirectory))
        {
            return;
        }

        foreach (string taskDir in Directory.GetDirectories(_paths.TasksDirectory))
        {
            string configFile = Path.Combine(taskDir, "config.json");
            if (!File.Exists(configFile))
            {
                continue;
            }

            string json = File.ReadAllText(configFile);
            TaskRecord? record = JsonSerializer.Deserialize<TaskRecord>(json, FileSystemJsonOptions.Config);
            if (record is not null)
            {
                ScheduledTask task = ToTask(record);
                _cache[task.Id.Value] = task;
            }
        }
    }

    private static ScheduledTask ToTask(TaskRecord r) =>
        new(
            new TaskId(r.Id),
            new GroupFolder(r.GroupFolder),
            new ChatJid(r.ChatJid),
            r.Prompt,
            r.ScheduleType,
            r.ScheduleValue,
            r.ContextMode,
            r.NextRun,
            r.LastRun,
            r.LastResult,
            r.Status,
            r.CreatedAt);

    private static TaskRecord ToRecord(ScheduledTask t) =>
        new(
            t.Id.Value,
            t.GroupFolder.Value,
            t.ChatJid.Value,
            t.Prompt,
            t.ScheduleType,
            t.ScheduleValue,
            t.ContextMode,
            t.NextRun,
            t.LastRun,
            t.LastResult,
            t.Status,
            t.CreatedAt);

    private static TaskRunLog ToRunLog(TaskId taskId, RunLogRecord r) =>
        new(taskId, r.RunAt, TimeSpan.FromMilliseconds(r.DurationMs), r.Status, r.Result, r.Error);

    private static RunLogRecord ToRunRecord(TaskRunLog l) =>
        new(l.RunAt, (long)l.Duration.TotalMilliseconds, l.Status, l.Result, l.Error);

    private sealed record TaskRecord(
        string Id,
        string GroupFolder,
        string ChatJid,
        string Prompt,
        ScheduleType ScheduleType,
        string ScheduleValue,
        TaskContextMode ContextMode,
        DateTimeOffset? NextRun,
        DateTimeOffset? LastRun,
        string? LastResult,
        TaskStatusEnum Status,
        DateTimeOffset CreatedAt);

    private sealed record RunLogRecord(
        DateTimeOffset RunAt,
        long DurationMs,
        ContainerRunStatus Status,
        string? Result,
        string? Error);
}
