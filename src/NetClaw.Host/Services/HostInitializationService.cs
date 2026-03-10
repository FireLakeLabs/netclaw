using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetClaw.Host.Configuration;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;
using NetClaw.Infrastructure.Persistence.Sqlite;
using NetClaw.Infrastructure.Security;

namespace NetClaw.Host.Services;

public sealed class HostInitializationService : IHostedService
{
    private readonly IFileSystem fileSystem;
    private readonly ILogger<HostInitializationService> logger;
    private readonly MountAllowlistLoader mountAllowlistLoader;
    private readonly HostPathOptions hostPathOptions;
    private readonly SqliteSchemaInitializer schemaInitializer;
    private readonly StorageOptions storageOptions;

    public HostInitializationService(
        StorageOptions storageOptions,
        HostPathOptions hostPathOptions,
        IFileSystem fileSystem,
        MountAllowlistLoader mountAllowlistLoader,
        SqliteSchemaInitializer schemaInitializer,
        ILogger<HostInitializationService> logger)
    {
        this.storageOptions = storageOptions;
        this.hostPathOptions = hostPathOptions;
        this.fileSystem = fileSystem;
        this.mountAllowlistLoader = mountAllowlistLoader;
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

        await mountAllowlistLoader.LoadAsync(hostPathOptions.MountAllowlistPath, cancellationToken);
        await schemaInitializer.InitializeAsync(cancellationToken);

        logger.LogInformation("NetClaw host initialized at {ProjectRoot}", storageOptions.ProjectRoot);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}