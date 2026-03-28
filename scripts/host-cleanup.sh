#!/usr/bin/env bash

run_host_with_cleanup() {
	local host_pid=0
	local host_pgid=""
	local new_session=0
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

	signal_process_group() {
		local signal="$1"

		if [[ "$new_session" -eq 0 ]] || [[ -z "$host_pgid" ]]; then
			return
		fi

		kill "-$signal" -- "-$host_pgid" 2>/dev/null || true
	}

	fallback_descendant_cleanup() {
		local signal="$1"

		if [[ "$host_pid" -le 0 ]]; then
			return
		fi

		kill_descendants "$host_pid" "$signal"
		kill "-$signal" "$host_pid" 2>/dev/null || true
	}

	stop_host() {
		local signal="$1"

		signal_process_group "$signal"
		fallback_descendant_cleanup "$signal"
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

	if command -v setsid >/dev/null 2>&1; then
		setsid "$@" &
		new_session=1
	else
		"$@" &
	fi

	host_pid=$!
	host_pgid="$(ps -o pgid= -p "$host_pid" | tr -d ' ' || true)"

	set +e
	local exit_code
	while :; do
		wait "$host_pid"
		exit_code=$?

		# If wait was interrupted by SIGINT (130) but the host is still alive,
		# keep waiting so that a second Ctrl+C can escalate to KILL and we
		# don't return before the process group is actually gone.
		if [[ "$exit_code" -ne 130 ]] || ! kill -0 "$host_pid" 2>/dev/null; then
			break
		fi
	done
	set -e

	trap - INT TERM EXIT
	return "$exit_code"
}
