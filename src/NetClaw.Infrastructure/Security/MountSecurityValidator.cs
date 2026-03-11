using NetClaw.Domain.Entities;

namespace NetClaw.Infrastructure.Security;

public sealed class MountSecurityValidator
{
    public IReadOnlyList<AdditionalMount> Validate(IReadOnlyList<AdditionalMount> requestedMounts, MountAllowlist allowlist, bool isMainGroup)
    {
        List<AdditionalMount> validatedMounts = [];

        foreach (AdditionalMount requestedMount in requestedMounts)
        {
            string normalizedHostPath = Path.GetFullPath(ExpandHome(requestedMount.HostPath));
            AllowedRoot? allowedRoot = allowlist.AllowedRoots.FirstOrDefault(root => IsChildPath(Path.GetFullPath(ExpandHome(root.Path)), normalizedHostPath));
            if (allowedRoot is null)
            {
                throw new InvalidOperationException($"Requested mount path is not allowlisted: {requestedMount.HostPath}");
            }

            if (allowlist.BlockedPatterns.Any(pattern => normalizedHostPath.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Requested mount path matches a blocked pattern: {requestedMount.HostPath}");
            }

            bool isReadOnly = requestedMount.IsReadOnly || (!isMainGroup && allowlist.NonMainReadOnly) || !allowedRoot.AllowReadWrite;
            validatedMounts.Add(new AdditionalMount(requestedMount.HostPath, requestedMount.ContainerPath, isReadOnly));
        }

        return validatedMounts;
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;
    }

    private static bool IsChildPath(string parentPath, string candidatePath)
    {
        string relativePath = Path.GetRelativePath(parentPath, candidatePath);
        return !relativePath.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathFullyQualified(relativePath);
    }
}
