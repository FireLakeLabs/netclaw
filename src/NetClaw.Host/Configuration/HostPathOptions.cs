using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Host.Configuration;

public sealed record HostPathOptions
{
    public required string ProjectRoot { get; init; }

    public required string DatabasePath { get; init; }

    public required string MountAllowlistPath { get; init; }

    public required string SenderAllowlistPath { get; init; }

    public static string DefaultProjectRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".netclaw");

    public static HostPathOptions Create(IConfiguration configuration, IHostEnvironment environment)
    {
        string? projectRoot = configuration["NetClaw:ProjectRoot"];
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            projectRoot = DefaultProjectRoot;
        }

        StorageOptions storageOptions = StorageOptions.Create(projectRoot);

        string? databasePath = configuration["NetClaw:DatabasePath"];
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = Path.Combine(storageOptions.DataDirectory, "netclaw.db");
        }

        string? mountAllowlistPath = configuration["NetClaw:MountAllowlistPath"];
        if (string.IsNullOrWhiteSpace(mountAllowlistPath))
        {
            mountAllowlistPath = Path.Combine(projectRoot, "mount-allowlist.json");
        }

        string? senderAllowlistPath = configuration["NetClaw:SenderAllowlistPath"];
        if (string.IsNullOrWhiteSpace(senderAllowlistPath))
        {
            senderAllowlistPath = Path.Combine(projectRoot, "sender-allowlist.json");
        }

        HostPathOptions options = new()
        {
            ProjectRoot = Path.GetFullPath(projectRoot),
            DatabasePath = Path.GetFullPath(databasePath),
            MountAllowlistPath = Path.GetFullPath(mountAllowlistPath),
            SenderAllowlistPath = Path.GetFullPath(senderAllowlistPath)
        };

        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectRoot))
        {
            throw new InvalidOperationException("Project root is required.");
        }

        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            throw new InvalidOperationException("Database path is required.");
        }

        if (string.IsNullOrWhiteSpace(MountAllowlistPath))
        {
            throw new InvalidOperationException("Mount allowlist path is required.");
        }

        if (string.IsNullOrWhiteSpace(SenderAllowlistPath))
        {
            throw new InvalidOperationException("Sender allowlist path is required.");
        }
    }
}
