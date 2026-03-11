# Telegram Swarm

## Description

The Telegram swarm feature gives multi-agent teams separate visible identities in a Telegram conversation by routing subagent output through a pool of Telegram bots. Effective parity for NetClaw means preserving the agent-team experience while adapting the implementation to Copilot and the existing NetClaw message pipeline.

Current baseline:

- NetClaw has no Telegram channel and no bot-pool abstraction.
- The current runtime has no sender-identity routing beyond the single-channel sender model.
- The scheduler and tool model are mature enough to support this once channel and sender-routing abstractions expand.

## High-Level Steps

1. Ship the base Telegram channel first.
2. Add a bot-pool service that can manage multiple Telegram bot tokens and stable sender-to-bot assignments.
3. Extend the outbound message contract so agent-generated messages can include an optional visible sender identity.
4. Route team/subagent output through the bot pool rather than the main bot when a sender identity is provided.
5. Persist mapping state so bot assignment is stable across restarts.
6. Add explicit operating guidance for rate limits, rename propagation, and bot availability.

## Complexity

Medium-High. The core Telegram channel is manageable, but the swarm layer requires new abstractions for pooled identities, sender routing, and persistence.