using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetClaw.Application.Channels;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Host.Services;

public sealed class ChannelWorker : BackgroundService
{
    private readonly IReadOnlyList<IChannel> channels;
    private readonly ChannelIngressService channelIngressService;
    private readonly ILogger<ChannelWorker> logger;
    private readonly ChannelWorkerOptions options;

    public ChannelWorker(
        IReadOnlyList<IChannel> channels,
        ChannelIngressService channelIngressService,
        ChannelWorkerOptions options,
        ILogger<ChannelWorker> logger)
    {
        this.channels = channels;
        this.channelIngressService = channelIngressService;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool forceSync = options.InitialSyncOnStart;

        foreach (IChannel channel in channels)
        {
            try
            {
                await channel.ConnectAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to connect channel {ChannelName}.", channel.Name.Value);
            }
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (IChannel channel in channels)
                {
                    try
                    {
                        await channel.SyncGroupsAsync(forceSync, stoppingToken);

                        if (channel is IInboundChannel inboundChannel)
                        {
                            await inboundChannel.PollInboundAsync(
                                (message, cancellationToken) => channelIngressService.HandleMessageAsync(channel.Name, message, cancellationToken),
                                (metadata, cancellationToken) => channelIngressService.HandleMetadataAsync(
                                    metadata.Channel is null ? metadata with { Channel = channel.Name } : metadata,
                                    cancellationToken),
                                stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Channel loop failed for {ChannelName}.", channel.Name.Value);
                    }
                }

                forceSync = false;
                await Task.Delay(options.PollInterval, stoppingToken);
            }
        }
        finally
        {
            foreach (IChannel channel in channels)
            {
                try
                {
                    await channel.DisconnectAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to disconnect channel {ChannelName}.", channel.Name.Value);
                }
            }
        }
    }
}