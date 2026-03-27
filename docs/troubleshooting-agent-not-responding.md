# Troubleshooting: Agent Stops Responding

This guide covers the most likely reasons NetClaw appears to accept inbound messages but never sends a reply, especially after the move to containerized execution and the later file-based persistence work.

It is written for operators trying to answer two questions quickly:

- Is the host still ingesting messages?
- If so, where is execution failing?

## Typical Symptoms

- Messages arrive through terminal, Slack, or reference-file, but no agent reply is produced.
- The same message appears to be retried repeatedly.
- Typing indicators may briefly appear and then disappear with no final response.
- Scheduled tasks or tool-driven follow-up actions stop working.
- Dashboard activity shows little or no completed agent output.

## Message Path To Keep In Mind

The high-level path is:

1. Channel ingests an inbound message.
2. Message is persisted in the file store.
3. The message loop selects messages that should be sent to the agent.
4. The group execution queue starts a run.
5. The host starts the agent inside a container.
6. The container runs the provider CLI.
7. Output is routed back to the owning channel.
8. Tool calls are written as IPC files and picked up by the host.

If replies stop appearing, the failure is usually in one of these areas:

- provider runtime inside the container
- container mount or path mismatch
- IPC tool output not landing where the host watches
- message selection or retry behavior hiding a repeated execution failure

## Most Likely Causes In This Codebase

### 1. Copilot auth or runtime setup is incomplete inside the container

This repo currently defaults to Copilot as the provider. Replies can still fail if the image is stale or the host is not providing a Copilot token into the container.

Relevant code:

- [container/Dockerfile](../container/Dockerfile#L37)
- [src/FireLakeLabs.NetClaw.AgentRunner/CopilotAgentProvider.cs](../src/FireLakeLabs.NetClaw.AgentRunner/CopilotAgentProvider.cs#L39)
- [src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs](../src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs#L269)

What to look for:

- The image was built before the real Copilot CLI was added.
- The configured default provider is still `copilot`.
- No `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, `GITHUB_TOKEN`, or `NetClaw:AgentRuntime:CopilotGitHubToken` is configured on the host.
- `CopilotUseLoggedInUser` is enabled even though the current containerized Copilot CLI path requires token auth.

What this looks like operationally:

- inbound messages are stored successfully
- the queue starts work
- the container exits with an error before any useful output is produced
- the host retries and then appears stuck

### 2. IPC path mismatch between host and container

The host mounts the group IPC directory directly at `/workspace/ipc`, and the watcher expects files under `/workspace/ipc/messages` and `/workspace/ipc/tasks` inside the container view.

Relevant code:

- [docs/containerized-agent-execution.md](./containerized-agent-execution.md#L134)
- [src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/ContainerizedAgentEngine.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/ContainerizedAgentEngine.cs#L259)
- [src/FireLakeLabs.NetClaw.Infrastructure/Ipc/FileSystemIpcWatcher.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Ipc/FileSystemIpcWatcher.cs#L77)
- [src/FireLakeLabs.NetClaw.AgentRunner/Program.cs](../src/FireLakeLabs.NetClaw.AgentRunner/Program.cs#L53)

If you're running an older image or stale published runner, watch for this mismatch:

- the runner builds IPC output paths as `/workspace/ipc/<group>/messages` and `/workspace/ipc/<group>/tasks`
- the host watcher expects the mounted group directory itself to contain `messages/` and `tasks/`

What breaks when this happens:

- tool-driven `send_message`
- scheduled task creation
- register-group requests
- close-input requests

The agent may still generate plain text output, but any workflow that depends on tool IPC can silently stop working.

### 3. Execution failures are easy to miss

The message processor catches most non-cancellation exceptions and returns `false` rather than surfacing the error directly to the channel.

Relevant code:

- [src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs](../src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs#L212)
- [src/FireLakeLabs.NetClaw.Application/Execution/GroupExecutionQueue.cs](../src/FireLakeLabs.NetClaw.Application/Execution/GroupExecutionQueue.cs#L242)

What this means:

- the queue retries failed runs with backoff
- users see no reply rather than a clear failure message
- the system can look wedged even when it is repeatedly failing and retrying correctly

### 4. File-based message filtering can hide some inbound prompts

The file-based message repository filters out bot messages and also filters messages whose content starts with `AssistantName:`.

Relevant code:

- [src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileMessageRepository.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileMessageRepository.cs#L131)

This is usually correct, but it can bite you if users commonly send prompts in a form like:

```text
Andy: summarize the last failure
```

If your assistant name is `Andy`, that message can be excluded from the agent prompt set.

## Fast Triage Order

Use this order so you do not waste time digging into the wrong layer.

### Step 1. Confirm inbound messages are still being persisted

NetClaw now uses file-based persistence for messages and router state.

Relevant paths:

- [src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileStoragePaths.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileStoragePaths.cs#L22)
- [src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileStoragePaths.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileStoragePaths.cs#L42)
- [src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs](../src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs#L100)

Check whether the affected chat is receiving new lines under:

```text
data/chats/<chatJid>/messages.jsonl
```

Interpretation:

- If new inbound messages are not being persisted, the problem is in channel ingestion.
- If they are being persisted, move on. The failure is downstream.

### Step 2. Check system health and queue state

The dashboard API already exposes the most useful runtime views.

Relevant endpoints:

- recent activity: [src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs](../src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs#L33)
- live queue state: [src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs](../src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs#L56)
- system health: [src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs](../src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs#L195)
- router state: [src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs](../src/FireLakeLabs.NetClaw.Dashboard/DashboardEndpoints.cs#L212)

You want to answer:

- Is the owning channel connected?
- Is the group active in the queue?
- Are there recent `Error` or `MessageCompleted` activity events?
- Is `last_agent_timestamp:<jid>` moving?

Interpretation:

- No recent agent events usually means the run is not starting.
- Repeated error events with no completion means the run is starting and failing.
- A busy queue with no completions suggests repeated retries.

### Step 3. Compare router state against stored messages

The message loop and message processor both depend on router state.

Relevant code:

- [src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs](../src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs#L53)
- [src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs](../src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs#L76)
- [src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs](../src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs#L87)
- [src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs](../src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs#L174)
- [src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs](../src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs#L197)

Focus on two keys:

- `last_timestamp`
- `last_agent_timestamp:<chatJid>`

Interpretation:

- `last_timestamp` moves, but `last_agent_timestamp:<jid>` does not: messages are being ingested, but agent execution is failing.
- neither key moves: the problem is before execution, usually channel ingestion or polling.
- `last_agent_timestamp:<jid>` moves only after successful runs: if it stalls while new messages keep arriving, expect retries and repeated failed runs.

### Step 4. Validate the container image and provider directly

If you are using the default provider, prove the image can actually run that provider.

Relevant code:

- [container/Dockerfile](../container/Dockerfile#L37)
- [src/FireLakeLabs.NetClaw.AgentRunner/CopilotAgentProvider.cs](../src/FireLakeLabs.NetClaw.AgentRunner/CopilotAgentProvider.cs#L39)
- [src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/ContainerizedAgentEngine.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/ContainerizedAgentEngine.cs#L192)

Checks:

1. Confirm which provider is configured.
2. Run the container image manually and verify the provider binary exists.
3. Check whether the provider command returns a real version.
4. Check whether a host token is being forwarded into the container.
5. Enable debug or trace logging so container stderr is visible.

Good result:

- the provider CLI exists and starts normally inside the image

Bad result:

- the provider command is missing
- the provider starts but immediately exits with an auth or config error

### Step 5. Verify IPC output lands in the right directory

For any workflow that uses tools, inspect IPC output paths.

Relevant code:

- [src/FireLakeLabs.NetClaw.AgentRunner/IpcToolWriter.cs](../src/FireLakeLabs.NetClaw.AgentRunner/IpcToolWriter.cs)
- [src/FireLakeLabs.NetClaw.AgentRunner/Program.cs](../src/FireLakeLabs.NetClaw.AgentRunner/Program.cs#L53)
- [src/FireLakeLabs.NetClaw.Infrastructure/Ipc/FileSystemIpcWatcher.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Ipc/FileSystemIpcWatcher.cs#L77)
- [src/FireLakeLabs.NetClaw.Infrastructure/Ipc/FileSystemIpcWatcher.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Ipc/FileSystemIpcWatcher.cs#L117)

Check:

- do files appear under the mounted group IPC directory's `messages/` and `tasks/` folders?
- do unexpected nested directories appear?
- are failed command files being moved to `data/ipc/errors/`?

Interpretation:

- files in `errors/` mean the host saw them but could not parse or authorize them
- files in the wrong nested directory mean the host never saw them at all

### Step 6. Check whether the queue is retrying a failing run

Relevant code:

- [src/FireLakeLabs.NetClaw.Application/Execution/GroupExecutionQueue.cs](../src/FireLakeLabs.NetClaw.Application/Execution/GroupExecutionQueue.cs#L242)

Behavior:

- failed message runs increment `RetryCount`
- the queue retries up to 5 times with exponential backoff

This is useful because it tells you the system is not deadlocked. It is failing in a repeatable way.

### Step 7. Check for message-format regressions caused by filtering

Relevant code:

- [src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileMessageRepository.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileMessageRepository.cs#L133)
- [src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileMessageRepository.cs](../src/FireLakeLabs.NetClaw.Infrastructure/Persistence/FileSystem/FileMessageRepository.cs#L138)

Questions to ask:

- Do users prefix prompts with `AssistantName:`?
- Did the assistant name change recently?
- Are inbound messages being marked as bot messages unexpectedly?

If yes, the message can be persisted but never included in the prompt sent to the agent.

## Recommended Logging During Investigation

Increase logging around:

- `FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents.ContainerizedAgentEngine`
- `FireLakeLabs.NetClaw.Application.Execution.GroupMessageProcessorService`
- `FireLakeLabs.NetClaw.Infrastructure.Ipc.FileSystemIpcWatcher`
- channel implementations for the affected channel

Why this matters:

- container stderr is only emitted through logger trace in the containerized engine
- the message processor otherwise returns `false` quietly on many failures

## What Good Looks Like

For a healthy message turn, you should be able to verify all of the following:

1. An inbound message appears in `data/chats/<jid>/messages.jsonl`.
2. The queue shows the group becoming active.
3. Recent activity shows stream events or at least a completed message.
4. The container provider starts without a stub or missing-binary error.
5. The outbound reply is routed back through the owning channel.
6. `last_agent_timestamp:<jid>` advances after the successful turn.

## What To Fix First If You Need A Fast Recovery

If the agent stopped responding after the container migration, prioritize in this order:

1. Verify the configured provider really exists in the container image.
2. Verify a host token is being forwarded into the Copilot container.
3. Fix IPC path mismatches between host mounts and container writes.
4. Improve logging around failed runs so silent failures become obvious.
5. Review message filtering if prompts are being persisted but excluded from execution.

## Related Documents

- [docs/containerized-agent-execution.md](./containerized-agent-execution.md)
- [docs/container-test-plan.md](./container-test-plan.md)
- [docs/file-based-persistence-plan.md](./file-based-persistence-plan.md)
- [docs/user-guide.md](./user-guide.md)