# Step 12: Stream Event Bridge

## Changes Made

- Extended the container stream contract to preserve event kind information instead of collapsing all runtime callbacks into undifferentiated running output.
- Added `ContainerEventKind` so the host can distinguish session start, text deltas, completed assistant messages, tool events, idle notifications, and errors.
- Updated the agent runtime translation layer to map provider-neutral `AgentEventKind` values into the new container event kinds.
- Extended the group execution queue contract to expose idle notifications through the public interface instead of only on the concrete implementation.
- Updated the interactive group message processor to route completed assistant messages from stream callbacks immediately, suppress duplicate final-result sends, and notify the queue when the runtime reports idle.
- Added application-level coverage verifying streamed completed messages are routed once and idle signals are propagated to the queue.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Application/Execution/GroupMessageProcessorService.cs`
  - `src/NetClaw.Domain/Contracts/Containers/ContainerContracts.cs`
  - `src/NetClaw.Domain/Contracts/Services/ServiceContracts.cs`
  - `src/NetClaw.Domain/Enums/ContainerEventKind.cs`
  - `src/NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `src/NetClaw.Infrastructure/Runtime/Agents/NetClawAgentRuntime.cs`
- Tests:
  - `tests/NetClaw.Application.Tests/Execution/GroupMessageProcessorServiceTests.cs`
  - `tests/NetClaw.Application.Tests/Execution/InboundMessagePollingServiceTests.cs`

## Unit Tests And Integration Tests Added

- `GroupMessageProcessorServiceTests`
  - verifies streamed `MessageCompleted` events are routed without duplicating the final result
  - verifies streamed `Idle` events are forwarded to the queue
- Existing application, host, infrastructure, setup, and integration suites were rerun against the new stream contract shape.

## Verification Performed

- `dotnet test tests/NetClaw.Application.Tests/NetClaw.Application.Tests.csproj`
- `dotnet test NetClaw.slnx`

Result: 105 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- The host still ignores text-delta and reasoning-delta events to avoid fragmented outbound messages.
- Interactive follow-up input is still resumed as a new queued turn rather than being piped into a live running session.
- Sender allowlist filtering and richer trigger authorization are still pending beyond the current message-loop and stream bridge work.