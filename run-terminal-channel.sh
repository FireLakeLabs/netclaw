#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PROJECT_ROOT="${NETCLAW_PROJECT_ROOT:-/tmp/netclaw-terminal}"
CHAT_JID="${NETCLAW_CHAT_JID:-team@jid}"
CHAT_NAME="${NETCLAW_CHAT_NAME:-Team}"
GROUP_FOLDER="${NETCLAW_GROUP_FOLDER:-team}"
AGENT_TRIGGER="${NETCLAW_AGENT_TRIGGER:-@Andy}"
TERMINAL_SENDER="${NETCLAW_TERMINAL_SENDER:-terminal-user}"
TERMINAL_SENDER_NAME="${NETCLAW_TERMINAL_SENDER_NAME:-Terminal User}"
TERMINAL_CHAT_NAME="${NETCLAW_TERMINAL_CHAT_NAME:-Terminal Chat}"
TERMINAL_IS_GROUP="${NETCLAW_TERMINAL_IS_GROUP:-true}"
TERMINAL_INPUT_PROMPT="${NETCLAW_TERMINAL_INPUT_PROMPT:-you> }"
CHANNEL_POLL_INTERVAL="${NETCLAW_CHANNEL_POLL_INTERVAL:-00:00:01}"
MESSAGE_LOOP_POLL_INTERVAL="${NETCLAW_MESSAGE_LOOP_POLL_INTERVAL:-00:00:01}"
MESSAGE_LOOP_TIMEZONE="${NETCLAW_MESSAGE_LOOP_TIMEZONE:-UTC}"
COPILOT_USE_LOGGED_IN_USER="${NETCLAW_COPILOT_USE_LOGGED_IN_USER:-true}"
INTERACTIVE_IDLE_TIMEOUT="${NETCLAW_INTERACTIVE_IDLE_TIMEOUT:-00:00:30}"
DASHBOARD_ENABLED="${NETCLAW_DASHBOARD_ENABLED:-true}"
DASHBOARD_PORT="${NETCLAW_DASHBOARD_PORT:-5080}"
DASHBOARD_BIND_ADDRESS="${NETCLAW_DASHBOARD_BIND_ADDRESS:-0.0.0.0}"
REQUIRE_TRIGGER="${NETCLAW_REQUIRE_TRIGGER:-false}"

export NETCLAW_PROJECT_ROOT="$PROJECT_ROOT"

register_args=(
	dotnet run --project "$SCRIPT_DIR/src/NetClaw.Setup" -- --step register
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
printf 'CHAT_JID: %s\n' "$CHAT_JID"
printf 'TRIGGER_MODE: %s\n' "$trigger_mode"
printf 'EXAMPLE: %s\n' "$example_prompt"
printf 'DASHBOARD: %s (port %s, bind %s)\n' "$DASHBOARD_ENABLED" "$DASHBOARD_PORT" "$DASHBOARD_BIND_ADDRESS"
printf 'Press Ctrl+C to stop.\n'
printf '=== END ===\n'

exec env \
	NetClaw__ProjectRoot="$PROJECT_ROOT" \
	NetClaw__Channels__Terminal__Enabled=true \
	NetClaw__Channels__Terminal__ChatJid="$CHAT_JID" \
	NetClaw__Channels__Terminal__Sender="$TERMINAL_SENDER" \
	NetClaw__Channels__Terminal__SenderName="$TERMINAL_SENDER_NAME" \
	NetClaw__Channels__Terminal__ChatName="$TERMINAL_CHAT_NAME" \
	NetClaw__Channels__Terminal__IsGroup="$TERMINAL_IS_GROUP" \
	NetClaw__Channels__Terminal__InputPrompt="$TERMINAL_INPUT_PROMPT" \
	NetClaw__Channels__PollInterval="$CHANNEL_POLL_INTERVAL" \
	NetClaw__MessageLoop__PollInterval="$MESSAGE_LOOP_POLL_INTERVAL" \
	NetClaw__MessageLoop__Timezone="$MESSAGE_LOOP_TIMEZONE" \
	NetClaw__AgentRuntime__CopilotUseLoggedInUser="$COPILOT_USE_LOGGED_IN_USER" \
	NetClaw__AgentRuntime__InteractiveIdleTimeout="$INTERACTIVE_IDLE_TIMEOUT" \
	NetClaw__Dashboard__Enabled="$DASHBOARD_ENABLED" \
	NetClaw__Dashboard__Port="$DASHBOARD_PORT" \
	NetClaw__Dashboard__BindAddress="$DASHBOARD_BIND_ADDRESS" \
	dotnet run --project "$SCRIPT_DIR/src/NetClaw.Host" "$@"