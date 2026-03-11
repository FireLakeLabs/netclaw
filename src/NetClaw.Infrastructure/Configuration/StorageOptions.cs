namespace NetClaw.Infrastructure.Configuration;

public sealed record StorageOptions
{
    public required string ProjectRoot { get; init; }

    public required string StoreDirectory { get; init; }

    public required string GroupsDirectory { get; init; }

    public required string DataDirectory { get; init; }

    public static StorageOptions Create(string projectRoot)
    {
        string normalizedRoot = Path.GetFullPath(projectRoot);

        return new StorageOptions
        {
            ProjectRoot = normalizedRoot,
            StoreDirectory = Path.Combine(normalizedRoot, "store"),
            GroupsDirectory = Path.Combine(normalizedRoot, "groups"),
            DataDirectory = Path.Combine(normalizedRoot, "data")
        };
    }

    public void Validate()
    {
        string[] paths = [ProjectRoot, StoreDirectory, GroupsDirectory, DataDirectory];
        if (paths.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("All storage paths are required.");
        }

        if (!Path.IsPathFullyQualified(ProjectRoot))
        {
            throw new InvalidOperationException("Project root must be an absolute path.");
        }
    }
}
