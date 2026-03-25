# Backbone Parity

## Description

NetClaw is already close to NanoClaw on the core runtime backbone: persisted group and session state, routing, IPC, scheduled tasks, interactive sessions, and a live hosted message path all exist. Effective parity now means hardening those foundations so they can support a larger feature surface without rework.

Current baseline:

- SQLite-backed groups, sessions, and scheduled tasks are implemented.
- The Copilot runtime supports persisted sessions, custom tools, and interactive flows.
- Slack, terminal, and reference-file channels give NetClaw a real inbound and outbound path.
- Container execution is implemented with Docker and Podman support. A credential proxy injects secrets transparently.
- Claude Code is a live provider alongside Copilot, both running inside the shared container image.

## High-Level Steps

1. Promote more runtime behaviors to explicit contracts and tests: media ingestion, tool registration, long-running session lifecycle, and channel capability negotiation.
2. Add a richer operational diagnostics layer covering per-group state, channel health, scheduler health, and active session health.
3. Tighten restart and recovery behavior so in-flight sessions, pending tasks, and active channels survive host restarts predictably.
4. Add end-to-end integration coverage for the full message loop, not just focused runtime and host slices.

## Complexity

Medium. The architecture is already in place, so this is mostly hardening and test depth rather than greenfield work. The main cost is integration coverage and container/runtime cleanup, not new domain modeling.