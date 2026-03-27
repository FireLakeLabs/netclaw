# Containerized Agent Execution — Test Plan

This document outlines the manual and semi-automated tests for validating the containerized agent execution feature. Tests are grouped by component and priority. Each test lists the preconditions, steps, and expected outcome.

## Prerequisites

Before running any test:

0. Choose a container runtime for commands in this plan:
  ```bash
  export CONTAINER_RUNTIME="${CONTAINER_RUNTIME:-podman}"   # or: export CONTAINER_RUNTIME=docker
  ```
1. Build the container image: `./container/build.sh`
2. Verify the image exists: `$CONTAINER_RUNTIME images netclaw-agent`
3. Build the solution: `dotnet build`
4. Set credentials in the environment:
   - `ANTHROPIC_API_KEY` for Claude Code tests
   - `GITHUB_TOKEN` (or configure `CopilotGitHubToken` in `appsettings.json`) for Copilot tests

---

## 1. Container Image Build

### 1.1 Docker build succeeds

**Steps:**
```bash
./container/build.sh
```

**Expected:** Image `netclaw-agent:latest` is created. Build completes with exit code 0.

### 1.2 Podman build succeeds

**Steps:**
```bash
CONTAINER_RUNTIME=podman ./container/build.sh
```

**Expected:** Image `netclaw-agent:latest` is created under Podman. Exit code 0.

### 1.3 Custom tag

**Steps:**
```bash
./container/build.sh test-v1
```

**Expected:** Image `netclaw-agent:test-v1` exists.

### 1.4 Image contents

**Steps:**
```bash
$CONTAINER_RUNTIME run --rm --entrypoint /bin/bash netclaw-agent:latest -c "
  which dotnet &&
  which node &&
  which claude &&
  which copilot &&
  ls /app/FireLakeLabs.NetClaw.AgentRunner.dll &&
  id user
"
```

**Expected:**
- `dotnet`, `node`, `claude` found in PATH.
- `copilot` found in PATH (real CLI binary).
- `FireLakeLabs.NetClaw.AgentRunner.dll` exists in `/app/`.
- `user` account exists (non-root UID).

### 1.5 Workspace directories exist

**Steps:**
```bash
$CONTAINER_RUNTIME run --rm --entrypoint /bin/bash netclaw-agent:latest -c "
  ls -la /workspace/group /workspace/global /workspace/ipc /workspace/project /home/user/.copilot /home/user/.claude
"
```

**Expected:** All directories exist and are owned by `user`.

---

## 2. Agent Runner (In-Container)

### 2.1 Copilot provider — happy path

**Preconditions:** `GITHUB_TOKEN` is set on the host or `NetClaw:AgentRuntime:CopilotGitHubToken` is configured so the credential proxy can inject it.

**Steps:**
```bash
echo '{"prompt":"Say hello","sessionId":null,"groupFolder":"test","chatJid":"terminal@local","isMain":false,"isScheduledTask":false,"assistantName":"NetClaw"}' | \
  $CONTAINER_RUNTIME run -i --rm -e NETCLAW_PROVIDER=copilot netclaw-agent:latest
```

**Expected:** The runner reads stdin, selects the Copilot provider, starts the real `copilot` CLI, and emits a JSONL success line on stdout with a non-empty `result`. Exit code 0.

### 2.2 Claude Code provider — happy path

**Preconditions:** `ANTHROPIC_API_KEY` set, credential proxy running or `ANTHROPIC_BASE_URL` pointing to a live API.

**Steps:**
```bash
echo '{"prompt":"Say hello in one sentence","sessionId":null,"groupFolder":"test","chatJid":"terminal@local","isMain":false,"isScheduledTask":false,"assistantName":"NetClaw"}' | \
  $CONTAINER_RUNTIME run -i --rm \
    -e NETCLAW_PROVIDER=claude-code \
    -e ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY" \
    netclaw-agent:latest
```

**Expected:** JSONL output on stdout with `status: "success"` and a `result` field containing the agent response. Exit code 0.

### 2.3 Empty stdin

**Steps:**
```bash
echo -n "" | $CONTAINER_RUNTIME run -i --rm netclaw-agent:latest
```

**Expected:** Error message on stderr: "No input received on stdin." Exit code 1.

### 2.4 Malformed JSON stdin

**Steps:**
```bash
echo 'not json' | $CONTAINER_RUNTIME run -i --rm netclaw-agent:latest
```

**Expected:** Error message on stderr about deserialization failure. Exit code 1.

### 2.5 Unknown provider

**Steps:**
```bash
echo '{"prompt":"hi","sessionId":null,"groupFolder":"test","chatJid":"terminal@local","isMain":false,"isScheduledTask":false,"assistantName":"NetClaw"}' | \
  $CONTAINER_RUNTIME run -i --rm -e NETCLAW_PROVIDER=unknown netclaw-agent:latest
```

**Expected:** Error on stderr or error JSONL on stdout. Exit code 1.

### 2.6 Session resumption flag (Copilot)

**Steps:**
```bash
echo '{"prompt":"continue","sessionId":"sess-123","groupFolder":"test","chatJid":"terminal@local","isMain":false,"isScheduledTask":false,"assistantName":"NetClaw"}' | \
  $CONTAINER_RUNTIME run -i --rm -e NETCLAW_PROVIDER=copilot netclaw-agent:latest 2>&1
```

**Expected:** The runner passes `--session-id sess-123` to the Copilot CLI (visible in error output from the stub or in process arguments if debugging).

### 2.7 Session resumption flag (Claude Code)

**Steps:**
```bash
echo '{"prompt":"continue","sessionId":"sess-456","groupFolder":"test","chatJid":"terminal@local","isMain":false,"isScheduledTask":false,"assistantName":"NetClaw"}' | \
  $CONTAINER_RUNTIME run -i --rm -e NETCLAW_PROVIDER=claude-code netclaw-agent:latest 2>&1
```

**Expected:** The runner passes `--resume sess-456` to the Claude CLI.

### 2.8 JSONL output format

**Steps:** Use test 2.2 or any successful execution.

**Expected:** Each stdout line is a valid JSON object parseable by `jq`. No pretty-printed JSON (no newlines within a single output object). Example:

```bash
echo '{"prompt":"hi","sessionId":null,"groupFolder":"test","chatJid":"terminal@local","isMain":false,"isScheduledTask":false,"assistantName":"NetClaw"}' | \
  $CONTAINER_RUNTIME run -i --rm -e NETCLAW_PROVIDER=claude-code -e ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY" netclaw-agent:latest | \
  jq -c .
```

No `jq` parse errors.

---

## 3. Credential Proxy

### 3.1 Proxy starts and binds

**Steps:**
```bash
dotnet run --project src/FireLakeLabs.NetClaw.Host
```

**Expected:** Log line: `Starting credential proxy on http://127.0.0.1:3001/`

### 3.2 Claude request — API key injection

**Preconditions:** Proxy running, `ANTHROPIC_API_KEY` set in host environment.

**Steps:**
```bash
curl -X POST http://127.0.0.1:3001/v1/messages \
  -H "Content-Type: application/json" \
  -H "anthropic-version: 2023-06-01" \
  -d '{"model":"claude-sonnet-4-20250514","max_tokens":10,"messages":[{"role":"user","content":"Say hi"}]}'
```

**Expected:** The proxy detects this as a Claude request (path contains `/v1/messages`), injects the real `x-api-key` header with the value from `ANTHROPIC_API_KEY`, forwards to `https://api.anthropic.com`, and returns the upstream response. Verify the response is a valid Claude API response (not a 401).

### 3.3 Copilot request — token injection

**Preconditions:** Proxy running, `GITHUB_TOKEN` set in host environment.

**Steps:**
```bash
curl -X POST http://127.0.0.1:3001/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"hi"}]}'
```

**Expected:** The proxy detects this as a Copilot request (no Claude path indicators), injects `Authorization: Bearer {GITHUB_TOKEN}`, forwards to `https://api.githubcopilot.com`, returns the upstream response.

### 3.4 Missing credentials — Claude

**Preconditions:** Proxy running, `ANTHROPIC_API_KEY` not set.

**Steps:**
```bash
curl -v http://127.0.0.1:3001/v1/messages \
  -H "Content-Type: application/json" \
  -d '{}'
```

**Expected:** Request forwarded without `x-api-key`. Upstream returns 401. The proxy relays the 401 back.

### 3.5 Upstream unreachable

**Preconditions:** Proxy running with a non-routable upstream URL.

**Configuration override:**
```json
{ "NetClaw:CredentialProxy:ClaudeUpstreamUrl": "http://192.0.2.1:1" }
```

**Steps:** Send a Claude request to the proxy.

**Expected:** Proxy returns HTTP 502 with a JSON error body `{"error": "..."}`.

### 3.6 Hop-by-hop headers stripped

**Steps:**
```bash
curl -v http://127.0.0.1:3001/v1/messages \
  -H "Connection: keep-alive" \
  -H "Transfer-Encoding: chunked" \
  -H "Content-Type: application/json" \
  -d '{}'
```

**Expected:** The `Connection` and `Transfer-Encoding` headers are not forwarded to the upstream API (verify via proxy debug logging or a request capture proxy like mitmproxy).

### 3.7 Custom port

**Configuration:**
```json
{ "NetClaw:CredentialProxy:Port": 4500 }
```

**Expected:** Proxy binds to port 4500. Requests to `http://127.0.0.1:4500/` are handled.

### 3.8 Proxy stops on host shutdown

**Steps:**
1. Start host.
2. Verify proxy is listening (curl test).
3. Stop host (Ctrl+C).

**Expected:** Log line: `Stopping credential proxy.` and `Credential proxy stopped.` Port is released.

---

## 4. ContainerizedAgentEngine — Host-Side Execution

### 4.1 Single-shot execution — end to end

**Preconditions:** Container image built, credentials configured, host running.

**Steps:**
1. Register a group and send a message via terminal channel.
2. Observe host logs for container spawn.

**Expected:**
- Host logs show container runtime invocation with correct image, mounts, and env vars.
- Container completes, JSONL output is parsed.
- Response is routed back to the terminal channel.

### 4.2 Timeout enforcement

**Configuration:**
```json
{ "NetClaw:ContainerRuntime:ExecutionTimeout": "00:00:05" }
```

**Steps:** Send a prompt that causes the agent to work for longer than 5 seconds.

**Expected:**
- After 5 seconds, host logs a warning: `Container {name} timed out after 00:00:05.`
- Container is stopped (for example, `$CONTAINER_RUNTIME stop`).
- Error result returned: "Container execution timed out."

### 4.3 Mount verification — group directory

**Steps:**
1. Register a group with folder `team`.
2. Send a prompt asking the agent to create a file in its workspace.
3. Check host filesystem at the group directory.

**Expected:** The file created by the agent appears in the host's group directory (confirming the RW mount works).

### 4.4 Mount verification — global directory is read-only

**Preconditions:** Non-main group.

**Steps:** Send a prompt asking the agent to write a file to `/workspace/global/`.

**Expected:** Write fails inside the container (read-only mount). Agent reports an error or the write is rejected.

### 4.5 Mount verification — project root is read-only (main group)

**Preconditions:** Main group.

**Steps:** Send a prompt asking the agent to write to `/workspace/project/`.

**Expected:** Write fails (read-only mount).

### 4.6 Mount verification — project root not visible to non-main groups

**Preconditions:** Non-main group.

**Steps:** Send a prompt asking the agent to list `/workspace/project/`.

**Expected:** Directory is empty or does not contain project files (not mounted for non-main groups).

### 4.7 Session directory persistence

**Steps:**
1. Send a first prompt that creates a session.
2. Note the session ID from the response.
3. Send a follow-up prompt to the same group.

**Expected:** The session directory on the host contains session state files that survive across container invocations. The second invocation passes `--session-id` or `--resume` to the CLI.

### 4.8 Container naming

**Steps:** Observe the `$CONTAINER_RUNTIME ps` output while an agent is executing.

**Expected:** Container named with pattern `netclaw-{provider}-{groupFolder}` (e.g., `netclaw-copilot-team`).

### 4.9 Non-zero exit code handling

**Steps:** Trigger a scenario where the CLI exits non-zero (e.g., invalid model name, bad credentials).

**Expected:** Host receives an error result with the container's stderr content. No crash or hang.

### 4.10 Malformed JSONL line in output

**Steps:** (Requires a modified container or test harness that injects a non-JSON line into stdout.)

**Expected:** Host logs a warning about the unparseable line. Execution continues and later valid JSONL lines are still processed.

---

## 5. Interactive Sessions

### 5.1 Multi-turn conversation

**Preconditions:** Interactive session enabled for the group.

**Steps:**
1. Send an initial message to start an interactive session.
2. Wait for the first agent response.
3. Send a follow-up message.
4. Wait for the second response.

**Expected:**
- First response arrives through the channel.
- Follow-up message is written as a JSON file to the IPC input directory.
- Agent picks up the follow-up and responds.
- Second response arrives through the channel.

### 5.2 Close signal

**Steps:**
1. Start an interactive session.
2. Close the session (via `/close` command or group input close).

**Expected:**
- A `_close` marker file is written to the IPC input directory.
- The agent runner detects the close signal and exits.
- Host session is marked as closed.

### 5.3 Idle timeout

**Configuration:** Set a short interactive idle timeout (e.g., 10 seconds).

**Steps:**
1. Start an interactive session.
2. Wait for the first response.
3. Do not send any follow-up for the idle timeout duration.

**Expected:**
- After the idle timeout, the session closes automatically.
- The container process is stopped.

### 5.4 Rapid follow-up messages

**Steps:**
1. Start an interactive session.
2. Send 3 follow-up messages in rapid succession (within 1 second).

**Expected:** All 3 messages are written as separate IPC files with monotonically increasing filenames. The agent processes them in order (alphabetical filename ordering).

---

## 6. Provider Selection

### 6.1 Copilot provider

**Configuration:**
```json
{ "NetClaw:AgentRuntime:DefaultProvider": "Copilot" }
```

**Steps:** Send a message.

**Expected:** Container is started with `-e NETCLAW_PROVIDER=copilot`. Host logs confirm Copilot provider.

### 6.2 Claude Code provider

**Configuration:**
```json
{ "NetClaw:AgentRuntime:DefaultProvider": "ClaudeCode" }
```

**Steps:** Send a message.

**Expected:** Container is started with `-e NETCLAW_PROVIDER=claude-code`. Host logs confirm Claude Code provider.

### 6.3 Copilot model override

**Configuration:**
```json
{ "NetClaw:AgentRuntime:CopilotModel": "o4-mini" }
```

**Steps:** Send a message with Copilot provider.

**Expected:** Container is started with `-e NETCLAW_COPILOT_MODEL=o4-mini`. The Copilot CLI receives `--model o4-mini`.

### 6.4 Copilot reasoning effort

**Configuration:**
```json
{ "NetClaw:AgentRuntime:CopilotReasoningEffort": "high" }
```

**Steps:** Send a message with Copilot provider.

**Expected:** Container is started with `-e NETCLAW_COPILOT_REASONING_EFFORT=high`. The Copilot CLI receives `--reasoning-effort high`.

---

## 7. Credential Isolation

### 7.1 Container cannot read real API keys

**Steps:**
1. Set `ANTHROPIC_API_KEY` on the host.
2. Start a container execution.
3. Inside the container, check the env: the agent should see `ANTHROPIC_API_KEY=placeholder` (or not at all).

**Verification approach:**
```bash
echo '{"prompt":"echo $ANTHROPIC_API_KEY","sessionId":null,"groupFolder":"test","chatJid":"terminal@local","isMain":false,"isScheduledTask":false,"assistantName":"NetClaw"}' | \
  $CONTAINER_RUNTIME run -i --rm \
    -e NETCLAW_PROVIDER=copilot \
    -e ANTHROPIC_API_KEY=placeholder \
    netclaw-agent:latest 2>&1
```

**Expected:** The container only sees `placeholder`, never the real key.

### 7.2 Proxy injects correct credentials per provider

**Steps:**
1. Send a Claude request through the proxy — verify `x-api-key` is injected.
2. Send a Copilot request through the proxy — verify `Authorization: Bearer` is injected.

**Expected:** Each provider's API accepts the request (no 401 errors). The container itself never receives the real credential.

### 7.3 Host environment does not leak into container

**Steps:**
1. Set a sensitive env var on the host (e.g., `SECRET_VALUE=supersecret`).
2. Start a container execution.
3. Have the agent try to read `SECRET_VALUE`.

**Expected:** `SECRET_VALUE` is not accessible inside the container. Only explicitly passed `-e` variables are visible.

---

## 8. Error Recovery

### 8.1 Container runtime not available

**Steps:** Set `ContainerRuntime:RuntimeBinary` to a nonexistent binary (e.g., `no-such-runtime`).

**Expected:** Host returns an error result with a descriptive message (e.g., "No such file or directory"). No crash or hang.

### 8.2 Container image not found

**Steps:** Set `ContainerRuntime:ImageName` to `nonexistent-image:latest`.

**Expected:** Docker returns an error about the image not being found. Host wraps this as an error result.

### 8.3 Port conflict on credential proxy

**Steps:**
1. Start another process listening on port 3001.
2. Start the host.

**Expected:** Host startup fails with a clear error about the port being in use. The host does not start.

### 8.4 Orphan container from previous run

**Steps:**
1. Start a long-running container execution.
2. Kill the host process (SIGKILL).
3. Verify the container is still running (`$CONTAINER_RUNTIME ps`).
4. Restart the host.
5. Trigger execution for the same group.

**Expected:** The new execution either reuses the container name (causing a conflict error) or the next execution completes. Document the current behavior — orphan cleanup is listed as a future improvement.

### 8.5 Disk full during IPC write

**Steps:** Fill the IPC directory's filesystem.

**Expected:** The `IpcToolWriter` propagates the IOException. The agent runner catches it and returns an error. No silent data loss.

---

## 9. Configuration Validation

### 9.1 Empty image name

**Configuration:**
```json
{ "NetClaw:ContainerRuntime:ImageName": "" }
```

**Expected:** Host startup fails with validation error from `ContainerRuntimeOptions.Validate()`.

### 9.2 Negative execution timeout

**Configuration:**
```json
{ "NetClaw:ContainerRuntime:ExecutionTimeout": "-00:00:01" }
```

**Expected:** Validation error at startup.

### 9.3 Invalid auth mode

**Configuration:**
```json
{ "NetClaw:CredentialProxy:AuthMode": "invalid" }
```

**Expected:** Validation error at startup. Only `api-key` and `oauth` are accepted.

### 9.4 Port out of range

**Configuration:**
```json
{ "NetClaw:CredentialProxy:Port": 99999 }
```

**Expected:** Validation error at startup.

---

## 10. Automated Test Coverage

These tests already exist in the codebase and can be verified with `dotnet test`:

| Test Class | File | What It Covers |
|---|---|---|
| `ContainerizedAgentEngineTests` | `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Runtime/ContainerizedAgentEngineTests.cs` | Mount construction (group, session, IPC, global RO, project main-only, Claude session path), options validation |
| `AgentRuntimeServicesTests` | `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Runtime/AgentRuntimeServicesTests.cs` | Runtime wiring with `IContainerExecutionService` |
| `OptionsTests` | `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Configuration/OptionsTests.cs` | Configuration parsing and validation |
| `ProgramTests` | `tests/FireLakeLabs.NetClaw.Host.Tests/ProgramTests.cs` | DI registration of all services including `ICodingAgentEngine` |
| `EndToEndIntegrationTests` | `tests/FireLakeLabs.NetClaw.IntegrationTests/EndToEndIntegrationTests.cs` | Full host startup, message routing, scheduling with containerized engine |

Run the full suite:
```bash
dotnet test
```

---

## 11. Known Gaps and Future Test Work

| Gap | Description |
|---|---|
| **Copilot CLI stub** | The Dockerfile installs a stub `copilot` script that always fails. Copilot end-to-end tests require a real CLI installation. |
| **MaxOutputBytes** | Declared in `ContainerRuntimeOptions` but not enforced in the current code. A test for output truncation would fail today. |
| **AuthMode oauth** | The `oauth` auth mode is accepted in validation but not implemented in `CredentialProxyService.InjectCredentials()`. |
| **Orphan cleanup** | No automatic cleanup of containers left behind by a killed host process. |
| **Concurrent containers** | No tests for multiple containers running simultaneously for different groups. |
| **Large payloads** | No test for very large stdin payloads or very large stdout responses. |
| **Network partitions** | No test for what happens if the credential proxy becomes unreachable mid-execution. |
| **Host wiring drift risk** | This plan assumes host DI wiring includes containerized runtime services and `CredentialProxyWorker`. If those registrations are absent in your branch, sections 3-9 will fail regardless of container image health. |
