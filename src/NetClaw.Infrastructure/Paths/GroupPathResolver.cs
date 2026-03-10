using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;

namespace NetClaw.Infrastructure.Paths;

public sealed class GroupPathResolver
{
    private readonly IFileSystem fileSystem;
    private readonly StorageOptions storageOptions;

    public GroupPathResolver(StorageOptions storageOptions, IFileSystem fileSystem)
    {
        this.storageOptions = storageOptions;
        this.fileSystem = fileSystem;
    }

    public string ResolveGroupDirectory(GroupFolder groupFolder)
    {
        return EnsureWithinBase(storageOptions.GroupsDirectory, Path.Combine(storageOptions.GroupsDirectory, groupFolder.Value));
    }

    public string ResolveGroupIpcDirectory(GroupFolder groupFolder)
    {
        return EnsureWithinBase(Path.Combine(storageOptions.DataDirectory, "ipc"), Path.Combine(storageOptions.DataDirectory, "ipc", groupFolder.Value));
    }

    private string EnsureWithinBase(string baseDirectory, string path)
    {
        string fullBaseDirectory = fileSystem.GetFullPath(baseDirectory);
        string fullPath = fileSystem.GetFullPath(path);
        string relativePath = Path.GetRelativePath(fullBaseDirectory, fullPath);

        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException($"Path escapes base directory: {fullPath}");
        }

        return fullPath;
    }
}