# Step 21: Terminal Response Formatting

## Changes Made

- Updated the terminal channel so assistant responses reclaim the active prompt line instead of being printed inline after `you> `.
- Redrew the input prompt after each assistant response so the next input still has a visible prompt.
- Added focused coverage for the exact prompt/response rendering sequence.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Infrastructure/Channels/TerminalChannel.cs`
- Tests:
  - `tests/NetClaw.Infrastructure.Tests/Channels/TerminalChannelTests.cs`
  - `tests/NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Verification

- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --filter TerminalChannelTests`
- `dotnet test tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj --filter TerminalChannel_ProcessesConsoleInputAndWritesReply`