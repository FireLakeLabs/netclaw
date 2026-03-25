#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PROJECT_ROOT="${NETCLAW_PROJECT_ROOT:-$HOME/.netclaw}"
CHAT_JID="${NETCLAW_CHAT_JID:-}"
CHAT_NAME="${NETCLAW_CHAT_NAME:-Slack Chat}"
GROUP_FOLDER="${NETCLAW_GROUP_FOLDER:-slack-chat}"
AGENT_TRIGGER="${NETCLAW_AGENT_TRIGGER:-@Andy}"
SLACK_BOT_TOKEN="${NETCLAW_SLACK_BOT_TOKEN:-}"
SLACK_APP_TOKEN="${NETCLAW_SLACK_APP_TOKEN:-}"
REQUIRE_TRIGGER="${NETCLAW_REQUIRE_TRIGGER:-false}"

if [[ -z "$CHAT_JID" ]]; then
	echo "NETCLAW_CHAT_JID is required and should be the Slack conversation ID (for example C..., G..., or D...)." >&2
	exit 1
fi

if [[ -z "$SLACK_BOT_TOKEN" ]]; then
	echo "NETCLAW_SLACK_BOT_TOKEN is required." >&2
	exit 1
fi

if [[ -z "$SLACK_APP_TOKEN" ]]; then
	echo "NETCLAW_SLACK_APP_TOKEN is required." >&2
	exit 1
fi

export NETCLAW_PROJECT_ROOT="$PROJECT_ROOT"

# Initialize project directory and config if needed
dotnet run --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Setup" -- --step init

register_args=(
	dotnet run --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Setup" -- --step register
	--jid "$CHAT_JID"
	--name "$CHAT_NAME"
	--trigger "$AGENT_TRIGGER"
	--folder "$GROUP_FOLDER"
)

if [[ "${REQUIRE_TRIGGER,,}" == "true" ]]; then
	trigger_mode="required ($AGENT_TRIGGER)"
else
	register_args+=(--no-trigger-required)
	trigger_mode="disabled"
fi

"${register_args[@]}"

printf '=== NETCLAW SLACK ===\n'
printf 'PROJECT_ROOT: %s\n' "$PROJECT_ROOT"
printf 'CONFIG: %s/appsettings.json\n' "$PROJECT_ROOT"
printf 'CHAT_JID: %s\n' "$CHAT_JID"
printf 'CHAT_NAME: %s\n' "$CHAT_NAME"
printf 'GROUP_FOLDER: %s\n' "$GROUP_FOLDER"
printf 'TRIGGER_MODE: %s\n' "$trigger_mode"
printf 'Press Ctrl+C to stop.\n'
printf '=== END ===\n'

# Secrets stay as env vars — everything else comes from appsettings.json
exec env \
	NetClaw__Channels__Slack__Enabled=true \
	NetClaw__Channels__Slack__BotToken="$SLACK_BOT_TOKEN" \
	NetClaw__Channels__Slack__AppToken="$SLACK_APP_TOKEN" \
	dotnet run --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Host" "$@"
	