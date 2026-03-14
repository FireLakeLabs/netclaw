using NetClaw.Dashboard.Models;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Dashboard.Services;

public sealed class WorkspaceFileService
{
    private const long MaxFileSizeBytes = 1024 * 1024;
    private readonly string groupsDirectory;
    private readonly string dataDirectory;

    public WorkspaceFileService(string groupsDirectory, string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(groupsDirectory))
        {
            throw new ArgumentException("Groups directory is required.", nameof(groupsDirectory));
        }

        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        }

        this.groupsDirectory = Path.GetFullPath(groupsDirectory);
        this.dataDirectory = Path.GetFullPath(dataDirectory);
    }

    public IReadOnlyList<WorkspaceTreeEntryDto> GetTree(GroupFolder groupFolder)
    {
        List<WorkspaceTreeEntryDto> roots = [];

        string groupDir = ResolveGroupDirectory(groupFolder);
        if (Directory.Exists(groupDir))
        {
            roots.Add(BuildTreeEntry(new DirectoryInfo(groupDir), "groups", groupDir));
        }

        string workspaceDir = Path.Combine(dataDirectory, "agent-workspaces", groupFolder.Value);
        string fullWorkspaceDir = Path.GetFullPath(workspaceDir);
        ValidateWithinBase(dataDirectory, fullWorkspaceDir);
        if (Directory.Exists(fullWorkspaceDir))
        {
            roots.Add(BuildTreeEntry(new DirectoryInfo(fullWorkspaceDir), "workspace", fullWorkspaceDir));
        }

        string sessionDir = Path.Combine(dataDirectory, "sessions", groupFolder.Value);
        string fullSessionDir = Path.GetFullPath(sessionDir);
        ValidateWithinBase(dataDirectory, fullSessionDir);
        if (Directory.Exists(fullSessionDir))
        {
            roots.Add(BuildTreeEntry(new DirectoryInfo(fullSessionDir), "sessions", fullSessionDir));
        }

        return roots;
    }

    public WorkspaceFileDto? ReadFile(GroupFolder groupFolder, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("File path is required.");
        }

        string fullPath = ResolveFilePath(groupFolder, relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        FileInfo fileInfo = new(fullPath);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"File exceeds maximum size of {MaxFileSizeBytes} bytes.");
        }

        string content = File.ReadAllText(fullPath);
        return new WorkspaceFileDto(relativePath, content, fileInfo.Length, new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));
    }

    private string ResolveGroupDirectory(GroupFolder groupFolder)
    {
        string path = Path.GetFullPath(Path.Combine(groupsDirectory, groupFolder.Value));
        ValidateWithinBase(groupsDirectory, path);
        return path;
    }

    private string ResolveFilePath(GroupFolder groupFolder, string relativePath)
    {
        string groupDir = ResolveGroupDirectory(groupFolder);
        string workspaceDir = Path.GetFullPath(Path.Combine(dataDirectory, "agent-workspaces", groupFolder.Value));
        string sessionDir = Path.GetFullPath(Path.Combine(dataDirectory, "sessions", groupFolder.Value));

        string fullPath = Path.GetFullPath(Path.Combine(groupDir, relativePath));

        if (IsWithinBase(groupDir, fullPath) || IsWithinBase(workspaceDir, fullPath) || IsWithinBase(sessionDir, fullPath))
        {
            return fullPath;
        }

        fullPath = Path.GetFullPath(Path.Combine(workspaceDir, relativePath));
        if (IsWithinBase(workspaceDir, fullPath))
        {
            return fullPath;
        }

        fullPath = Path.GetFullPath(Path.Combine(sessionDir, relativePath));
        if (IsWithinBase(sessionDir, fullPath))
        {
            return fullPath;
        }

        throw new InvalidOperationException("Path escapes allowed workspace directories.");
    }

    private static void ValidateWithinBase(string baseDirectory, string fullPath)
    {
        if (!IsWithinBase(baseDirectory, fullPath))
        {
            throw new InvalidOperationException($"Path escapes base directory: {fullPath}");
        }
    }

    private static bool IsWithinBase(string baseDirectory, string fullPath)
    {
        string relativePath = Path.GetRelativePath(baseDirectory, fullPath);
        return !relativePath.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathFullyQualified(relativePath);
    }

    private static WorkspaceTreeEntryDto BuildTreeEntry(DirectoryInfo directory, string rootLabel, string rootPath)
    {
        return new WorkspaceTreeEntryDto(
            rootLabel,
            string.Empty,
            IsDirectory: true,
            SizeBytes: null,
            LastModified: new DateTimeOffset(directory.LastWriteTimeUtc, TimeSpan.Zero),
            Children: BuildChildren(directory, rootPath));
    }

    private static IReadOnlyList<WorkspaceTreeEntryDto> BuildChildren(DirectoryInfo directory, string rootPath)
    {
        List<WorkspaceTreeEntryDto> items = [];

        try
        {
            foreach (DirectoryInfo subDir in directory.EnumerateDirectories().OrderBy(d => d.Name))
            {
                items.Add(new WorkspaceTreeEntryDto(
                    subDir.Name,
                    Path.GetRelativePath(rootPath, subDir.FullName),
                    IsDirectory: true,
                    SizeBytes: null,
                    LastModified: new DateTimeOffset(subDir.LastWriteTimeUtc, TimeSpan.Zero),
                    Children: BuildChildren(subDir, rootPath)));
            }

            foreach (FileInfo file in directory.EnumerateFiles().OrderBy(f => f.Name))
            {
                items.Add(new WorkspaceTreeEntryDto(
                    file.Name,
                    Path.GetRelativePath(rootPath, file.FullName),
                    IsDirectory: false,
                    SizeBytes: file.Length,
                    LastModified: new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                    Children: null));
            }
        }
        catch (UnauthorizedAccessException)
        {
        }

        return items;
    }
}
