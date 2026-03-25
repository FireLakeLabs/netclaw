using FireLakeLabs.NetClaw.Dashboard.Models;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Dashboard.Services;

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
            roots.Add(BuildTreeEntry(new DirectoryInfo(groupDir), "groups", "groups", groupDir));
        }

        string workspaceDir = Path.Combine(dataDirectory, "agent-workspaces", groupFolder.Value);
        string fullWorkspaceDir = Path.GetFullPath(workspaceDir);
        ValidateWithinBase(dataDirectory, fullWorkspaceDir);
        if (Directory.Exists(fullWorkspaceDir))
        {
            roots.Add(BuildTreeEntry(new DirectoryInfo(fullWorkspaceDir), "workspace", "workspace", fullWorkspaceDir));
        }

        string sessionDir = Path.Combine(dataDirectory, "sessions", groupFolder.Value);
        string fullSessionDir = Path.GetFullPath(sessionDir);
        ValidateWithinBase(dataDirectory, fullSessionDir);
        if (Directory.Exists(fullSessionDir))
        {
            roots.Add(BuildTreeEntry(new DirectoryInfo(fullSessionDir), "sessions", "sessions", fullSessionDir));
        }

        return roots;
    }

    public string? ResolveRawFilePath(GroupFolder groupFolder, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string fullPath = ResolveFilePath(groupFolder, relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        RejectReparsePoints(fullPath, groupsDirectory, dataDirectory);
        return fullPath;
    }

    private static void RejectReparsePoints(string fullPath, params string[] stopDirectories)
    {
        string? current = fullPath;
        while (!string.IsNullOrEmpty(current))
        {
            if (stopDirectories.Any(d => string.Equals(current, d, StringComparison.Ordinal)))
            {
                break;
            }

            try
            {
                if (File.Exists(current) || Directory.Exists(current))
                {
                    FileAttributes attributes = File.GetAttributes(current);
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        throw new WorkspacePathTraversalException("Symbolic links and reparse points are not allowed.");
                    }
                }
            }
            catch (WorkspacePathTraversalException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw new WorkspacePathTraversalException("Cannot verify path safety — access denied.");
            }
            catch (IOException)
            {
                throw new WorkspacePathTraversalException("Cannot verify path safety — I/O error.");
            }

            current = Path.GetDirectoryName(current);
        }
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

        if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new WorkspacePathTraversalException("Symbolic links and reparse points are not allowed.");
        }

        if (fileInfo.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"File exceeds maximum size of {MaxFileSizeBytes} bytes.");
        }

        try
        {
            string content = File.ReadAllText(fullPath);
            return new WorkspaceFileDto(relativePath, content, fileInfo.Length, new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException("Access to the file was denied.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Could not read the file.", ex);
        }
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

        // Strip root prefix (e.g. "groups/...", "workspace/...", "sessions/...") and resolve against the matching base.
        (string baseDir, string innerPath) = StripRootPrefix(relativePath, groupDir, workspaceDir, sessionDir);

        string fullPath = Path.GetFullPath(Path.Combine(baseDir, innerPath));
        if (IsWithinBase(baseDir, fullPath))
        {
            return fullPath;
        }

        throw new WorkspacePathTraversalException("Path escapes allowed workspace directories.");
    }

    private static (string BaseDir, string InnerPath) StripRootPrefix(string relativePath, string groupDir, string workspaceDir, string sessionDir)
    {
        if (relativePath.StartsWith("groups/", StringComparison.Ordinal))
        {
            return (groupDir, relativePath["groups/".Length..]);
        }

        if (relativePath.StartsWith("workspace/", StringComparison.Ordinal))
        {
            return (workspaceDir, relativePath["workspace/".Length..]);
        }

        if (relativePath.StartsWith("sessions/", StringComparison.Ordinal))
        {
            return (sessionDir, relativePath["sessions/".Length..]);
        }

        // Legacy/fallback: try each base directory in order.
        return (groupDir, relativePath);
    }

    private static void ValidateWithinBase(string baseDirectory, string fullPath)
    {
        if (!IsWithinBase(baseDirectory, fullPath))
        {
            throw new WorkspacePathTraversalException($"Path escapes base directory: {fullPath}");
        }
    }

    private static bool IsWithinBase(string baseDirectory, string fullPath)
    {
        string relativePath = Path.GetRelativePath(baseDirectory, fullPath);
        return !relativePath.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathFullyQualified(relativePath);
    }

    private static WorkspaceTreeEntryDto BuildTreeEntry(DirectoryInfo directory, string rootLabel, string rootPrefix, string rootPath)
    {
        return new WorkspaceTreeEntryDto(
            rootLabel,
            rootPrefix,
            IsDirectory: true,
            SizeBytes: null,
            LastModified: new DateTimeOffset(directory.LastWriteTimeUtc, TimeSpan.Zero),
            Children: BuildChildren(directory, rootPrefix, rootPath));
    }

    private static IReadOnlyList<WorkspaceTreeEntryDto> BuildChildren(DirectoryInfo directory, string rootPrefix, string rootPath)
    {
        List<WorkspaceTreeEntryDto> items = [];

        try
        {
            foreach (DirectoryInfo subDir in directory.EnumerateDirectories().OrderBy(d => d.Name))
            {
                if (subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                string childRelative = Path.GetRelativePath(rootPath, subDir.FullName);
                items.Add(new WorkspaceTreeEntryDto(
                    subDir.Name,
                    $"{rootPrefix}/{childRelative}",
                    IsDirectory: true,
                    SizeBytes: null,
                    LastModified: new DateTimeOffset(subDir.LastWriteTimeUtc, TimeSpan.Zero),
                    Children: BuildChildren(subDir, rootPrefix, rootPath)));
            }

            foreach (FileInfo file in directory.EnumerateFiles().OrderBy(f => f.Name))
            {
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                string childRelative = Path.GetRelativePath(rootPath, file.FullName);
                items.Add(new WorkspaceTreeEntryDto(
                    file.Name,
                    $"{rootPrefix}/{childRelative}",
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
