using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Runtime;

public sealed class DockerContainerRuntime : IContainerRuntime
{
    private readonly ICommandRunner commandRunner;
    private readonly ContainerRuntimeOptions options;
    private readonly PlatformInfo platformInfo;

    public DockerContainerRuntime(ICommandRunner commandRunner, ContainerRuntimeOptions options, PlatformInfo platformInfo)
    {
        this.commandRunner = commandRunner;
        this.options = options;
        this.platformInfo = platformInfo;
    }

    public async Task CleanupOrphansAsync(CancellationToken cancellationToken = default)
    {
        CommandResult result = await commandRunner.RunAsync(options.RuntimeBinary, "ps --filter name=netclaw- --format {{.Names}}", cancellationToken);
        if (!result.Succeeded)
        {
            return;
        }

        string[] containerNames = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string containerName in containerNames)
        {
            await StopContainerAsync(new ContainerName(containerName), cancellationToken);
        }
    }

    public async Task EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        CommandResult result = await commandRunner.RunAsync(options.RuntimeBinary, "info", cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Container runtime is required but was not reachable.");
        }
    }

    public IReadOnlyList<string> GetHostGatewayArguments()
    {
        return platformInfo.Kind == PlatformKind.Linux && !platformInfo.IsWsl
            ? ["--add-host=host.docker.internal:host-gateway"]
            : [];
    }

    public async Task StopContainerAsync(ContainerName containerName, CancellationToken cancellationToken = default)
    {
        await commandRunner.RunAsync(options.RuntimeBinary, $"stop {containerName.Value}", cancellationToken);
    }
}
