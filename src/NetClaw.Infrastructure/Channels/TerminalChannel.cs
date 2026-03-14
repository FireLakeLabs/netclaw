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
    private readonly SemaphoreSlim outputLock = new(1, 1);
    private readonly ConcurrentQueue<StoredMessage> pendingMessages = new();
    private readonly TaskCompletionSource readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? readLoopCancellation;
    private Task? readLoopTask;
    private bool isConnected;
    private bool metadataPending;
    private bool promptVisible;
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

    internal Task ReadyTask => readySignal.Task;

    internal Task? ReadLoopCompletion => readLoopTask;

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

        await WriteAssistantMessageAsync(text, cancellationToken);
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
            await WritePromptAsync(cancellationToken);
            readySignal.TrySetResult();

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
                promptVisible = false;
                break;
            }

            promptVisible = false;

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

    private Task WritePromptAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(options.InputPrompt))
        {
            return Task.CompletedTask;
        }

        promptVisible = true;
        return WriteOutputAsync(options.InputPrompt, appendNewLine: false, cancellationToken);
    }

    private async Task WriteOutputAsync(string text, bool appendNewLine, CancellationToken cancellationToken)
    {
        await outputLock.WaitAsync(cancellationToken);
        try
        {
            if (appendNewLine)
            {
                await output.WriteLineAsync(text);
            }
            else
            {
                await output.WriteAsync(text);
            }

            await output.FlushAsync(cancellationToken);
        }
        finally
        {
            outputLock.Release();
        }
    }

    private async Task WriteAssistantMessageAsync(string text, CancellationToken cancellationToken)
    {
        string renderedText = $"{options.OutboundPrefix}{text}";

        await outputLock.WaitAsync(cancellationToken);
        try
        {
            if (promptVisible)
            {
                await output.WriteAsync($"\r{renderedText}{Environment.NewLine}");

                if (!string.IsNullOrEmpty(options.InputPrompt))
                {
                    await output.WriteAsync(options.InputPrompt);
                    promptVisible = true;
                }
                else
                {
                    promptVisible = false;
                }
            }
            else
            {
                await output.WriteLineAsync(renderedText);
                promptVisible = false;
            }

            await output.FlushAsync(cancellationToken);
        }
        finally
        {
            outputLock.Release();
        }
    }
}
