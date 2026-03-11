# Step 13: Sender Allowlist Policy

## Changes Made

- Added host-owned sender policy support with NanoClaw-compatible `trigger` and `drop` modes.
- Added sender policy domain types and an application-facing `ISenderAuthorizationService` so authorization logic stays out of the message loop and group processor.
- Added a host path for `sender-allowlist.json` and loaded the policy during host initialization alongside the mount allowlist.
- Implemented infrastructure policy loading and evaluation, including `allow: "*"` and chat-specific allowed-sender lists.
- Integrated sender policy into inbound message polling so denied senders in `drop` mode do not enqueue groups.
- Integrated sender policy into group message processing so denied senders in `drop` mode are excluded from the prompt and only authorized senders can satisfy trigger checks.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Application/Execution/GroupMessageProcessorService.cs`
  - `src/NetClaw.Application/Execution/InboundMessagePollingService.cs`
  - `src/NetClaw.Domain/Contracts/Services/ServiceContracts.cs`
  - `src/NetClaw.Domain/Entities/SenderAllowlist.cs`
  - `src/NetClaw.Domain/Enums/SenderPolicyMode.cs`
  - `src/NetClaw.Host/Configuration/HostPathOptions.cs`
  - `src/NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `src/NetClaw.Host/Services/HostInitializationService.cs`
  - `src/NetClaw.Infrastructure/Security/MountAllowlistLoader.cs`
  - `src/NetClaw.Infrastructure/Security/SenderAllowlistService.cs`
- Tests:
  - `tests/NetClaw.Application.Tests/Execution/GroupMessageProcessorServiceTests.cs`
  - `tests/NetClaw.Application.Tests/Execution/InboundMessagePollingServiceTests.cs`
  - `tests/NetClaw.Infrastructure.Tests/Security/SenderAllowlistServiceTests.cs`

## Unit Tests And Integration Tests Added

- `SenderAllowlistServiceTests`
  - verifies missing config falls back to default `trigger` behavior
  - verifies chat-specific `drop` mode filters denied senders and blocks trigger eligibility
- `InboundMessagePollingServiceTests`
  - verifies denied senders in drop mode do not enqueue a group
- `GroupMessageProcessorServiceTests`
  - verifies denied senders in drop mode are excluded from the formatted prompt

## Verification Performed

- `dotnet test tests/NetClaw.Application.Tests/NetClaw.Application.Tests.csproj`
- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj`
- `dotnet test NetClaw.slnx`

Result: 109 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- True live follow-up input into a still-running agent session remains unimplemented; follow-up messages are still handled as later queued turns.
- Text-delta and reasoning-delta stream events are still intentionally suppressed from outbound routing to avoid fragmented replies.
- Channel migration and concrete inbound channel adapters are still pending beyond the current host/runtime/message-loop core.