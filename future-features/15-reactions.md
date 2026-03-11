# Reactions

## Description

The reactions feature tracks inbound emoji reactions and lets the agent send reactions back to messages. In NanoClaw this is mostly WhatsApp-driven. In NetClaw, effective parity depends first on the base channel supporting message identifiers and reaction events.

Current baseline:

- NetClaw does not yet implement reaction-aware channels.
- WhatsApp is not yet present, so the original primary use case is blocked.
- Some other channels like Slack or Discord could support partial reaction parity once their adapters mature.

## High-Level Steps

1. Add stable message IDs to the relevant channel adapters and persistence path.
2. Add inbound reaction-event storage and a forward-only state model for reaction lifecycle where the channel supports it.
3. Add an agent tool for reacting to a specific stored message by ID.
4. Start with the first channel that supports the feature cleanly, then expand to others.
5. Add tests around duplicate reactions, state updates, and missing-message failures.

## Complexity

Medium-High. The core logic is not hard, but the feature is channel-dependent and the highest-value original use case is blocked on WhatsApp.