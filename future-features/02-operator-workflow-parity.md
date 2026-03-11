# Operator Workflow Parity

## Description

NanoClaw has a more polished operator surface for registration, task management, channel setup, and day-to-day administration. NetClaw now has the underlying runtime primitives and scheduler tools, but operator workflows still need to be made smoother and more discoverable.

Current baseline:

- Group registration, session lookup, scheduler creation, pause, resume, cancel, and task listing now exist as live tools.
- Setup flows exist, but they are still closer to engineering steps than polished operator UX.
- Slack is functional, but the repo does not yet expose a unified control-plane experience across all channels.

## High-Level Steps

1. Build a coherent main-group control plane around the tools that already exist: registration, task management, channel checks, session inspection, and input control.
2. Add setup subcommands and diagnostics commands that map directly onto common operator jobs such as channel registration, auth validation, and repair steps.
3. Standardize tool result formats so the agent can answer admin questions cleanly and consistently.
4. Add docs and guided flows for the likely operator tasks: adding channels, managing tasks, diagnosing silent failures, and cleaning up sessions.
5. Expand integration tests around cross-group authorization and main-group-only actions.

## Complexity

Medium. Most of the underlying capabilities already exist, so the remaining work is UX, control-plane consistency, and operator-focused testing rather than deep infrastructure work.