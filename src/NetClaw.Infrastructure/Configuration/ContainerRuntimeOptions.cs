namespace NetClaw.Infrastructure.Configuration;

public sealed record ContainerRuntimeOptions
{
    public string RuntimeBinary { get; init; } = "docker";

    public string HostGatewayName { get; init; } = "host.docker.internal";

    public string ProxyBindHostOverride { get; init; } = string.Empty;

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
    }
}