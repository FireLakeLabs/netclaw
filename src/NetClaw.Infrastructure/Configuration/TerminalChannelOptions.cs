namespace NetClaw.Infrastructure.Configuration;

public sealed record TerminalChannelOptions
{
    public bool Enabled { get; init; }

    public string ChatJid { get; init; } = "terminal@local";

    public string Sender { get; init; } = "terminal-user";

    public string SenderName { get; init; } = "Terminal User";

    public string ChatName { get; init; } = "Terminal";

    public bool IsGroup { get; init; }

    public string OutboundPrefix { get; init; } = "assistant> ";

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ChatJid))
        {
            throw new InvalidOperationException("Terminal channel chat JID is required when enabled.");
        }

        if (string.IsNullOrWhiteSpace(Sender))
        {
            throw new InvalidOperationException("Terminal channel sender is required when enabled.");
        }

        if (string.IsNullOrWhiteSpace(SenderName))
        {
            throw new InvalidOperationException("Terminal channel sender name is required when enabled.");
        }
    }
}