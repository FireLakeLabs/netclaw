# Step 17: Reference File Channel

## Changes Made

- Extended the channel contract with inbound polling support so concrete channel adapters can deliver inbound messages and chat metadata into the host instead of existing only as outbound senders.
- Added `ChannelIngressService` to persist inbound channel messages and chat metadata through the existing repository layer.
- Added a file-backed reference channel in Infrastructure that reads inbound JSON envelopes from an inbox directory, writes outbound replies to an outbox directory, tracks owned chats, and quarantines invalid payloads into an error directory.
- Added `ChannelWorker` to the host so channels are connected, optionally synced, and polled continuously as part of the hosted-service lifecycle.
- Updated host DI/configuration to compose registered channels dynamically and added config for channel polling plus the optional reference file channel.
- Added end-to-end coverage proving inbound file message ingestion, DB persistence, message-loop processing, and outbound file reply generation through the real host pipeline.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Domain/Contracts/Channels/ChannelContracts.cs`
  - `src/NetClaw.Application/Channels/ChannelIngressService.cs`
  - `src/NetClaw.Infrastructure/Channels/ReferenceFileChannel.cs`
  - `src/NetClaw.Infrastructure/Configuration/ChannelWorkerOptions.cs`
  - `src/NetClaw.Infrastructure/Configuration/ReferenceFileChannelOptions.cs`
  - `src/NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `src/NetClaw.Host/Services/ChannelWorker.cs`
- Tests:
  - `tests/NetClaw.Application.Tests/Channels/ChannelIngressServiceTests.cs`
  - `tests/NetClaw.Infrastructure.Tests/Channels/ReferenceFileChannelTests.cs`
  - `tests/NetClaw.Host.Tests/ChannelWorkerTests.cs`
  - `tests/NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Unit Tests And Integration Tests Added

- `ChannelIngressServiceTests`
  - verifies inbound metadata and messages are persisted correctly
- `ReferenceFileChannelTests`
  - verifies valid inbox files emit metadata/messages and are moved to `processed`
  - verifies outbound replies are written as JSON envelopes in `outbox`
- `ChannelWorkerTests`
  - verifies channels are connected, synced, polled, and disconnected through the host worker lifecycle
- `EndToEndIntegrationTests`
  - verifies the reference file channel can ingest an inbound message and emit an outbound reply through the full host pipeline

## Verification Performed

- `dotnet test tests/NetClaw.Application.Tests/NetClaw.Application.Tests.csproj`
- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj`
- `dotnet test tests/NetClaw.Host.Tests/NetClaw.Host.Tests.csproj`
- `dotnet test tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj`
- `dotnet test`

Result: 121 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- The reference file channel is intentionally a single-host development/reference adapter; it is not a production messaging integration.
- Channel ownership remains adapter-defined; there is not yet a persisted channel-to-chat registry for multi-channel conflict resolution.
- Live platform-specific channel adapters and a reusable migration template/package are still pending beyond this first concrete reference implementation.