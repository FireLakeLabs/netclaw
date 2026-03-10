# Step 05: Application Services

## Changes Made

- Added XML message formatting for inbound history rendering and outbound response cleanup.
- Added outbound channel routing that resolves the connected channel owning a target chat JID.
- Added a per-group execution queue with concurrency limits, task deduplication, and retry-aware message processing.
- Added task scheduling orchestration for once, interval, and cron-based tasks, including run log persistence and task state transitions.
- Added IPC command processing for message forwarding, task creation, and main-group-only group registration.
- Added the `Cronos` package to support cron expression evaluation in the Application layer.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Application/NetClaw.Application.csproj`
  - `src/NetClaw.Application/Formatting/*`
  - `src/NetClaw.Application/Routing/*`
  - `src/NetClaw.Application/Execution/*`
  - `src/NetClaw.Application/Scheduling/*`
  - `src/NetClaw.Application/Ipc/*`
- Tests:
  - `tests/NetClaw.Application.Tests/AssemblySmokeTests.cs`
  - `tests/NetClaw.Application.Tests/Formatting/*`
  - `tests/NetClaw.Application.Tests/Routing/*`
  - `tests/NetClaw.Application.Tests/Execution/*`
  - `tests/NetClaw.Application.Tests/Scheduling/*`
  - `tests/NetClaw.Application.Tests/Ipc/*`

## Unit Tests And Integration Tests Added

- `XmlMessageFormatterTests`
  - verifies XML escaping and timezone context rendering
  - verifies internal tag stripping for outbound text
- `ChannelOutboundRouterTests`
  - verifies routing through the owning connected channel
  - verifies failure when no eligible channel exists
- `GroupExecutionQueueTests`
  - verifies queued message processing
  - verifies task deduplication by task identifier
  - verifies message sending only during active non-task execution
- `TaskSchedulerServiceTests`
  - verifies interval scheduling avoids drift
  - verifies due task execution updates status and appends run logs
- `IpcCommandProcessorTests`
  - verifies main-group-only registration behavior
  - verifies authorized task creation
  - verifies unauthorized message forwarding is blocked

## Verification Performed

- `dotnet build tests/NetClaw.Application.Tests/NetClaw.Application.Tests.csproj`
- `dotnet test tests/NetClaw.Application.Tests/NetClaw.Application.Tests.csproj`

Result: 15 xUnit tests passed.

## Deferred Items And Known Gaps

- The Application services are not yet wired into dependency injection or host startup.
- Scheduler execution currently depends on delegate-based execution and message sending hooks that will be bound in the Host layer.
- IPC file discovery and transport plumbing are still deferred to later host/setup work.