using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;

namespace FireLakeLabs.NetClaw.Host.Services;

public sealed class CredentialProxyWorker : IHostedService
{
    private readonly ICredentialProxyService proxyService;
    private readonly ILogger<CredentialProxyWorker> logger;

    public CredentialProxyWorker(ICredentialProxyService proxyService, ILogger<CredentialProxyWorker> logger)
    {
        this.proxyService = proxyService;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting credential proxy on {BindAddress}", proxyService.BindAddress);
        await proxyService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping credential proxy.");
        await proxyService.StopAsync(cancellationToken);
    }
}
