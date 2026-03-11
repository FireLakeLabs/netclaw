using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetClaw.Application.Execution;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Host.Services;

public sealed class MessageLoopWorker : BackgroundService
{
    private readonly ILogger<MessageLoopWorker> logger;
    private readonly InboundMessagePollingService pollingService;
    private readonly MessageLoopOptions options;

    public MessageLoopWorker(InboundMessagePollingService pollingService, MessageLoopOptions options, ILogger<MessageLoopWorker> logger)
    {
        this.pollingService = pollingService;
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
                    await pollingService.PollOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Message loop iteration failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
