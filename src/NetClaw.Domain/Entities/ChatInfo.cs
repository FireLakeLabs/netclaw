using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Entities;

public sealed record ChatInfo
{
    public ChatInfo(ChatJid jid, string name, DateTimeOffset lastMessageTime, ChannelName channel, bool isGroup)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Chat name is required.", nameof(name));
        }

        Jid = jid;
        Name = name.Trim();
        LastMessageTime = lastMessageTime;
        Channel = channel;
        IsGroup = isGroup;
    }

    public ChatJid Jid { get; }

    public string Name { get; }

    public DateTimeOffset LastMessageTime { get; }

    public ChannelName Channel { get; }

    public bool IsGroup { get; }
}
