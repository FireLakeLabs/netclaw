#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/scripts/host-cleanup.sh"
DOTNET_BIN="${DOTNET_BIN:-}"

if [[ -z "$DOTNET_BIN" ]]; then
	if command -v dotnet >/dev/null 2>&1; then
		DOTNET_BIN="$(command -v dotnet)"
	elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
		DOTNET_BIN="$HOME/.dotnet/dotnet"
	else
		echo "Unable to locate dotnet. Set DOTNET_BIN or add dotnet to PATH." >&2
		exit 1
	fi
fi

PROJECT_ROOT="${NETCLAW_PROJECT_ROOT:-$HOME/.netclaw}"
CHAT_JID="${NETCLAW_CHAT_JID:-}"
CHAT_NAME="${NETCLAW_CHAT_NAME:-Slack Chat}"
GROUP_FOLDER="${NETCLAW_GROUP_FOLDER:-slack-chat}"
AGENT_TRIGGER="${NETCLAW_AGENT_TRIGGER:-@assistant}"
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
export NETCLAW_HOST_PROCESS_MATCH="FireLakeLabs.NetClaw.Host"

# Initialize project directory and config if needed
"$DOTNET_BIN" run --disable-build-servers --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Setup" -- --step init

register_args=(
	"$DOTNET_BIN" run --disable-build-servers --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Setup" -- --step register
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

host_command=(
	env
	NetClaw__Channels__Slack__Enabled=true
	NetClaw__Channels__Slack__BotToken="$SLACK_BOT_TOKEN"
	NetClaw__Channels__Slack__AppToken="$SLACK_APP_TOKEN"
	"$DOTNET_BIN"
	run
	--disable-build-servers
	--project
	"$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Host"
)
host_command+=("$@")

run_host_with_cleanup "${host_command[@]}"

	