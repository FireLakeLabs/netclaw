# Step 15: Interactive Session Preemption

## Changes Made

- Updated `GroupExecutionQueue` so queued tasks preempt active non-task interactive sessions immediately instead of waiting for a later idle notification before requesting shutdown.
- Prevented new live follow-up input from being written into a session once that group already has a pending task, so follow-up messages are no longer routed into a session that is being preempted.
- Extended the Copilot interactive session loop so `RequestClose` cancels an in-flight `SendPromptAsync` call via the SDK cancellation token, rather than only completing after the next idle/input boundary.
- Hardened the Copilot interactive session implementation against repeated close/dispose calls by guarding prompt cancellation and session disposal.
- Added unit coverage for queue-side preemption and runtime-side cancellation of a blocked interactive prompt.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Application/Execution/GroupExecutionQueue.cs`
  - `src/NetClaw.Infrastructure/Runtime/Agents/CopilotCodingAgentEngine.cs`
- Tests:
  - `tests/NetClaw.Application.Tests/Execution/GroupExecutionQueueTests.cs`
  - `tests/NetClaw.Infrastructure.Tests/Runtime/AgentRuntimeServicesTests.cs`

## Unit Tests And Integration Tests Added

- `GroupExecutionQueueTests`
  - verifies a queued task requests session close immediately while a message run is still active
  - verifies new follow-up input is rejected once a task is pending for that active session
- `AgentRuntimeServicesTests`
  - verifies a close request cancels a blocked Copilot interactive prompt and completes the session as interrupted

## Verification Performed

- `dotnet test tests/NetClaw.Application.Tests/NetClaw.Application.Tests.csproj`
- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj`
- `dotnet test`

Result: 113 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- Preemption currently maps interrupted interactive runs to an error result so the message turn can be retried safely; there is not yet a distinct interrupted status in the domain model.
- Runtime preemption is still session-wide; there is no provider-native "cancel current turn but keep session open" capability exposed yet.
- Real authenticated Copilot validation of in-flight interruption timing is still pending beyond fake-adapter coverage.