# NetClaw Secret References for 1Password CLI
#
# Usage:
#   op run --env-file=.env.tpl -- ./run-slack-channel.sh
#
# Setup:
#   1. Install the 1Password CLI: https://developer.1password.com/docs/cli/
#   2. Create a vault called "netclaw" (or adjust the references below)
#   3. Create items in the vault for each secret:
#      - An item called "slack" with fields "bot-token" and "app-token"
#      - An item called "copilot" with a field "github-token" (if not using logged-in user)
#      - An item called "dashboard" with a field "bearer-token" (for dashboard auth, future)
#   4. Run your launch script wrapped with op run:
#      op run --env-file=.env.tpl -- ./run-slack-channel.sh
#
# Secret references are resolved by `op run` at process start.
# The plaintext values exist only in the child process environment
# and never touch disk, shell history, or logs.
#
# See: https://developer.1password.com/docs/cli/secret-references/

# Slack channel credentials
NETCLAW_SLACK_BOT_TOKEN=op://netclaw/slack/bot-token
NETCLAW_SLACK_APP_TOKEN=op://netclaw/slack/app-token
NETCLAW_CHAT_JID=op://netclaw/slack/chat-jid

# Slack conversation to connect (not a secret, but convenient to keep here)
# NETCLAW_CHAT_JID=C0123456789

# Copilot GitHub token (only needed if CopilotUseLoggedInUser is false)
# NETCLAW_COPILOT_GITHUB_TOKEN=op://netclaw/copilot/github-token

# Dashboard bearer token (for future dashboard auth middleware)
# NETCLAW_DASHBOARD_TOKEN=op://netclaw/dashboard/bearer-token
