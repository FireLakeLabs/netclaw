# Compact Mode

## Description

Compact mode gives long-running sessions a manual way to reduce context drift without fully discarding the conversation. Effective parity means providing a session-control behavior appropriate to Copilot, even if the implementation differs from Claude's native `/compact` semantics.

Current baseline:

- NetClaw has persisted sessions and resume support.
- It does not yet have manual compaction, transcript archival before compaction, or session-command parsing.

## High-Level Steps

1. Add a main-group or trusted-sender session control entry point for compaction.
2. Determine whether the Copilot SDK exposes a native equivalent; if not, implement compaction as a controlled summarize-and-resume flow.
3. Archive the pre-compaction transcript and session metadata before mutating session state.
4. Persist the replacement or continued session ID cleanly through the session repository.
5. Add explicit denial rules for untrusted callers and tests around in-flight session interruption.

## Complexity

Medium. The work is bounded, but the exact semantics depend on Copilot capabilities rather than a direct Claude SDK match.