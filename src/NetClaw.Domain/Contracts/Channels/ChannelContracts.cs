using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Contracts.Channels;

public sealed record ChannelMessage(ChatJid ChatJid, StoredMessage Message);

public sealed record ChannelMetadataEvent(ChatJid ChatJid, DateTimeOffset Timestamp, string? Name, ChannelName? Channel, bool? IsGroup);

public interface IChannel
{
    ChannelName Name { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default);

    bool IsConnected { get; }

    bool Owns(ChatJid chatJid);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default);

    Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default);
}

public interface IInboundChannel : IChannel
{
    Task PollInboundAsync(
        Func<ChannelMessage, CancellationToken, Task> onMessage,
        Func<ChannelMetadataEvent, CancellationToken, Task> onMetadata,
        CancellationToken cancellationToken = default);
}