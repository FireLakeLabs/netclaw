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

ARG COPILOT_VERSION=1.0.12
ARG TARGETARCH

# Install Node.js (for Claude Code CLI)
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI globally
RUN npm install -g @anthropic-ai/claude-code

# Install the standalone Copilot CLI binary from the official GitHub release.
RUN set -eux; \
    case "${TARGETARCH}" in \
        amd64) copilot_arch='x64' ;; \
        arm64) copilot_arch='arm64' ;; \
        *) echo "Unsupported Copilot architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac; \
    curl -fsSL -o /tmp/SHA256SUMS.txt "https://github.com/github/copilot-cli/releases/download/v${COPILOT_VERSION}/SHA256SUMS.txt"; \
    curl -fsSL -o /tmp/copilot.tar.gz "https://github.com/github/copilot-cli/releases/download/v${COPILOT_VERSION}/copilot-linux-${copilot_arch}.tar.gz"; \
    expected_sha="$(awk '/ copilot-linux-'"${copilot_arch}"'\.tar\.gz$/ { print $1 }' /tmp/SHA256SUMS.txt)"; \
    test -n "${expected_sha}"; \
    printf '%s  %s\n' "${expected_sha}" /tmp/copilot.tar.gz | sha256sum -c -; \
    mkdir -p /tmp/copilot-extract; \
    tar -xzf /tmp/copilot.tar.gz -C /tmp/copilot-extract; \
    install -m 0755 "$(find /tmp/copilot-extract -type f -name copilot -print -quit)" /usr/local/bin/copilot; \
    rm -rf /tmp/copilot.tar.gz /tmp/SHA256SUMS.txt /tmp/copilot-extract

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
