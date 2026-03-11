# WhatsApp Channel

## Description

WhatsApp is one of NanoClaw's most important live channels. It provides personal self-chat, group-chat, and device-linked messaging using WhatsApp Web-style authentication. Effective parity on NetClaw means supporting a reliable Linux-friendly auth and message path, even if the implementation uses a sidecar rather than a pure .NET client.

Current baseline:

- NetClaw has no WhatsApp channel yet.
- There is no voice, image, or reaction path because those features all depend on the base WhatsApp integration.
- The current architecture can host a new channel cleanly once an adapter exists.

## High-Level Steps

1. Decide the integration boundary: pure .NET if a stable library exists, or a Node sidecar around Baileys if it does not.
2. Implement auth flows suitable for Linux: pairing code, terminal QR, and optionally browser QR.
3. Build inbound and outbound message translation into NetClaw's channel model, including JID mapping, trigger handling, and group registration.
4. Persist auth state securely and make recovery/re-auth flows explicit in setup tooling.
5. Add integration tests around auth state, inbound message normalization, and outbound delivery.
6. Use this channel as the prerequisite base for reactions, image vision, and voice transcription.

## Complexity

High. WhatsApp is the hardest mainstream channel because the best-supported ecosystem is still Node-centric and operationally fragile compared with Slack, Discord, or Telegram.