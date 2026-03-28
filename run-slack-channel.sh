#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
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

# Initialize project directory and config if needed
"$DOTNET_BIN" run --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Setup" -- --step init

register_args=(
	"$DOTNET_BIN" run --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Setup" -- --step register
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

# Repeated Ctrl+C can kill `dotnet run` before it tears down child processes; we explicitly stop descendants.
run_host_with_cleanup() {
	local host_pid=0
	local interrupted=0

	kill_descendants() {
		local parent_pid="$1"
		local signal="$2"
		local child_pid

		if ! command -v pgrep >/dev/null 2>&1; then
			return
		fi

		while IFS= read -r child_pid; do
			if [[ -z "$child_pid" ]]; then
				continue
			fi

			kill_descendants "$child_pid" "$signal"
			kill "-$signal" "$child_pid" 2>/dev/null || true
		done < <(pgrep -P "$parent_pid" || true)
	}

	stop_host() {
		local signal="$1"

		if [[ "$host_pid" -le 0 ]]; then
			return
		fi

		if ! kill -0 "$host_pid" 2>/dev/null; then
			return
		fi

		kill_descendants "$host_pid" "$signal"
		kill "-$signal" "$host_pid" 2>/dev/null || true
	}

	on_interrupt() {
		if [[ "$interrupted" -eq 0 ]]; then
			interrupted=1
			echo "Stopping NetClaw host..."
			stop_host TERM
		else
			echo "Force-stopping NetClaw host and children..."
			stop_host KILL
		fi
	}

	trap on_interrupt INT
	trap 'stop_host TERM' TERM
	trap 'stop_host TERM' EXIT

	# Secrets stay as env vars — everything else comes from appsettings.json
	env \
		NetClaw__Channels__Slack__Enabled=true \
		NetClaw__Channels__Slack__BotToken="$SLACK_BOT_TOKEN" \
		NetClaw__Channels__Slack__AppToken="$SLACK_APP_TOKEN" \
		"$DOTNET_BIN" run --project "$SCRIPT_DIR/src/FireLakeLabs.NetClaw.Host" "$@" &
	host_pid=$!

	set +e
	wait "$host_pid"
	local exit_code=$?
	set -e

	trap - INT TERM EXIT
	return "$exit_code"
}

run_host_with_cleanup "$@"

	