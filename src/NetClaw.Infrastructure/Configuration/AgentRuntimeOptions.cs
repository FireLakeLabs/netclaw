using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Configuration;

public sealed record AgentRuntimeOptions
{
    public string DefaultProvider { get; init; } = "copilot";

    public bool KeepContainerBoundary { get; init; } = true;

    public string CopilotCliPath { get; init; } = "copilot";

    public string CopilotConfigDirectory { get; init; } = string.Empty;

    public string? CopilotCliUrl { get; init; }

    public string CopilotLogLevel { get; init; } = "info";

    public bool CopilotUseStdio { get; init; } = true;

    public bool CopilotAutoStart { get; init; } = true;

    public bool CopilotAutoRestart { get; init; } = true;

    public string? CopilotGitHubToken { get; init; }

    public bool? CopilotUseLoggedInUser { get; init; }

    public string CopilotClientName { get; init; } = "NetClaw";

    public string CopilotModel { get; init; } = "gpt-5";

    public string? CopilotReasoningEffort { get; init; }

    public bool CopilotStreaming { get; init; } = true;

    public bool CopilotEnableInfiniteSessions { get; init; } = true;

    public double? CopilotBackgroundCompactionThreshold { get; init; }

    public double? CopilotBufferExhaustionThreshold { get; init; }

    public void Validate()
    {
        _ = GetDefaultProvider();

        if (string.IsNullOrWhiteSpace(CopilotCliPath))
        {
            throw new InvalidOperationException("Copilot CLI path is required.");
        }

        if (string.IsNullOrWhiteSpace(CopilotConfigDirectory))
        {
            throw new InvalidOperationException("Copilot config directory is required.");
        }

        if (string.IsNullOrWhiteSpace(CopilotLogLevel))
        {
            throw new InvalidOperationException("Copilot log level is required.");
        }

        if (string.IsNullOrWhiteSpace(CopilotClientName))
        {
            throw new InvalidOperationException("Copilot client name is required.");
        }

        if (string.IsNullOrWhiteSpace(CopilotModel))
        {
            throw new InvalidOperationException("Copilot model is required.");
        }

        if (!string.IsNullOrWhiteSpace(CopilotCliUrl)
            && (!string.IsNullOrWhiteSpace(CopilotGitHubToken) || CopilotUseLoggedInUser is not null))
        {
            throw new InvalidOperationException("Copilot CLI URL cannot be combined with explicit auth options.");
        }

        ValidateThreshold(CopilotBackgroundCompactionThreshold, nameof(CopilotBackgroundCompactionThreshold));
        ValidateThreshold(CopilotBufferExhaustionThreshold, nameof(CopilotBufferExhaustionThreshold));

        if (CopilotBackgroundCompactionThreshold is { } background
            && CopilotBufferExhaustionThreshold is { } buffer
            && background >= buffer)
        {
            throw new InvalidOperationException("Copilot background compaction threshold must be lower than the buffer exhaustion threshold.");
        }

        if (!string.IsNullOrWhiteSpace(CopilotReasoningEffort))
        {
            string normalizedReasoningEffort = CopilotReasoningEffort.Trim().ToLowerInvariant();
            if (normalizedReasoningEffort is not ("low" or "medium" or "high" or "xhigh"))
            {
                throw new InvalidOperationException($"Unsupported Copilot reasoning effort '{CopilotReasoningEffort}'.");
            }
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

    private static void ValidateThreshold(double? threshold, string propertyName)
    {
        if (threshold is null)
        {
            return;
        }

        if (threshold <= 0d || threshold >= 1d)
        {
            throw new InvalidOperationException($"{propertyName} must be between 0 and 1.");
        }
    }
}