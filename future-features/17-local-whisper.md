# Local Whisper

## Description

Local Whisper replaces remote transcription with on-device speech-to-text, typically through `whisper.cpp` or another local inference path. Effective parity for NetClaw means making it a Linux-native optional backend under the same transcription abstraction.

Current baseline:

- NetClaw has no transcription abstraction yet.
- Linux is a better target than macOS-specific tooling because the repo is already Linux-first.

## High-Level Steps

1. First add the generic transcription abstraction used by the voice-transcription feature.
2. Implement a Linux-friendly local backend using `whisper.cpp`, a direct binary invocation, or a stable service wrapper.
3. Add model download, model-path configuration, and resource guidance.
4. Keep provider selection configurable so deployments can choose API or local transcription.
5. Add performance and timeout controls so long audio does not starve the host.

## Complexity

Medium. The architectural path is clean once voice transcription exists, but model packaging and host resource management add operational work.