# Image Vision

## Description

Image vision lets the agent inspect images sent through a live channel and reason over them as part of the conversation. In NetClaw, effective parity should target the channels that make sense first, especially Slack and later WhatsApp.

Current baseline:

- NetClaw has no media ingestion or multimodal message path.
- Slack is currently text-first.
- Copilot/GPT-5.4 can support image understanding if the runtime passes the content correctly.

## High-Level Steps

1. Add channel-level attachment detection and secure download handling.
2. Store images in a per-group workspace area with retention rules.
3. Resize or normalize images with a .NET image library such as ImageSharp.
4. Extend the runtime prompt/input model so images can be passed into Copilot sessions in a supported multimodal shape.
5. Add tests around attachment download failure, file-size limits, and multimodal message formatting.

## Complexity

Medium. The feature is practical on .NET and Linux, but it requires cross-cutting changes in channels, storage, and the Copilot session input builder.