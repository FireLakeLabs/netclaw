using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Channels;

public sealed class ChannelIngressService
{
    private readonly IMessageRepository messageRepository;

    public ChannelIngressService(IMessageRepository messageRepository)
    {
        this.messageRepository = messageRepository;
    }

    public Task HandleMessageAsync(ChannelName channelName, ChannelMessage channelMessage, CancellationToken cancellationToken = default)
    {
        return messageRepository.StoreMessageAsync(channelMessage.Message, cancellationToken);
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
