FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build

WORKDIR /source

# Copy solution and project files for restore
COPY FireLakeLabs.NetClaw.slnx ./
COPY src/FireLakeLabs.NetClaw.Domain/FireLakeLabs.NetClaw.Domain.csproj src/FireLakeLabs.NetClaw.Domain/
COPY src/FireLakeLabs.NetClaw.AgentRunner/FireLakeLabs.NetClaw.AgentRunner.csproj src/FireLakeLabs.NetClaw.AgentRunner/
COPY Directory.Build.props ./

# Restore only the AgentRunner and its dependency
RUN dotnet restore src/FireLakeLabs.NetClaw.AgentRunner/FireLakeLabs.NetClaw.AgentRunner.csproj

# Copy source
COPY src/FireLakeLabs.NetClaw.Domain/ src/FireLakeLabs.NetClaw.Domain/
COPY src/FireLakeLabs.NetClaw.AgentRunner/ src/FireLakeLabs.NetClaw.AgentRunner/

# Publish
RUN dotnet publish src/FireLakeLabs.NetClaw.AgentRunner/FireLakeLabs.NetClaw.AgentRunner.csproj \
    -c Release \
    -o /app \
    --no-restore

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime

# Install Node.js (for Claude Code CLI)
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI globally
RUN npm install -g @anthropic-ai/claude-code

# Install Copilot CLI (placeholder — replace with actual package when available)
# For now, create a stub script that returns an error if actually invoked without real CLI.
# The stub drains stdin so the runner's stdin write does not cause a broken pipe.
RUN printf '#!/bin/sh\ncat > /dev/null\necho "Copilot CLI not installed in this image" >&2\nexit 1\n' > /usr/local/bin/copilot && \
    chmod +x /usr/local/bin/copilot

# Create non-root user for container execution
RUN useradd -m -s /bin/bash user

WORKDIR /app
COPY --from=build /app .

# Create default workspace directories while still root, then hand ownership to the runtime user.
RUN mkdir -p /workspace/group /workspace/global /workspace/ipc /workspace/project /home/user/.copilot /home/user/.claude && \
    chown -R user:user /workspace /home/user /app

USER user

ENV NETCLAW_PROVIDER=copilot

ENTRYPOINT ["dotnet", "FireLakeLabs.NetClaw.AgentRunner.dll"]
