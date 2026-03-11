# Discord Channel

## Description

Discord parity means adding a bot-based channel with support for server channels, threads, DMs, typing indicators, and mention-aware routing. Compared with WhatsApp, this is a more conventional integration target for .NET.

Current baseline:

- NetClaw does not yet have a Discord adapter.
- The host and channel interfaces are already a good fit for Discord's event stream.
- The .NET ecosystem has mature Discord client libraries.

## High-Level Steps

1. Implement a Discord channel using a stable .NET client library.
2. Add setup guidance for bot token creation, required intents, and server/channel registration.
3. Normalize mentions, reply context, and thread routing into NetClaw's message model.
4. Add typing indicators and sane long-message splitting for outbound replies.
5. Add channel tests and at least one integration path validating real gateway behavior.

## Complexity

Medium-Low. Discord is a relatively direct implementation on Linux and .NET. Most of the cost is setup UX and message-shape handling, not foundational architecture.