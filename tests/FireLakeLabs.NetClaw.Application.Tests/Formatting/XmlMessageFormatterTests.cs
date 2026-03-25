using FireLakeLabs.NetClaw.Application.Formatting;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Application.Tests.Formatting;

public sealed class XmlMessageFormatterTests
{
    [Fact]
    public void FormatInbound_ProducesEscapedXmlMessages()
    {
        XmlMessageFormatter formatter = new();
        StoredMessage[] messages =
        [
            new("msg-1", new ChatJid("chat@jid"), "sender", "Tom & Jerry", "<hello>", DateTimeOffset.UtcNow)
        ];

        string formatted = formatter.FormatInbound(messages, "UTC");

        Assert.Contains("<context timezone=\"UTC\" />", formatted);
        Assert.Contains("sender=\"Tom &amp; Jerry\"", formatted);
        Assert.Contains("&lt;hello&gt;", formatted);
    }

    [Fact]
    public void FormatInbound_IncludesAttachmentElements()
    {
        XmlMessageFormatter formatter = new();
        FileAttachment attachment = new("file-1", "msg-1", new ChatJid("chat@jid"), "report.pdf", "application/pdf", 2048, "/tmp/report.pdf", DateTimeOffset.UtcNow);
        StoredMessage[] messages =
        [
            new("msg-1", new ChatJid("chat@jid"), "sender", "User", "see attached", DateTimeOffset.UtcNow, attachments: [attachment])
        ];

        string formatted = formatter.FormatInbound(messages, "UTC");

        Assert.Contains("<attachments>", formatted);
        Assert.Contains("name=\"report.pdf\"", formatted);
        Assert.Contains("path=\".uploads/report.pdf\"", formatted);
        Assert.Contains("size=\"2048\"", formatted);
        Assert.Contains("type=\"application/pdf\"", formatted);
    }

    [Fact]
    public void FormatInbound_OmitsTypeAttributeWhenMimeTypeIsNull()
    {
        XmlMessageFormatter formatter = new();
        FileAttachment attachment = new("file-1", "msg-1", new ChatJid("chat@jid"), "data.bin", null, 512, "/tmp/data.bin", DateTimeOffset.UtcNow);
        StoredMessage[] messages =
        [
            new("msg-1", new ChatJid("chat@jid"), "sender", "User", "see attached", DateTimeOffset.UtcNow, attachments: [attachment])
        ];

        string formatted = formatter.FormatInbound(messages, "UTC");

        Assert.Contains("name=\"data.bin\"", formatted);
        Assert.DoesNotContain("type=", formatted);
    }

    [Fact]
    public void NormalizeOutbound_StripsInternalTags()
    {
        XmlMessageFormatter formatter = new();

        string normalized = formatter.NormalizeOutbound("hello <internal>secret</internal> world");

        Assert.Equal("hello  world", normalized);
    }
}
