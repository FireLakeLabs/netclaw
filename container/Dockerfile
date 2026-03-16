FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build

WORKDIR /source

# Copy solution and project files for restore
COPY NetClaw.slnx ./
COPY src/NetClaw.Domain/NetClaw.Domain.csproj src/NetClaw.Domain/
COPY src/NetClaw.AgentRunner/NetClaw.AgentRunner.csproj src/NetClaw.AgentRunner/
COPY Directory.Build.props ./

# Restore only the AgentRunner and its dependency
RUN dotnet restore src/NetClaw.AgentRunner/NetClaw.AgentRunner.csproj

# Copy source
COPY src/NetClaw.Domain/ src/NetClaw.Domain/
COPY src/NetClaw.AgentRunner/ src/NetClaw.AgentRunner/

# Publish
RUN dotnet publish src/NetClaw.AgentRunner/NetClaw.AgentRunner.csproj \
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
# For now, create a stub script that returns an error if actually invoked without real CLI
RUN echo '#!/bin/sh\necho "Copilot CLI not installed in this image" >&2\nexit 1' > /usr/local/bin/copilot && \
    chmod +x /usr/local/bin/copilot

# Create non-root user for container execution
RUN useradd -m -s /bin/bash user
USER user

WORKDIR /app
COPY --from=build /app .

# Create default workspace directories
RUN mkdir -p /workspace/group /workspace/global /workspace/ipc /workspace/project /home/user/.copilot /home/user/.claude

ENV NETCLAW_PROVIDER=copilot

ENTRYPOINT ["dotnet", "NetClaw.AgentRunner.dll"]
