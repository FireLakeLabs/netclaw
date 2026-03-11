# Step 28: Slack DM Status-Only Progress

## Changes Made

- Removed the placeholder typing fallback for Slack direct messages entirely so DM conversations only use native assistant status when available.
- Kept placeholder progress messages for non-DM Slack conversations, where threaded bot-message behavior is still the right fallback.
- Updated Slack channel tests to cover the new DM contract and to prevent regressions back to dual progress surfaces.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Infrastructure/Channels/SlackChannel.cs`
- Tests:
  - `tests/NetClaw.Infrastructure.Tests/Channels/SlackChannelTests.cs`

## Verification

- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --filter "SlackChannelTests"`