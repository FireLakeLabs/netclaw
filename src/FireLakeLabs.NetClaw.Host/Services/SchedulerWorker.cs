using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FireLakeLabs.NetClaw.Host.Services;

public sealed class SchedulerWorker : BackgroundService
{
    private readonly ILogger<SchedulerWorker> logger;
    private readonly SchedulerOptions options;
    private readonly ITaskSchedulerService schedulerService;

    public SchedulerWorker(ITaskSchedulerService schedulerService, SchedulerOptions options, ILogger<SchedulerWorker> logger)
    {
        this.schedulerService = schedulerService;
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
                    await schedulerService.RunDueTasksAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Task scheduler iteration failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
