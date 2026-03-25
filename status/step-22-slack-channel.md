# Step 22: Slack Channel Adapter

## Changes Made

- Added a real Slack channel adapter built on Slack Socket Mode and the Slack Web API.
- Added `SlackChannelOptions` for bot/app tokens, mention normalization, working-indicator text, and default thread reply behavior.
- Added a `SlackSocketModeClient` with Web API calls for `auth.test`, `apps.connections.open`, `conversations.info`, `chat.postMessage`, `chat.update`, and `chat.delete`.
- Implemented `SlackChannel` so inbound Slack messages flow through the existing channel worker, Slack mentions normalize to the assistant trigger, and outbound replies reuse the visible working-indicator placeholder.
- Wired generic typing/working-indicator calls into the .NET message loop so channels can surface active processing state without channel-specific host hacks.
- Added unit coverage for Slack message ingestion, placeholder posting/updating, and placeholder cleanup.
- Added a host-level integration test for the Slack channel with a fake socket-mode client.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Application/Execution/GroupMessageProcessorService.cs`
  - `src/FireLakeLabs.NetClaw.Application/Execution/InboundMessagePollingService.cs`
  - `src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Channels/SlackChannel.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Channels/SlackSocketContracts.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Channels/SlackSocketModeClient.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Configuration/SlackChannelOptions.cs`
  - `README.md`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Application.Tests/Execution/GroupMessageProcessorServiceTests.cs`
  - `tests/FireLakeLabs.NetClaw.Application.Tests/Execution/InboundMessagePollingServiceTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Channels/SlackChannelTests.cs`
  - `tests/FireLakeLabs.NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Verification

- `dotnet test tests/FireLakeLabs.NetClaw.Application.Tests/FireLakeLabs.NetClaw.Application.Tests.csproj --filter "FullyQualifiedName~InboundMessagePollingServiceTests|FullyQualifiedName~GroupMessageProcessorServiceTests"`
- `dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj --filter SlackChannelTests`
- `dotnet test tests/FireLakeLabs.NetClaw.IntegrationTests/FireLakeLabs.NetClaw.IntegrationTests.csproj --filter SlackChannel_IngestsInboundMessageAndUpdatesWorkingIndicatorWithReply`

## Pending For Live Smoke

- Create the Slack app and install it into the target workspace.
- Provide the bot token and app-level Socket Mode token.
- Confirm the final app scopes and event subscriptions before the live Slack smoke test.