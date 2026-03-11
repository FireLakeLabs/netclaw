using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Routing;

public sealed class ChannelOutboundRouter : IOutboundRouter
{
    public async Task RouteAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string text, CancellationToken cancellationToken = default)
    {
        IChannel? channel = channels.FirstOrDefault(candidate => candidate.IsConnected && candidate.Owns(chatJid));
        if (channel is null)
        {
            throw new InvalidOperationException($"No connected channel owns chat JID '{chatJid.Value}'.");
        }

        await channel.SendMessageAsync(chatJid, text, cancellationToken);
    }
}
