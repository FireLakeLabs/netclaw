# Voice Transcription

## Description

Voice transcription turns incoming voice notes into text before they reach the agent. Effective parity on NetClaw means adding a media-preprocessing pipeline plus a transcription provider, initially most likely through an API-backed service.

Current baseline:

- NetClaw has no voice-capable channel and no audio processing pipeline.
- The feature is blocked on a channel that can deliver audio, most notably WhatsApp or possibly Telegram.

## High-Level Steps

1. Add a channel with downloadable audio attachments.
2. Add audio normalization and temporary-file handling.
3. Add a transcription service contract and implement an API-backed provider first.
4. Inject the transcript into the conversation in a predictable format and preserve attachment metadata.
5. Add retry, timeout, and size limits so transcription failures do not stall the message loop.

## Complexity

Medium-High. The transcription provider is manageable, but the real dependency is the missing media-capable channel and the end-to-end audio ingestion path.