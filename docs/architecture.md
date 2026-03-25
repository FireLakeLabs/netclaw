# Architecture

## Overview

NetClaw is a hosted .NET application that accepts inbound chat messages, stores them, decides which registered group should react, and runs the selected prompt through an agent runtime. The current real provider is Copilot. The rest of the system is arranged to keep that provider choice isolated from channel handling, persistence, scheduling, and setup.

The repo is split into five projects:

- `FireLakeLabs.NetClaw.Domain`: contracts and core types
- `FireLakeLabs.NetClaw.Application`: orchestration and workflow services
- `FireLakeLabs.NetClaw.Infrastructure`: adapters for channels, persistence, runtime integration, and OS-facing behavior
- `FireLakeLabs.NetClaw.Host`: dependency injection and hosted services
- `FireLakeLabs.NetClaw.Setup`: operational CLI

## Main Runtime Flow

At startup, `FireLakeLabs.NetClaw.Host` builds the service graph in `ServiceCollectionExtensions`. The host wires configuration, persistence, channels, the agent runtime, scheduling, and the background workers.

The main workers are:

- `HostInitializationService`: creates and validates the local runtime state
- `ChannelWorker`: connects enabled channels and polls their inbound queues
- `MessageLoopWorker`: polls persisted inbound messages and decides which groups need execution
- `IpcWatcherWorker`: watches filesystem IPC commands
- `SchedulerWorker`: runs due scheduled tasks

The hot path for a normal message looks like this:

1. A channel receives an inbound message.
2. `ChannelIngressService` stores the message and metadata in SQLite.
3. `InboundMessagePollingService` loads new stored messages, applies sender policy, and groups messages by chat.
4. If the group is eligible to trigger, it either appends input to an active session or enqueues work for the per-group execution queue.
5. `GroupMessageProcessorService` builds the formatted inbound prompt and starts an interactive agent session.
6. `NetClawAgentRuntime` resolves the provider, workspace, persisted session, and exposed tools.
7. The active provider runs the prompt. Today that is the Copilot engine.
8. Completed output is normalized and routed back to the owning channel through `ChannelOutboundRouter`.

## Group Model

Everything is anchored to a registered group. A group has:

- a chat JID
- a friendly name
- a workspace folder
- a trigger string
- an `IsMain` flag
- a `RequiresTrigger` flag

The main group acts as the control plane. It can register other groups and perform broader administrative actions. Non-main groups are more restricted.

## Persistence

SQLite is the only persistence layer today. Infrastructure repositories store:

- inbound and outbound message history
- registered groups
- persisted session IDs
- scheduled tasks and task run logs
- router state such as polling cursors and last agent timestamps

The repo uses SQLite because it is simple to inspect and easy to move around during development. It fits the current goals better than a heavier service dependency.

## Agent Runtime

The runtime boundary is intentionally separate from channel and storage concerns.

`NetClawAgentRuntime` does the following:

- loads the registered group
- resolves the provider from configuration
- builds a per-group workspace
- resolves an existing session if one exists
- delegates execution to `IContainerExecutionService`

All agent work runs inside an isolated Docker or Podman container. `ContainerizedAgentEngine` spawns the container, writes input to stdin as JSON, and parses JSONL output from stdout. A `CredentialProxyService` on the host injects real API keys — containers never see the actual secrets.

Inside the container, `FireLakeLabs.NetClaw.AgentRunner` dispatches to the configured provider CLI (Copilot or Claude Code). The provider is selected at runtime via the `NETCLAW_PROVIDER` environment variable. Placeholder engines for Codex and OpenCode still exist but are not wired into DI — their CLIs can be added to the container image when ready.

The previous in-process execution path through `CopilotCodingAgentEngine` and the Copilot SDK has been replaced by containerized execution.

## Workspaces And Sessions

Each group gets a working directory, a session directory, and a runtime workspace directory. The workspace builder is responsible for laying those out and placing agent instructions where the runtime expects them.

Interactive session state is persisted by group folder, not just by provider response. That lets a later message resume the same logical conversation.

## Tool Surface

NetClaw now exposes a small live control-plane tool set to Copilot sessions. The tool registry is provider-neutral, while the Copilot tool factory maps those tool definitions to concrete `AIFunction` handlers.

Current tools include:

- group registration and inspection
- direct group messaging
- session lookup and input closing
- scheduler creation, listing, pause, resume, and cancel

This matters because the scheduler and admin features are no longer just backend services. The agent can call them directly from chat.

## Scheduling

Scheduled tasks are persisted in SQLite and executed by `TaskSchedulerService` through `SchedulerWorker`.

Each task includes:

- task ID
- target group folder and chat JID
- prompt
- schedule type (`once`, `interval`, `cron`)
- schedule value
- context mode (`isolated` or `group`)
- status and run metadata

When a task is due, the scheduler runs it through the same agent runtime used for normal chat, optionally reusing the group's session when the task context is `group`.

## Channels

NetClaw channels implement a common interface so the host can treat them uniformly. The currently live channels are:

- `ReferenceFileChannel`: useful for local smoke testing through files
- `TerminalChannel`: useful for quick local iteration
- `SlackChannel`: first real remote channel, backed by Slack Socket Mode

Slack currently has the most complete live-channel behavior. It supports inbound message capture, outbound replies, trigger gating, thread reuse, and visible work-state handling.

## Authorization And Triggering

Inbound messages are filtered through sender authorization before they are allowed to trigger the runtime. Trigger behavior is group-specific:

- the main group can respond without an explicit trigger
- a non-main group may require the configured trigger string
- sender allowlists can block or restrict who is allowed to wake the agent

This is part of keeping the project useful in messy real chat environments.

## Container Boundary

The repo still carries a container runtime abstraction and Docker-based execution assumptions, but the current live Copilot integration is better understood as a host-managed runtime with a container-shaped seam. The delayed isolation work is to make that seam more real and less Docker-specific.

## Setup CLI

`FireLakeLabs.NetClaw.Setup` is a small CLI that handles operational steps such as registration. It exists so common setup actions are not hidden inside ad hoc scripts.

The helper scripts at the repo root are thin convenience wrappers over the setup CLI plus host startup.

## Design Priorities

The project optimizes for:

- understandable flow over abstraction theater
- direct inspectability of state and behavior
- small, testable services
- Linux-friendly development and hosting
- changing the system without needing to reverse engineer somebody else's platform first