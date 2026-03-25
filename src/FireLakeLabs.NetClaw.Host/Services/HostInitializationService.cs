using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FireLakeLabs.NetClaw.Host.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;
using FireLakeLabs.NetClaw.Infrastructure.Security;

namespace FireLakeLabs.NetClaw.Host.Services;

public sealed class HostInitializationService : IHostedService
{
    private readonly IFileSystem fileSystem;
    private readonly ILogger<HostInitializationService> logger;
    private readonly MountAllowlistLoader mountAllowlistLoader;
    private readonly SenderAllowlistService senderAllowlistService;
    private readonly HostPathOptions hostPathOptions;
    private readonly SqliteSchemaInitializer schemaInitializer;
    private readonly StorageOptions storageOptions;

    public HostInitializationService(
        StorageOptions storageOptions,
        HostPathOptions hostPathOptions,
        IFileSystem fileSystem,
        MountAllowlistLoader mountAllowlistLoader,
        SenderAllowlistService senderAllowlistService,
        SqliteSchemaInitializer schemaInitializer,
        ILogger<HostInitializationService> logger)
    {
        this.storageOptions = storageOptions;
        this.hostPathOptions = hostPathOptions;
        this.fileSystem = fileSystem;
        this.mountAllowlistLoader = mountAllowlistLoader;
        this.senderAllowlistService = senderAllowlistService;
        this.schemaInitializer = schemaInitializer;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(storageOptions.ProjectRoot);
        fileSystem.CreateDirectory(storageOptions.StoreDirectory);
        fileSystem.CreateDirectory(storageOptions.GroupsDirectory);
        fileSystem.CreateDirectory(storageOptions.DataDirectory);
        fileSystem.CreateDirectory(Path.Combine(storageOptions.DataDirectory, "ipc"));

        string? databaseDirectory = Path.GetDirectoryName(hostPathOptions.DatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            fileSystem.CreateDirectory(databaseDirectory);
        }

        RestrictSensitiveDirectories();

        await mountAllowlistLoader.LoadAsync(hostPathOptions.MountAllowlistPath, cancellationToken);
        await senderAllowlistService.LoadAsync(hostPathOptions.SenderAllowlistPath, cancellationToken);
        await schemaInitializer.InitializeAsync(cancellationToken);

        logger.LogInformation("NetClaw host initialized at {ProjectRoot}", storageOptions.ProjectRoot);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void RestrictSensitiveDirectories()
    {
        string ipcDirectory = Path.Combine(storageOptions.DataDirectory, "ipc");
        string sessionsDirectory = Path.Combine(storageOptions.DataDirectory, "sessions");
        string filesDirectory = Path.Combine(storageOptions.DataDirectory, "files");

        // Ensure all sensitive storage locations are restricted to the owner
        DirectoryPermissions.RestrictToOwner(storageOptions.ProjectRoot, logger);
        DirectoryPermissions.RestrictToOwner(storageOptions.StoreDirectory, logger);
        DirectoryPermissions.RestrictToOwner(storageOptions.GroupsDirectory, logger);
        DirectoryPermissions.RestrictToOwner(storageOptions.DataDirectory, logger);
        DirectoryPermissions.RestrictToOwner(ipcDirectory, logger);
        DirectoryPermissions.RestrictToOwner(sessionsDirectory, logger);
        DirectoryPermissions.RestrictToOwner(filesDirectory, logger);

        string? databaseDirectory = Path.GetDirectoryName(hostPathOptions.DatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            DirectoryPermissions.RestrictToOwner(databaseDirectory, logger);
        }
    }
}
