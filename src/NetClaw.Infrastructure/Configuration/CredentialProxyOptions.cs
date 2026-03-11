namespace NetClaw.Infrastructure.Configuration;

public sealed record CredentialProxyOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 3001;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Credential proxy host is required.");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException("Credential proxy port must be between 1 and 65535.");
        }
    }
}
