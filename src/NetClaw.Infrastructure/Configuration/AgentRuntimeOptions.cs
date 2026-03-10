using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Configuration;

public sealed record AgentRuntimeOptions
{
    public string DefaultProvider { get; init; } = "copilot";

    public bool KeepContainerBoundary { get; init; } = true;

    public string CopilotCliPath { get; init; } = "copilot";

    public void Validate()
    {
        _ = GetDefaultProvider();

        if (string.IsNullOrWhiteSpace(CopilotCliPath))
        {
            throw new InvalidOperationException("Copilot CLI path is required.");
        }
    }

    public AgentProviderKind GetDefaultProvider()
    {
        return DefaultProvider.Trim().ToLowerInvariant() switch
        {
            "copilot" => AgentProviderKind.Copilot,
            "claudecode" or "claude-code" or "claude" => AgentProviderKind.ClaudeCode,
            "codex" => AgentProviderKind.Codex,
            "opencode" or "open-code" => AgentProviderKind.OpenCode,
            _ => throw new InvalidOperationException($"Unsupported agent provider '{DefaultProvider}'.")
        };
    }
}