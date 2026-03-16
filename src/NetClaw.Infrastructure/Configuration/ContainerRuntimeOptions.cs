namespace NetClaw.Infrastructure.Configuration;

public sealed record ContainerRuntimeOptions
{
    public string RuntimeBinary { get; init; } = "docker";

    public string HostGatewayName { get; init; } = "host.docker.internal";

    public string ProxyBindHostOverride { get; init; } = string.Empty;

    public string ImageName { get; init; } = "netclaw-agent:latest";

    public TimeSpan ExecutionTimeout { get; init; } = TimeSpan.FromMinutes(10);

    public int MaxOutputBytes { get; init; } = 10 * 1024 * 1024;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RuntimeBinary))
        {
            throw new InvalidOperationException("Container runtime binary is required.");
        }

        if (string.IsNullOrWhiteSpace(HostGatewayName))
        {
            throw new InvalidOperationException("Host gateway name is required.");
        }

        if (string.IsNullOrWhiteSpace(ImageName))
        {
            throw new InvalidOperationException("Container image name is required.");
        }

        if (ExecutionTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Execution timeout must be positive.");
        }

        if (MaxOutputBytes <= 0)
        {
            throw new InvalidOperationException("Max output bytes must be positive.");
        }
    }
}
