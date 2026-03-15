using NetClaw.Application.Observability;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Channels;

public sealed class ChannelIngressService
{
    private readonly IMessageRepository messageRepository;
    private readonly IFileAttachmentRepository fileAttachmentRepository;
    private readonly IMessageNotifier messageNotifier;

    public ChannelIngressService(IMessageRepository messageRepository, IFileAttachmentRepository fileAttachmentRepository, IMessageNotifier messageNotifier)
    {
        this.messageRepository = messageRepository;
        this.fileAttachmentRepository = fileAttachmentRepository;
        this.messageNotifier = messageNotifier;
    }

    public async Task HandleMessageAsync(ChannelName channelName, ChannelMessage channelMessage, CancellationToken cancellationToken = default)
    {
        await messageRepository.StoreMessageAsync(channelMessage.Message, cancellationToken);

        foreach (FileAttachment attachment in channelMessage.Message.Attachments)
        {
            await fileAttachmentRepository.StoreAsync(attachment, cancellationToken);
        }

        messageNotifier.NotifyNewMessage(channelMessage.Message);
    }

    public Task HandleMetadataAsync(ChannelMetadataEvent metadataEvent, CancellationToken cancellationToken = default)
    {
        ChatInfo chatInfo = new(
            metadataEvent.ChatJid,
            string.IsNullOrWhiteSpace(metadataEvent.Name) ? metadataEvent.ChatJid.Value : metadataEvent.Name,
            metadataEvent.Timestamp,
            metadataEvent.Channel ?? new ChannelName("unknown"),
            metadataEvent.IsGroup ?? false);

        return messageRepository.StoreChatMetadataAsync(chatInfo, cancellationToken);
    }
}
