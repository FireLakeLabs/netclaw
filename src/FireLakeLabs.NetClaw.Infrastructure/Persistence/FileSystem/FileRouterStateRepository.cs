using System.Collections.Concurrent;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using System.Text.Json;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// File-based router state repository. Stores all keys in a single <c>data/state.json</c> file.
/// The entire dictionary is loaded into memory on startup and flushed atomically on every write.
/// </summary>
public sealed class FileRouterStateRepository : IRouterStateRepository
{
    private readonly string _stateFilePath;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public FileRouterStateRepository(FileStoragePaths paths)
    {
        _stateFilePath = paths.StateFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
        Load();
    }

    public Task<RouterStateEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(key, out string? value);
        return Task.FromResult(value is not null ? new RouterStateEntry(key, value) : null);
    }

    public Task<IReadOnlyList<RouterStateEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RouterStateEntry> entries = _cache
            .Select(kvp => new RouterStateEntry(kvp.Key, kvp.Value))
            .ToList();
        return Task.FromResult(entries);
    }

    public async Task SetAsync(RouterStateEntry entry, CancellationToken cancellationToken = default)
    {
        _cache[entry.Key] = entry.Value;
        await FlushAsync(cancellationToken);
    }

    private void Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return;
        }

        string json = File.ReadAllText(_stateFilePath);
        Dictionary<string, string>? dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, FileSystemJsonOptions.Config);
        if (dict is not null)
        {
            foreach ((string k, string v) in dict)
            {
                _cache[k] = v;
            }
        }
    }

    private Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> snapshot = new(_cache, StringComparer.Ordinal);
        return FileAtomicWriter.WriteJsonAsync(_stateFilePath, snapshot, FileSystemJsonOptions.Config, cancellationToken);
    }
}
