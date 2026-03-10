using System.Collections.Concurrent;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Channels;

public sealed class TerminalChannel : IInboundChannel
{
    private readonly object gate = new();
    private readonly TextReader input;
    private readonly TerminalChannelOptions options;
    private readonly TextWriter output;
    private readonly ConcurrentQueue<StoredMessage> pendingMessages = new();
    private CancellationTokenSource? readLoopCancellation;
    private Task? readLoopTask;
    private bool isConnected;
    private bool metadataPending;
    private int messageSequence;

    public TerminalChannel(TerminalChannelOptions options)
        : this(options, Console.In, Console.Out)
    {
    }

    public TerminalChannel(TerminalChannelOptions options, TextReader input, TextWriter output)
    {
        this.options = options;
        this.input = input;
        this.output = output;
    }

    public ChannelName Name => new("terminal");

    public bool IsConnected
    {
        get
        {
            lock (gate)
            {
                return isConnected;
            }
        }
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (isConnected)
            {
                return Task.CompletedTask;
            }

            isConnected = true;
            metadataPending = true;
            readLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readLoopTask = Task.Run(() => ReadLoopAsync(readLoopCancellation.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Task? readTask;
        CancellationTokenSource? cancellationSource;

        lock (gate)
        {
            if (!isConnected)
            {
                return;
            }

            isConnected = false;
            readTask = readLoopTask;
            cancellationSource = readLoopCancellation;
            readLoopTask = null;
            readLoopCancellation = null;
        }

        cancellationSource?.Cancel();
        if (readTask is not null)
        {
            try
            {
                await readTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellationSource?.Dispose();
    }

    public bool Owns(ChatJid chatJid)
    {
        return string.Equals(chatJid.Value, options.ChatJid, StringComparison.Ordinal);
    }

    public async Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Terminal channel is not connected.");
        }

        await output.WriteLineAsync($"{options.OutboundPrefix}{text}");
        await output.FlushAsync(cancellationToken);
    }

    public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task PollInboundAsync(
        Func<ChannelMessage, CancellationToken, Task> onMessage,
        Func<ChannelMetadataEvent, CancellationToken, Task> onMetadata,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return;
        }

        if (metadataPending)
        {
            metadataPending = false;
            await onMetadata(
                new ChannelMetadataEvent(
                    new ChatJid(options.ChatJid),
                    DateTimeOffset.UtcNow,
                    options.ChatName,
                    Name,
                    options.IsGroup),
                cancellationToken);
        }

        while (pendingMessages.TryDequeue(out StoredMessage? message))
        {
            await onMessage(new ChannelMessage(message.ChatJid, message), cancellationToken);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await input.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int sequence = Interlocked.Increment(ref messageSequence);
            pendingMessages.Enqueue(new StoredMessage(
                $"terminal-{sequence:D8}",
                new ChatJid(options.ChatJid),
                options.Sender,
                options.SenderName,
                line,
                DateTimeOffset.UtcNow));
        }
    }
}