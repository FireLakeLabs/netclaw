using System.Collections.Concurrent;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Contracts.Channels;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;

namespace FireLakeLabs.NetClaw.Infrastructure.Channels;

public sealed class ReferenceFileChannel : IInboundChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, byte> ownedChats = new(StringComparer.Ordinal);
    private readonly string errorsDirectory;
    private readonly string inboxDirectory;
    private readonly object gate = new();
    private readonly string outboxDirectory;
    private readonly string processedDirectory;
    private readonly ReferenceFileChannelOptions options;
    private bool isConnected;

    public ReferenceFileChannel(ReferenceFileChannelOptions options)
    {
        this.options = options;
        string rootDirectory = Path.GetFullPath(options.RootDirectory);
        inboxDirectory = Path.Combine(rootDirectory, "inbox");
        outboxDirectory = Path.Combine(rootDirectory, "outbox");
        processedDirectory = Path.Combine(rootDirectory, "processed");
        errorsDirectory = Path.Combine(rootDirectory, "errors");
    }

    public ChannelName Name => new("reference-file");

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
        Directory.CreateDirectory(inboxDirectory);
        Directory.CreateDirectory(outboxDirectory);
        Directory.CreateDirectory(processedDirectory);
        Directory.CreateDirectory(errorsDirectory);

        lock (gate)
        {
            isConnected = true;
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            isConnected = false;
        }

        return Task.CompletedTask;
    }

    public bool Owns(ChatJid chatJid)
    {
        return options.ClaimAllChats || ownedChats.ContainsKey(chatJid.Value);
    }

    public async Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Reference file channel is not connected.");
        }

        ownedChats.TryAdd(chatJid.Value, 0);
        string fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        string outputPath = Path.Combine(outboxDirectory, fileName);
        string tempPath = outputPath + ".tmp";

        ReferenceOutboundEnvelope envelope = new(chatJid.Value, text, DateTimeOffset.UtcNow);
        string payload = JsonSerializer.Serialize(envelope, JsonOptions);
        await File.WriteAllTextAsync(tempPath, payload, cancellationToken);
        File.Move(tempPath, outputPath);
    }

    public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendFileAsync(ChatJid chatJid, string filePath, string fileName, string? threadTs, CancellationToken cancellationToken = default)
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

        string[] files = Directory.GetFiles(inboxDirectory, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReferenceInboundEnvelope? envelope;
            try
            {
                string json = await File.ReadAllTextAsync(filePath, cancellationToken);
                envelope = JsonSerializer.Deserialize<ReferenceInboundEnvelope>(json, JsonOptions);
                ValidateEnvelope(envelope);
            }
            catch
            {
                MoveToDirectory(filePath, errorsDirectory);
                continue;
            }

            ChatJid chatJid = new(envelope!.ChatJid!);
            ownedChats.TryAdd(chatJid.Value, 0);

            if (!string.IsNullOrWhiteSpace(envelope.ChatName) || envelope.IsGroup is not null)
            {
                await onMetadata(
                    new ChannelMetadataEvent(
                        chatJid,
                        envelope.Timestamp ?? DateTimeOffset.UtcNow,
                        envelope.ChatName,
                        Name,
                        envelope.IsGroup),
                    cancellationToken);
            }

            StoredMessage storedMessage = new(
                envelope.Id!,
                chatJid,
                envelope.Sender!,
                envelope.SenderName!,
                envelope.Content!,
                envelope.Timestamp ?? DateTimeOffset.UtcNow,
                envelope.IsFromMe,
                envelope.IsBotMessage);

            await onMessage(new ChannelMessage(chatJid, storedMessage), cancellationToken);
            MoveToDirectory(filePath, processedDirectory);
        }
    }

    private static void ValidateEnvelope(ReferenceInboundEnvelope? envelope)
    {
        if (envelope is null
            || string.IsNullOrWhiteSpace(envelope.Id)
            || string.IsNullOrWhiteSpace(envelope.ChatJid)
            || string.IsNullOrWhiteSpace(envelope.Sender)
            || string.IsNullOrWhiteSpace(envelope.SenderName)
            || string.IsNullOrWhiteSpace(envelope.Content))
        {
            throw new InvalidOperationException("Invalid reference channel inbound envelope.");
        }
    }

    private static void MoveToDirectory(string sourcePath, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        string destinationPath = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(sourcePath, destinationPath);
    }

    private sealed record ReferenceInboundEnvelope(
        string? Id,
        string? ChatJid,
        string? Sender,
        string? SenderName,
        string? Content,
        DateTimeOffset? Timestamp,
        bool IsFromMe,
        bool IsBotMessage,
        string? ChatName,
        bool? IsGroup);

    private sealed record ReferenceOutboundEnvelope(string ChatJid, string Text, DateTimeOffset Timestamp);
}
