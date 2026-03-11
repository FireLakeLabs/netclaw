# Step 23: Slack Launcher Helper

## Changes Made

- Added a root-level `run-slack-channel.sh` helper so Slack bot/app tokens can be supplied via environment variables instead of being pasted into the repo.
- Wrapped the existing register step plus host startup for the Slack channel, mirroring the terminal helper workflow.
- Documented the minimum environment variables required for a live Slack smoke run.

## Files And Projects Affected

- Production:
  - `run-slack-channel.sh`
  - `README.md`

## Verification

- `bash -n run-slack-channel.sh`

## Pending For Live Smoke

- Export the Slack bot token, app token, and target conversation ID.
- Start the helper script and send a test message from the registered Slack conversation.