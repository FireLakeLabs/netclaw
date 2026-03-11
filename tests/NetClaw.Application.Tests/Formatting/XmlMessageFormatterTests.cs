using NetClaw.Application.Formatting;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Tests.Formatting;

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
    public void NormalizeOutbound_StripsInternalTags()
    {
        XmlMessageFormatter formatter = new();

        string normalized = formatter.NormalizeOutbound("hello <internal>secret</internal> world");

        Assert.Equal("hello  world", normalized);
    }
}
