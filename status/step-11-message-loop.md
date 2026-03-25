# Step 11: Interactive Message Loop

## Changes Made

- Added an application-level inbound message polling service that reads newly stored messages from the database, advances the global `last_timestamp` cursor, and enqueues registered groups for processing.
- Added an application-level group message processor that loads pending per-group messages, enforces trigger checks for non-main groups, formats the prompt, executes the provider-neutral agent runtime, routes the normalized reply, and advances a per-group `last_agent_timestamp:{jid}` cursor on success.
- Added host-owned message loop configuration for poll interval and prompt-formatting timezone.
- Wired the host queue to the real group message processor instead of the previous no-op callback.
- Added a hosted message-loop worker so interactive DB-backed message processing runs continuously alongside the scheduler and IPC watcher.
- Added integration coverage proving that a stored inbound message can flow through the queue, execute a fake agent runtime, route a reply through a fake channel, and persist the per-group processing cursor.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs`
  - `src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs`
  - `src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `src/FireLakeLabs.NetClaw.Host/Services/MessageLoopWorker.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Configuration/MessageLoopOptions.cs`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Application.Tests/Execution/InboundMessagePollingServiceTests.cs`
  - `tests/FireLakeLabs.NetClaw.Application.Tests/Execution/GroupMessageProcessorServiceTests.cs`
  - `tests/FireLakeLabs.NetClaw.Host.Tests/ProgramTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Configuration/OptionsTests.cs`
  - `tests/FireLakeLabs.NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Unit Tests And Integration Tests Added

- `InboundMessagePollingServiceTests`
  - verifies triggered messages enqueue registered groups and update the global message cursor
  - verifies non-triggered messages for non-main groups are not enqueued
- `GroupMessageProcessorServiceTests`
  - verifies pending messages are formatted, executed, routed, and committed to the per-group cursor on success
  - verifies missing triggers leave the cursor untouched
  - verifies runtime failures return retryable failures without advancing state
- `ProgramTests`
  - verifies message-loop services and options are registered by the host
- `EndToEndIntegrationTests`
  - verifies a stored inbound message flows through the message loop, queue, fake runtime, and fake channel reply path

## Verification Performed

- `dotnet test tests/FireLakeLabs.NetClaw.Application.Tests/FireLakeLabs.NetClaw.Application.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Host.Tests/FireLakeLabs.NetClaw.Host.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.IntegrationTests/FireLakeLabs.NetClaw.IntegrationTests.csproj`
- `dotnet test FireLakeLabs.NetClaw.slnx`

Result: 104 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- The interactive path currently executes per queued turn using resumed sessions; it does not yet pipe follow-up input into a still-running live agent process.
- Streaming output is not yet surfaced incrementally to channels; outbound routing currently uses the final normalized result.
- Sender allowlist filtering and richer trigger authorization from NanoClaw have not yet been ported into the .NET message loop.