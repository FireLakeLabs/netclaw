# Step 21: Terminal Response Formatting

## Changes Made

- Updated the terminal channel so assistant responses reclaim the active prompt line instead of being printed inline after `you> `.
- Redrew the input prompt after each assistant response so the next input still has a visible prompt.
- Added focused coverage for the exact prompt/response rendering sequence.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Infrastructure/Channels/TerminalChannel.cs`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Channels/TerminalChannelTests.cs`
  - `tests/FireLakeLabs.NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Verification

- `dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj --filter TerminalChannelTests`
- `dotnet test tests/FireLakeLabs.NetClaw.IntegrationTests/FireLakeLabs.NetClaw.IntegrationTests.csproj --filter TerminalChannel_ProcessesConsoleInputAndWritesReply`