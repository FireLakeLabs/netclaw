namespace NetClaw.Infrastructure.Configuration;

public sealed record CredentialProxyOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 3001;

    public string CopilotUpstreamUrl { get; init; } = "https://api.githubcopilot.com";

    public string ClaudeUpstreamUrl { get; init; } = "https://api.anthropic.com";

    public string AuthMode { get; init; } = "api-key";

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

        if (string.IsNullOrWhiteSpace(CopilotUpstreamUrl))
        {
            throw new InvalidOperationException("Copilot upstream URL is required.");
        }

        if (string.IsNullOrWhiteSpace(ClaudeUpstreamUrl))
        {
            throw new InvalidOperationException("Claude upstream URL is required.");
        }

        string normalizedMode = AuthMode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("api-key" or "oauth"))
        {
            throw new InvalidOperationException($"Unsupported auth mode '{AuthMode}'. Must be 'api-key' or 'oauth'.");
        }
    }
}
