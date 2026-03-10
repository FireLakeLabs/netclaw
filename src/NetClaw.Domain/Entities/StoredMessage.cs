using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Entities;

public sealed record StoredMessage
{
    public StoredMessage(string id, ChatJid chatJid, string sender, string senderName, string content, DateTimeOffset timestamp, bool isFromMe = false, bool isBotMessage = false)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Message ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(sender))
        {
            throw new ArgumentException("Sender is required.", nameof(sender));
        }

        if (string.IsNullOrWhiteSpace(senderName))
        {
            throw new ArgumentException("Sender name is required.", nameof(senderName));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content is required.", nameof(content));
        }

        Id = id.Trim();
        ChatJid = chatJid;
        Sender = sender.Trim();
        SenderName = senderName.Trim();
        Content = content;
        Timestamp = timestamp;
        IsFromMe = isFromMe;
        IsBotMessage = isBotMessage;
    }

    public string Id { get; }

    public ChatJid ChatJid { get; }

    public string Sender { get; }

    public string SenderName { get; }

    public string Content { get; }

    public DateTimeOffset Timestamp { get; }

    public bool IsFromMe { get; }

    public bool IsBotMessage { get; }
}