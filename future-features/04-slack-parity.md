# Slack Parity

## Description

Slack is NetClaw's first real live messaging channel and is already partly implemented. Effective parity means finishing the remaining event-model, setup, and UX gaps so Slack can serve as a polished primary channel rather than only a working proof point.

Current baseline:

- Socket Mode, threaded replies, DM assistant-thread behavior, and visible working state are implemented.
- The repo already supports mention replacement, Slack auth, and per-channel registration.
- Remaining gaps are mostly polish, documentation depth, live validation coverage, and edge-case routing.

## High-Level Steps

1. Complete broad Slack event coverage for channel, group, DM, mention, and assistant-thread cases.
2. Harden mention normalization and thread handling across all supported message shapes.
3. Add setup and verification flows in `NetClaw.Setup` that mirror the NanoClaw skill guidance but fit the .NET CLI.
4. Add richer file and metadata handling if Slack is expected to be the main daily-use channel.
5. Add more live and integration coverage for bot scopes, missing-scope failure modes, and DM assistant paths.

## Complexity

Medium. The hard architectural work is done. Remaining work is channel polish, setup UX, and better coverage for real Slack workspace behavior.