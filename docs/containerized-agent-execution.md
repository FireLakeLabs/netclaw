# Containerized Agent Execution

## Summary

Move agent execution from in-process SDK calls into isolated Docker/Podman containers. The host spawns a container per execution, communicates via stdin/stdout JSONL (one JSON object per line), mounts per-group workspaces with least-privilege permissions, and injects credentials through an HTTP proxy. Both Copilot and Claude Code providers run inside a single shared container image. The existing in-process execution path is removed once container execution works.

## Motivation

Today, `NetClawAgentRuntime` calls `CopilotCodingAgentEngine` in-process via the Copilot SDK. The agent runs with the same permissions as the host — full access to the filesystem, environment variables (including API keys), and network. There is no isolation boundary between the agent and the host process.

Container-based execution provides:

- **Process isolation**: The agent runs in a separate process with its own filesystem view.
- **Filesystem isolation**: Only explicitly mounted directories are visible to the agent. Non-main groups cannot see other groups' data or the project root.
- **Credential isolation**: Real API keys never enter the container. A credential proxy on the host injects them transparently.
- **Resource boundaries**: Containers can be stopped on timeout, cleaned up on orphan detection, and constrained by runtime limits.
- **Provider flexibility**: Different agent providers (Copilot, Claude Code) run in the same container image with the provider selected at runtime.

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                          NetClaw Host                            │
│                                                                  │
│  Channel → Router → NetClawAgentRuntime                          │
│                          │                                       │
│                          ▼                                       │
│              ContainerizedAgentEngine                            │
│                │         │         │                              │
│         Build mounts  Build args  Spawn container                │
│                                    │                              │
│                     stdin ─────────┼──── ContainerInput JSON      │
│                     stdout ────────┼──── JSONL output (one per line)│
│                     stderr ────────┼──── Diagnostics/logs         │
│                                    │                              │
│              CredentialProxyService │                              │
│                │                   │                              │
│         Real API keys ──► Proxy ◄──┼── Container HTTP requests    │
│                                    │   (placeholder credentials)  │
│                                                                  │
│  FileSystemIpcWatcher ◄── mounted /workspace/ipc/ ──► IPC files  │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                     Container (netclaw-agent)                     │
│                                                                  │
│  NetClaw.AgentRunner (.NET console app)                          │
│      │                                                           │
│      ├── Read ContainerInput from stdin                          │
│      ├── Select provider (Copilot CLI / claude CLI)              │
│      ├── Execute agent, stream output via JSONL to stdout        │
│      ├── Poll /workspace/ipc/input/ for follow-up messages       │
│      └── MCP IPC tool server → writes to /workspace/ipc/         │
│                                                                  │
│  Mounts:                                                         │
│    /workspace/group     ← group directory (rw)                   │
│    /workspace/global    ← global directory (ro, non-main)        │
│    /workspace/ipc       ← IPC directory (rw)                     │
│    /workspace/project   ← project root (ro, main only)           │
│    /home/user/.copilot  ← session state (rw)                     │
└──────────────────────────────────────────────────────────────────┘
```

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Container image count | Single image with both CLIs | Copilot CLI and Claude Code don't conflict; negligible size difference; simpler config |
| Agent runner language | .NET console app | Same toolchain as the host; shares `NetClaw.Domain` contracts directly |
| Host-side fallback | None — remove in-process path | Container execution is the only mode; eliminates dual-path maintenance |
| Credential injection | HTTP proxy on docker bridge | Containers never see real secrets; proxy intercepts and injects transparently |
| Interactive session IPC | File-based polling in mounted directory | Replaces in-memory `Channel<string>`; works across the process boundary |
| Streaming protocol | JSONL on stdout | One compact JSON object per line; standard format, simple parsing, debuggable with `jq` |

## Existing Seams

These interfaces and types already exist and are filled in by this work:

| Seam | Location | Status |
|------|----------|--------|
| `IContainerRuntime` | `ServiceContracts.cs` | Implemented (`DockerContainerRuntime`); used for health checks, stop, cleanup |
| `IContainerExecutionService` | `ServiceContracts.cs` | Implemented (`ContainerExecutionService`) — wraps `ContainerizedAgentEngine` |
| `ContainerMount` | `ContainerContracts.cs` | Used by `ContainerizedAgentEngine.BuildMounts()` |
| `ContainerExecutionRequest` | `ContainerContracts.cs` | Used by `ContainerExecutionService.RunAsync()` |
| `KeepContainerBoundary` | `AgentRuntimeOptions.cs` | Removed — containerized execution is the only mode |
| `CredentialProxyOptions` | `CredentialProxyOptions.cs` | Extended with `CopilotUpstreamUrl`, `ClaudeUpstreamUrl`, `AuthMode`; used by `CredentialProxyService` |
| `ICredentialProxyService` | `ServiceContracts.cs` | Implemented (`CredentialProxyService`) — HTTP proxy with credential injection |
| `FileSystemIpcWatcher` | `FileSystemIpcWatcher.cs` | Fully implemented; polls IPC directories for JSON command files |

---

## Phase 1: Container Agent Runner

A standalone .NET 10 console app (`src/NetClaw.AgentRunner/`) that runs inside the container.

### 1. Project structure

- References only `NetClaw.Domain` for shared records (`ContainerInput`, `ContainerOutput`, `ContainerStreamEvent`).
- No reference to `NetClaw.Infrastructure` or `NetClaw.Host`.
- Entry point reads `NETCLAW_PROVIDER` env var and dispatches to the appropriate provider path.

### 2. Stdin/stdout protocol (JSONL)

- Host writes a single-line `ContainerInput` JSON to container stdin, then closes stdin.
- Container writes one `ContainerOutput` JSON per line to stdout (compact, no pretty-printing).
- Multiple lines may be emitted during a single execution (streaming results).
- All diagnostic output goes to stderr, never stdout.
- The .NET agent runner fully controls stdout — all subprocess output is redirected to stderr, so there is no contamination risk. This is why JSONL works cleanly here without the sentinel markers that NanoClaw uses (where the Node.js runtime has less control over stdout).

### 3. Copilot provider path

- Use the Copilot SDK (`GitHub.Copilot.SDK`) to create/resume sessions and send prompts.
- Configuration from environment variables: `NETCLAW_COPILOT_MODEL`, `NETCLAW_COPILOT_STREAMING`, `NETCLAW_COPILOT_REASONING_EFFORT`, etc.
- Credential proxy URL injected as the Copilot CLI endpoint.

### 4. Claude Code provider path

- Shell out to the `claude` CLI binary installed in the container image.
- `ANTHROPIC_BASE_URL` points to the credential proxy.
- `ANTHROPIC_API_KEY=placeholder` — the proxy replaces it with the real key.
- Parse CLI output into `ContainerOutput` format.

### 5. IPC follow-up polling (interactive sessions)

- Poll `/workspace/ipc/input/` for JSON files containing follow-up user messages.
- Files are ordered by filename (timestamp-based naming).
- A `_close` sentinel file signals session end.
- Replaces the in-memory `Channel<string>` from `CopilotInteractiveAgentSession`.

### 6. MCP IPC tool server

- Expose the same tools from `NetClawAgentToolRegistry` as an MCP stdio server.
- Tools: `send_group_message`, `schedule_group_task`, `list_scheduled_tasks`, `pause_scheduled_task`, `resume_scheduled_task`, `cancel_scheduled_task`, `lookup_session_state`, `close_group_input`, `register_group` (main only).
- Tools write JSON files to `/workspace/ipc/messages/` and `/workspace/ipc/tasks/`.
- The host's `FileSystemIpcWatcher` already polls these directories.

---

## Phase 2: Container Image

Single Dockerfile producing `netclaw-agent`.

### 7. Dockerfile

- Base: .NET 10 runtime image.
- Install Node.js (for Claude CLI).
- Install Copilot CLI globally.
- Install `@anthropic-ai/claude-code` globally.
- Publish `NetClaw.AgentRunner` into the image.
- Entry point: the agent runner, with provider selected via `NETCLAW_PROVIDER` env var.

### 8. Build script

- `container/build.sh` — builds the `netclaw-agent` image.
- Supports `CONTAINER_RUNTIME` env var (docker/podman).
- Accepts optional tag argument (defaults to `latest`).

---

## Phase 3: Credential Proxy

### 10. CredentialProxyService

Implement `ICredentialProxyService` in `NetClaw.Infrastructure`:

- HTTP listener (Kestrel or `HttpListener`).
- Binds to the docker bridge IP on Linux (auto-detected from `docker0` interface), 127.0.0.1 on WSL/macOS.
- Request flow:
  1. Receive request from container.
  2. Collect body.
  3. Strip hop-by-hop headers.
  4. Inject real credentials based on auth mode:
     - **Copilot**: Inject GitHub token header.
     - **Claude Code (api-key mode)**: Inject `x-api-key` header.
     - **Claude Code (oauth mode)**: Intercept token exchange, inject real OAuth token.
  5. Forward to upstream API.
  6. Pipe response back to container.

### 11. CredentialProxyOptions

Extend the existing config:

- `UpstreamUrl` — target API base URL.
- `AuthMode` — `api-key` or `oauth`.
- Bind-host auto-detection logic.

### 12. Hosted service registration

- Start the proxy before agent execution begins.
- Stop on host shutdown.
- Register in DI alongside existing hosted services.

---

## Phase 4: Host-Side Container Execution Engine

### 13. ContainerizedAgentEngine

New `ICodingAgentEngine` + `IInteractiveCodingAgentEngine` implementation:

- `ExecuteAsync()`: Build mounts → build args → spawn container via `Process.Start()` → write `ContainerInput` to stdin → parse stdout JSONL → translate to `ContainerStreamEvent` → return `ContainerExecutionResult`.
- `StartInteractiveSessionAsync()`: Same spawn flow, but keep container alive. Write follow-up messages as JSON files to the mounted IPC input directory. Return `IInteractiveContainerSession` backed by file-based IPC.
- Provider selection via `NETCLAW_PROVIDER` env var at container start.
- Uses `IContainerRuntime` for gateway args, stop, cleanup.
- Uses `ICommandRunner` for process spawning.

### 14. Mount construction

Extend `NetClawAgentWorkspaceBuilder` to produce `IReadOnlyList<ContainerMount>`:

| Host Path | Container Path | Access |
|-----------|---------------|--------|
| Group directory | `/workspace/group` | read-write |
| Global directory | `/workspace/global` | read-only (non-main only) |
| Session directory | `/home/user/.copilot` or `.claude` | read-write |
| IPC directory | `/workspace/ipc` | read-write |
| Project root | `/workspace/project` | read-only (main only, `.env` shadowed to `/dev/null`) |
| Additional mounts | Per group config | Validated against `MountSecurityValidator` |

### 15. Container arg construction

Build the `docker run` argument list:

```
docker run -i --rm --name {containerName}
  -e TZ={timezone}
  -e NETCLAW_PROVIDER={copilot|claude-code}
  -e NETCLAW_CREDENTIAL_PROXY_URL=http://host.docker.internal:{port}
  -e ANTHROPIC_API_KEY=placeholder
  --add-host=host.docker.internal:host-gateway  # Linux only
  --user {uid}:{gid}                            # when not root
  -v {host}:{container}[:ro]                    # for each mount
  netclaw-agent:latest
```

### 16. Stdout JSONL parser

- Read stdout from the container process line by line.
- Deserialize each line as a `ContainerOutput` JSON object.
- Translate to `ContainerStreamEvent` and invoke `onStreamEvent` callback.
- Reset the hard timeout on each valid line (activity detection).
- Skip empty lines. Log and discard lines that fail deserialization (defensive, should not happen since the runner controls stdout).

### 17. Timeout and lifecycle

- Configurable hard timeout per execution.
- `docker stop` on timeout (graceful shutdown).
- `SIGKILL` fallback if graceful stop fails.
- Log container stderr/stdout to per-group log directory on error or debug level.

### 18. IContainerExecutionService

Wire `ContainerizedAgentEngine` behind the existing `IContainerExecutionService` interface. This is the seam `NetClawAgentRuntime` calls.

---

## Phase 5: Rewire the Runtime

### 19. Modify NetClawAgentRuntime

Instead of resolving `ICodingAgentEngine` by provider and calling it in-process, delegate to `IContainerExecutionService`. The runtime still handles:

- Group resolution
- Workspace building
- Session persistence
- Stream event translation

But the actual agent execution crosses the container boundary.

### 20. Remove in-process engines

Remove from DI:

- `CopilotCodingAgentEngine`
- `ClaudeCodePlaceholderEngine`
- `CodexPlaceholderEngine`
- `OpenCodePlaceholderEngine`
- `ICopilotClientPool`
- `ICopilotClientAdapterFactory`
- `SdkCopilotClientAdapterFactory`

Register `ContainerizedAgentEngine` instead.

### 21. Remove KeepContainerBoundary

No longer needed — containerized execution is the only mode. Clean up `AgentRuntimeOptions`.

### 22. Extend ContainerRuntimeOptions

Add:

- `ImageName` — the container image (default: `netclaw-agent:latest`).
- `Timeout` — hard timeout per execution.
- `MaxOutputSize` — truncation limit for stdout/stderr.

---

## Phase 6: Testing

### 23. Agent runner unit tests

Test stdin → execution → stdout protocol with a mock agent SDK. Verify JSONL output formatting, error handling, IPC polling.

### 24. ContainerizedAgentEngine unit tests

Test mount construction, arg building, stdout parsing, timeout behavior using `FakeCommandRunner` / mock process. No real Docker needed.

### 25. CredentialProxyService unit tests

Verify header injection, auth mode selection, upstream forwarding, hop-by-hop stripping.

### 26. Integration test — full loop

Extend `EndToEndIntegrationTests`:

- Register a group.
- Start the host with a stub container image (echo container that reads stdin and writes canned output between markers).
- Send a message through the message loop.
- Verify the response arrives via the outbound router.

### 27. Container build in CI

- Build and tag images in CI.
- Smoke test: agent runner starts and responds to canned stdin JSON.

---

## Verification Criteria

1. `dotnet build` succeeds for all projects including `NetClaw.AgentRunner`.
2. `dotnet test` passes all unit and integration tests.
3. `container/build.sh` produces a working `netclaw-agent` image.
4. Smoke test: `echo '{"prompt":"hi",...}' | docker run -i -e NETCLAW_PROVIDER=copilot netclaw-agent:latest` returns valid JSONL output (one JSON object per line).
5. End-to-end: Terminal channel → container spawn → agent response → terminal output.
6. Credential proxy: Containers cannot read real API keys; proxy injects transparently.
7. Mount security: Non-main groups cannot write to project root; `.env` is shadowed.
8. Interactive sessions: Follow-up IPC files reach the agent; `_close` sentinel terminates the session.
9. Lifecycle: Containers stop on timeout; orphan cleanup catches stale containers.

## Further Considerations

- **Codex and OpenCode**: Currently placeholders. Their CLIs can be added to the single container image when implemented — no structural changes needed.
- **Session persistence**: Session data lives on mounted volumes that survive container lifecycle. Host tracks session IDs in SQLite; provider-specific state lives in the mounted session directory.
- **Container image registry**: Local-only builds for now; registry push can be added to CI later if remote deployment is needed.
