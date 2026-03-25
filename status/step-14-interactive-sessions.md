# Step 14: Interactive Sessions And Live Follow-Up Input

## Changes Made

- Added provider-neutral interactive session contracts so the host can keep an agent session alive after the initial prompt instead of treating every inbound turn as a fresh execution.
- Added `ActiveGroupSessionRegistry` and wired `GroupExecutionQueue` input handlers to it so queue follow-up messages now flow into a live session with synchronous `SendMessage` / `CloseInput` semantics.
- Updated inbound polling to detect when a group already has an active session, format only the newly eligible messages, send them directly into that session, and advance the per-group agent cursor immediately.
- Updated group message processing to start interactive runtime sessions, keep them registered while active, route streamed message-completed output, and continue forwarding idle events back to the queue.
- Extended the Copilot engine with a long-lived interactive loop that keeps the Copilot session adapter open across multiple prompts and closes it automatically after a configurable idle timeout.
- Added runtime support for interactive session startup in `NetClawAgentRuntime`, including provider capability checks and early session-state persistence.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Application/Execution/ActiveGroupSessionRegistry.cs`
  - `src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs`
  - `src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Agents/AgentContracts.cs`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Containers/ContainerContracts.cs`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Services/ServiceContracts.cs`
  - `src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Configuration/AgentRuntimeOptions.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/CopilotCodingAgentEngine.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/NetClawAgentRuntime.cs`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Application.Tests/Execution/GroupMessageProcessorServiceTests.cs`
  - `tests/FireLakeLabs.NetClaw.Application.Tests/Execution/InboundMessagePollingServiceTests.cs`
  - `tests/FireLakeLabs.NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Unit Tests And Integration Tests Added

- `InboundMessagePollingServiceTests`
  - verifies follow-up input is sent into an active session instead of enqueueing a new turn
  - verifies the per-group agent cursor advances immediately after a successful live send
- `GroupMessageProcessorServiceTests`
  - updated to run through the interactive runtime contract instead of the old one-shot execution shape
- `EndToEndIntegrationTests`
  - updated fake runtime coverage to exercise the host through the interactive session interface

## Verification Performed

- `dotnet test`

Result: 110 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- The Copilot interactive loop currently accepts sequential follow-up prompts only; it does not yet support provider-native partial-input streaming or cancellation of an in-flight prompt.
- Idle close behavior is runtime-owned through `NetClaw:AgentRuntime:InteractiveIdleTimeout`; there is not yet a separate per-group or per-provider override surface.
- Channel migration and concrete inbound channel adapters remain pending beyond the current host/runtime/message-loop core.