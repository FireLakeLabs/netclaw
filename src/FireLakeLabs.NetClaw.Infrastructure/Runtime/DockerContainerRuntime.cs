using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;

namespace FireLakeLabs.NetClaw.Infrastructure.Runtime;

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
        if (IsPodman())
        {
            return [];
        }

        if (platformInfo.Kind == PlatformKind.Linux && !platformInfo.IsWsl)
        {
            string hostGatewayName = string.IsNullOrWhiteSpace(options.HostGatewayName)
                ? "host.docker.internal"
                : options.HostGatewayName;

            return [$"--add-host={hostGatewayName}:host-gateway"];
        }

        return [];
    }

    public async Task StopContainerAsync(ContainerName containerName, CancellationToken cancellationToken = default)
    {
        await commandRunner.RunAsync(options.RuntimeBinary, $"stop {containerName.Value}", cancellationToken);
    }

    private bool IsPodman()
    {
        string runtimeName = Path.GetFileName(options.RuntimeBinary);
        return runtimeName.Equals("podman", StringComparison.OrdinalIgnoreCase);
    }
}
