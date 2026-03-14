#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PROJECT_ROOT="${NETCLAW_PROJECT_ROOT:-/tmp/netclaw-slack}"
CHAT_JID="${NETCLAW_CHAT_JID:-}"
CHAT_NAME="${NETCLAW_CHAT_NAME:-Slack Chat}"
GROUP_FOLDER="${NETCLAW_GROUP_FOLDER:-slack-chat}"
AGENT_TRIGGER="${NETCLAW_AGENT_TRIGGER:-@Andy}"
SLACK_BOT_TOKEN="${NETCLAW_SLACK_BOT_TOKEN:-}"
SLACK_APP_TOKEN="${NETCLAW_SLACK_APP_TOKEN:-}"
SLACK_MENTION_REPLACEMENT="${NETCLAW_SLACK_MENTION_REPLACEMENT:-$AGENT_TRIGGER}"
SLACK_WORKING_INDICATOR_TEXT="${NETCLAW_SLACK_WORKING_INDICATOR_TEXT:-Evaluating...}"
SLACK_REPLY_IN_THREAD_BY_DEFAULT="${NETCLAW_SLACK_REPLY_IN_THREAD_BY_DEFAULT:-true}"
CHANNEL_POLL_INTERVAL="${NETCLAW_CHANNEL_POLL_INTERVAL:-00:00:01}"
MESSAGE_LOOP_POLL_INTERVAL="${NETCLAW_MESSAGE_LOOP_POLL_INTERVAL:-00:00:01}"
MESSAGE_LOOP_TIMEZONE="${NETCLAW_MESSAGE_LOOP_TIMEZONE:-UTC}"
COPILOT_USE_LOGGED_IN_USER="${NETCLAW_COPILOT_USE_LOGGED_IN_USER:-true}"
INTERACTIVE_IDLE_TIMEOUT="${NETCLAW_INTERACTIVE_IDLE_TIMEOUT:-00:00:30}"
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

register_args=(
	dotnet run --project "$SCRIPT_DIR/src/NetClaw.Setup" -- --step register
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
printf 'CHAT_JID: %s\n' "$CHAT_JID"
printf 'CHAT_NAME: %s\n' "$CHAT_NAME"
printf 'GROUP_FOLDER: %s\n' "$GROUP_FOLDER"
printf 'TRIGGER_MODE: %s\n' "$trigger_mode"
printf 'WORKING_INDICATOR: %s\n' "$SLACK_WORKING_INDICATOR_TEXT"
printf 'Press Ctrl+C to stop.\n'
printf '=== END ===\n'

exec env \
	NetClaw__ProjectRoot="$PROJECT_ROOT" \
	NetClaw__Channels__Slack__Enabled=true \
	NetClaw__Channels__Slack__BotToken="$SLACK_BOT_TOKEN" \
	NetClaw__Channels__Slack__AppToken="$SLACK_APP_TOKEN" \
	NetClaw__Channels__Slack__MentionReplacement="$SLACK_MENTION_REPLACEMENT" \
	NetClaw__Channels__Slack__WorkingIndicatorText="$SLACK_WORKING_INDICATOR_TEXT" \
	NetClaw__Channels__Slack__ReplyInThreadByDefault="$SLACK_REPLY_IN_THREAD_BY_DEFAULT" \
	NetClaw__Channels__PollInterval="$CHANNEL_POLL_INTERVAL" \
	NetClaw__MessageLoop__PollInterval="$MESSAGE_LOOP_POLL_INTERVAL" \
	NetClaw__MessageLoop__Timezone="$MESSAGE_LOOP_TIMEZONE" \
	NetClaw__AgentRuntime__CopilotUseLoggedInUser="$COPILOT_USE_LOGGED_IN_USER" \
	NetClaw__AgentRuntime__InteractiveIdleTimeout="$INTERACTIVE_IDLE_TIMEOUT" \
	dotnet run --project "$SCRIPT_DIR/src/NetClaw.Host" "$@"