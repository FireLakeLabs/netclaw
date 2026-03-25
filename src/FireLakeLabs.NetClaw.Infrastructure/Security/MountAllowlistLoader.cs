using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Security;

public sealed class MountAllowlistLoader
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IFileSystem fileSystem;

    public MountAllowlistLoader(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public async Task<MountAllowlist> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!fileSystem.FileExists(path))
        {
            return new MountAllowlist([], [], nonMainReadOnly: true);
        }

        string json = await fileSystem.ReadAllTextAsync(path, cancellationToken);
        MountAllowlistDocument? document = JsonSerializer.Deserialize<MountAllowlistDocument>(json, SerializerOptions);

        if (document is null)
        {
            throw new InvalidOperationException("Mount allowlist could not be deserialized.");
        }

        IReadOnlyList<AllowedRoot> allowedRoots = document.AllowedRoots?
            .Select(root => new AllowedRoot(root.Path, root.AllowReadWrite, root.Description))
            .ToArray() ?? [];
        IReadOnlyList<string> blockedPatterns = document.BlockedPatterns ?? [];

        return new MountAllowlist(allowedRoots, blockedPatterns, document.NonMainReadOnly);
    }

    private sealed record MountAllowlistDocument(AllowedRootDocument[]? AllowedRoots, string[]? BlockedPatterns, bool NonMainReadOnly = true);

    private sealed record AllowedRootDocument(string Path, bool AllowReadWrite, string? Description);
}
