# Step 24: Slack Live Deserialization Fix

## Changes Made

- Fixed the Slack Web API envelope model so live Slack responses deserialize correctly under `System.Text.Json`.
- Added direct client tests for `auth.test` and `conversations.info` parsing using a stub HTTP handler.
- Kept the change scoped to the Slack API client rather than altering the channel or host behavior.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Infrastructure/Channels/SlackSocketModeClient.cs`
- Tests:
  - `tests/NetClaw.Infrastructure.Tests/Channels/SlackSocketModeClientTests.cs`

## Verification

- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --filter "SlackSocketModeClientTests|SlackChannelTests"`