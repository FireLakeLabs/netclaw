using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FireLakeLabs.NetClaw.Host.Services;

public sealed class IpcWatcherWorker : BackgroundService
{
    private readonly IIpcCommandWatcher commandWatcher;
    private readonly ILogger<IpcWatcherWorker> logger;
    private readonly IpcWatcherOptions options;

    public IpcWatcherWorker(IIpcCommandWatcher commandWatcher, IpcWatcherOptions options, ILogger<IpcWatcherWorker> logger)
    {
        this.commandWatcher = commandWatcher;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(options.PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await commandWatcher.PollOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "IPC watcher iteration failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
