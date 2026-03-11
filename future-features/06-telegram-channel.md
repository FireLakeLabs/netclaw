# Telegram Channel

## Description

Telegram provides a comparatively clean bot API for group and DM interactions. Effective parity means adding a stable Telegram bot channel with registration, trigger gating, and support for both passive and interactive chat patterns.

Current baseline:

- NetClaw has no Telegram channel today.
- The host/channel abstractions are ready for another API-driven text channel.
- The .NET ecosystem has mature Telegram bot clients, so this is mostly missing implementation rather than a platform blocker.

## High-Level Steps

1. Implement `TelegramChannel` using a stable .NET Telegram bot library.
2. Add setup CLI guidance for bot creation, token configuration, and group privacy decisions.
3. Add chat discovery and registration flows so users can register DMs or groups cleanly.
4. Normalize mentions, group metadata, and reply context into NetClaw's message model.
5. Add tests for trigger-required and no-trigger-required group configurations.

## Complexity

Medium. The main work is channel plumbing and setup UX. Telegram is one of the cleaner parity targets because the API and auth model are straightforward on Linux and .NET.