using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Tests.ValueObjects;

public sealed class SimpleIdentifierTests
{
    [Fact]
    public void ChatJid_TrimsWhitespace()
    {
        ChatJid chatJid = new(" 123@jid ");

        Assert.Equal("123@jid", chatJid.Value);
    }

    [Fact]
    public void TaskId_RejectsBlankValues()
    {
        Assert.Throws<ArgumentException>(() => new TaskId(" "));
    }

    [Fact]
    public void SessionId_RejectsBlankValues()
    {
        Assert.Throws<ArgumentException>(() => new SessionId(string.Empty));
    }

    [Fact]
    public void ContainerName_RejectsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new ContainerName("container name"));
    }

    [Fact]
    public void ChannelName_NormalizesCase()
    {
        ChannelName channelName = new(" WhatsApp ");

        Assert.Equal("whatsapp", channelName.Value);
    }
}
