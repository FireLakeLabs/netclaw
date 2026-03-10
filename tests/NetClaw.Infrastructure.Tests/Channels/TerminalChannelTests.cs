using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Channels;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Tests.Channels;

public sealed class TerminalChannelTests
{
    [Fact]
    public async Task PollInboundAsync_EmitsConfiguredMetadataAndConsoleInput()
    {
        StringReader input = new("hello terminal\n");
        StringWriter output = new();
        TerminalChannel channel = new(
            new TerminalChannelOptions
            {
                Enabled = true,
                ChatJid = "team@jid",
                Sender = "sender-1",
                SenderName = "User",
                ChatName = "Terminal Chat",
                IsGroup = true
            },
            input,
            output);

        await channel.ConnectAsync();
        await Task.Delay(50);

        List<ChannelMetadataEvent> metadata = [];
        List<ChannelMessage> messages = [];
        await channel.PollInboundAsync(
            (message, _) =>
            {
                messages.Add(message);
                return Task.CompletedTask;
            },
            (eventData, _) =>
            {
                metadata.Add(eventData);
                return Task.CompletedTask;
            });

        Assert.Single(metadata);
        Assert.Equal("Terminal Chat", metadata[0].Name);
        Assert.Single(messages);
        Assert.Equal("hello terminal", messages[0].Message.Content);
        Assert.True(channel.Owns(new ChatJid("team@jid")));

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task SendMessageAsync_WritesToOutput()
    {
        StringReader input = new(string.Empty);
        StringWriter output = new();
        TerminalChannel channel = new(
            new TerminalChannelOptions
            {
                Enabled = true,
                ChatJid = "team@jid",
                OutboundPrefix = "bot> "
            },
            input,
            output);

        await channel.ConnectAsync();
        await channel.SendMessageAsync(new ChatJid("team@jid"), "assistant reply");

        Assert.Contains("bot> assistant reply", output.ToString(), StringComparison.Ordinal);

        await channel.DisconnectAsync();
    }
}