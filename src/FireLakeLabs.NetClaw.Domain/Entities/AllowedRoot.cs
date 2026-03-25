namespace FireLakeLabs.NetClaw.Domain.Entities;

public sealed record AllowedRoot
{
    public AllowedRoot(string path, bool allowReadWrite, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Allowed root path is required.", nameof(path));
        }

        Path = path.Trim();
        AllowReadWrite = allowReadWrite;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public string Path { get; }

    public bool AllowReadWrite { get; }

    public string? Description { get; }
}
