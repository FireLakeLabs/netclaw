using FireLakeLabs.NetClaw.Domain.Contracts.Channels;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Channels;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Channels;

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
        await channel.ReadLoopCompletion!;

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
        Assert.StartsWith("you> ", output.ToString(), StringComparison.Ordinal);

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task SendMessageAsync_WritesToOutput()
    {
        BlockingTextReader input = new();
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
        await channel.ReadyTask;
        await channel.SendMessageAsync(new ChatJid("team@jid"), "assistant reply");

        string expected = $"you> \rbot> assistant reply{Environment.NewLine}you> ";
        Assert.Equal(expected, output.ToString());

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task PollInboundAsync_DoesNotWritePromptWhenDisabled()
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
                IsGroup = true,
                InputPrompt = string.Empty
            },
            input,
            output);

        await channel.ConnectAsync();
        await channel.ReadLoopCompletion!;
        await channel.PollInboundAsync((_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);

        Assert.DoesNotContain("you> ", output.ToString(), StringComparison.Ordinal);

        await channel.DisconnectAsync();
    }

    private sealed class BlockingTextReader : TextReader
    {
        private readonly TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return new ValueTask<string?>(completion.Task);
        }
    }
}
