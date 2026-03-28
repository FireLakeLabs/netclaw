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
CHAT_JID="${NETCLAW_CHAT_JID:-team@jid}"
CHAT_NAME="${NETCLAW_CHAT_NAME:-Team}"
GROUP_FOLDER="${NETCLAW_GROUP_FOLDER:-team}"
AGENT_TRIGGER="${NETCLAW_AGENT_TRIGGER:-@assistant}"
REQUIRE_TRIGGER="${NETCLAW_REQUIRE_TRIGGER:-false}"

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

example_prompt="What is the capital of Missouri?"

if [[ "${REQUIRE_TRIGGER,,}" == "true" ]]; then
	trigger_mode="required ($AGENT_TRIGGER)"
	example_prompt="$AGENT_TRIGGER $example_prompt"
else
	register_args+=(--no-trigger-required)
	trigger_mode="disabled"
fi

"${register_args[@]}"

printf '=== NETCLAW TERMINAL ===\n'
printf 'PROJECT_ROOT: %s\n' "$PROJECT_ROOT"
printf 'CONFIG: %s/appsettings.json\n' "$PROJECT_ROOT"
printf 'CHAT_JID: %s\n' "$CHAT_JID"
printf 'TRIGGER_MODE: %s\n' "$trigger_mode"
printf 'EXAMPLE: %s\n' "$example_prompt"
printf 'Press Ctrl+C to stop.\n'
printf '=== END ===\n'

host_command=(
	env
	NetClaw__Channels__Terminal__Enabled=true
	"$DOTNET_BIN"
	run
	--disable-build-servers
	--project
	"$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Host"
)
host_command+=("$@")

run_host_with_cleanup "${host_command[@]}"

	