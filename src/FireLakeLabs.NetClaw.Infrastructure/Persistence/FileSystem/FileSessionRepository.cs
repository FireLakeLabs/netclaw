using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// File-based session repository. Each group stores its session ID in
/// <c>groups/{folder}/session.json</c>.
/// </summary>
public sealed class FileSessionRepository : ISessionRepository
{
    private readonly FileStoragePaths _paths;

    public FileSessionRepository(FileStoragePaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyDictionary<GroupFolder, SessionId>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<GroupFolder, SessionId> result = [];

        if (!Directory.Exists(_paths.GroupsDirectory))
        {
            return result;
        }

        foreach (string groupDir in Directory.GetDirectories(_paths.GroupsDirectory))
        {
            string sessionFile = Path.Combine(groupDir, "session.json");
            if (!File.Exists(sessionFile))
            {
                continue;
            }

            SessionDto? dto = await ReadSessionFileAsync(sessionFile, cancellationToken);
            if (dto is not null)
            {
                result[new GroupFolder(dto.GroupFolder)] = new SessionId(dto.SessionId);
            }
        }

        return result;
    }

    public async Task<SessionId?> GetByGroupFolderAsync(GroupFolder groupFolder, CancellationToken cancellationToken = default)
    {
        string sessionFile = _paths.SessionFilePath(groupFolder.Value);
        SessionDto? dto = await ReadSessionFileAsync(sessionFile, cancellationToken);
        return dto is not null ? new SessionId(dto.SessionId) : null;
    }

    public async Task UpsertAsync(SessionState sessionState, CancellationToken cancellationToken = default)
    {
        string sessionFile = _paths.SessionFilePath(sessionState.GroupFolder.Value);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionFile)!);

        SessionDto dto = new(sessionState.GroupFolder.Value, sessionState.SessionId.Value);
        await FileAtomicWriter.WriteJsonAsync(sessionFile, dto, FileSystemJsonOptions.Config, cancellationToken);
    }

    private static async Task<SessionDto?> ReadSessionFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return System.Text.Json.JsonSerializer.Deserialize<SessionDto>(json, FileSystemJsonOptions.Config);
    }

    private sealed record SessionDto(string GroupFolder, string SessionId);
}
