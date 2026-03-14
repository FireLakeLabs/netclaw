using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Routing;

public sealed class ChannelOutboundRouter(IMessageRepository messageRepository) : IOutboundRouter
{
    private int sequence;

    public async Task RouteAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string text, CancellationToken cancellationToken = default)
    {
        IChannel? channel = channels.FirstOrDefault(candidate => candidate.IsConnected && candidate.Owns(chatJid));
        if (channel is null)
        {
            throw new InvalidOperationException($"No connected channel owns chat JID '{chatJid.Value}'.");
        }

        await channel.SendMessageAsync(chatJid, text, cancellationToken);

        int seq = Interlocked.Increment(ref sequence);
        StoredMessage outbound = new(
            $"out-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{seq}",
            chatJid,
            "agent",
            "Agent",
            text,
            DateTimeOffset.UtcNow,
            isFromMe: true,
            isBotMessage: true);

        await messageRepository.StoreMessageAsync(outbound, cancellationToken);
    }
}
