namespace FireLakeLabs.NetClaw.Infrastructure.Configuration;

public sealed record SlackChannelOptions
{
    public bool Enabled { get; init; }

    public string BotToken { get; init; } = string.Empty;

    public string AppToken { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://slack.com/api";

    public string MentionReplacement { get; init; } = "@Andy";

    public string WorkingIndicatorText { get; init; } = "Evaluating...";

    public bool ReplyInThreadByDefault { get; init; } = true;

    public long MaxFileDownloadBytes { get; init; } = 50 * 1024 * 1024;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(BotToken))
        {
            throw new InvalidOperationException("Slack channel bot token is required when enabled.");
        }

        if (string.IsNullOrWhiteSpace(AppToken))
        {
            throw new InvalidOperationException("Slack channel app token is required when enabled.");
        }

        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            throw new InvalidOperationException("Slack channel API base URL is required when enabled.");
        }

        if (MentionReplacement is null)
        {
            throw new InvalidOperationException("Slack channel mention replacement cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(WorkingIndicatorText))
        {
            throw new InvalidOperationException("Slack channel working indicator text is required when enabled.");
        }
    }
}
