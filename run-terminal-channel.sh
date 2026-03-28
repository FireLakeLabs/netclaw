#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PROJECT_ROOT="${NETCLAW_PROJECT_ROOT:-$HOME/.netclaw}"
CHAT_JID="${NETCLAW_CHAT_JID:-team@jid}"
CHAT_NAME="${NETCLAW_CHAT_NAME:-Team}"
GROUP_FOLDER="${NETCLAW_GROUP_FOLDER:-team}"
AGENT_TRIGGER="${NETCLAW_AGENT_TRIGGER:-@assistant}"
REQUIRE_TRIGGER="${NETCLAW_REQUIRE_TRIGGER:-false}"

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

exec env \
	NetClaw__Channels__Terminal__Enabled=true \
	dotnet run --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Host" "$@"
	