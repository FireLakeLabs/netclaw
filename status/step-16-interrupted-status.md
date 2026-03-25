# Step 16: Interrupted Run Status

## Changes Made

- Added a first-class `Interrupted` value to `ContainerRunStatus` so preempted interactive sessions no longer have to be reported as generic runtime errors.
- Updated `CopilotCodingAgentEngine` so an explicit interactive-session close now completes with `Interrupted` instead of `Error` while still preserving the interruption reason.
- Tightened `GroupMessageProcessorService` acknowledgment semantics so only `Success` advances the per-group agent cursor; interrupted or failed runs now leave the inbound turn retryable.
- Added domain and application coverage for the new status, including validation that interrupted task run logs do not require an error payload.
- Updated runtime tests to assert that canceled in-flight Copilot prompts surface as `Interrupted`.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Domain/Enums/ContainerRunStatus.cs`
  - `src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/CopilotCodingAgentEngine.cs`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Domain.Tests/Entities/StoredMessageAndRunLogTests.cs`
  - `tests/FireLakeLabs.NetClaw.Application.Tests/Execution/GroupMessageProcessorServiceTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Runtime/AgentRuntimeServicesTests.cs`

## Unit Tests And Integration Tests Added

- `StoredMessageAndRunLogTests`
  - verifies interrupted task run logs are valid without an error payload
- `GroupMessageProcessorServiceTests`
  - verifies interrupted runtime completion does not advance the cursor and remains retryable
- `AgentRuntimeServicesTests`
  - verifies interactive prompt cancellation now returns `Interrupted`

## Verification Performed

- `dotnet test tests/FireLakeLabs.NetClaw.Domain.Tests/FireLakeLabs.NetClaw.Domain.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Application.Tests/FireLakeLabs.NetClaw.Application.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj`
- `dotnet test`

Result: 115 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- Scheduled task execution still records only success or error outcomes; interrupted task runs are not yet surfaced by the scheduler because task preemption semantics have not been added there.
- Provider stream-event translation still uses `Running` for non-error incremental events; there is no dedicated stream event for an interruption boundary.
- Real authenticated Copilot validation of interrupted-session behavior is still pending beyond fake-adapter coverage.