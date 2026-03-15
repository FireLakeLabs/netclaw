using NetClaw.Application.Observability;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Routing;

public sealed class ChannelOutboundRouter(IMessageRepository messageRepository, IFileAttachmentRepository fileAttachmentRepository, IMessageNotifier messageNotifier) : IOutboundRouter
{
    public async Task RouteAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string text, CancellationToken cancellationToken = default)
    {
        IChannel? channel = channels.FirstOrDefault(candidate => candidate.IsConnected && candidate.Owns(chatJid));
        if (channel is null)
        {
            throw new InvalidOperationException($"No connected channel owns chat JID '{chatJid.Value}'.");
        }

        await channel.SendMessageAsync(chatJid, text, cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        StoredMessage outbound = new(
            $"out-{now.ToUnixTimeMilliseconds()}-{Guid.NewGuid()}",
            chatJid,
            "agent",
            "Agent",
            text,
            now,
            isFromMe: true,
            isBotMessage: true);

        await messageRepository.StoreMessageAsync(outbound, cancellationToken);
        messageNotifier.NotifyNewMessage(outbound);
    }

    public async Task RouteFileAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string filePath, string fileName, CancellationToken cancellationToken = default)
    {
        IChannel? channel = channels.FirstOrDefault(candidate => candidate.IsConnected && candidate.Owns(chatJid));
        if (channel is null)
        {
            throw new InvalidOperationException($"No connected channel owns chat JID '{chatJid.Value}'.");
        }

        await channel.SendFileAsync(chatJid, filePath, fileName, threadTs: null, cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string messageId = $"out-{now.ToUnixTimeMilliseconds()}-{Guid.NewGuid()}";
        string fileId = $"outfile-{Guid.NewGuid()}";
        long fileSize = new FileInfo(filePath).Length;

        FileAttachment attachment = new(
            fileId,
            messageId,
            chatJid,
            fileName,
            mimeType: null,
            fileSize,
            filePath,
            now);

        StoredMessage outbound = new(
            messageId,
            chatJid,
            "agent",
            "Agent",
            string.Empty,
            now,
            isFromMe: true,
            isBotMessage: true,
            attachments: [attachment]);

        await messageRepository.StoreMessageAsync(outbound, cancellationToken);
        await fileAttachmentRepository.StoreAsync(attachment, cancellationToken);
        messageNotifier.NotifyNewMessage(outbound);
    }
}
