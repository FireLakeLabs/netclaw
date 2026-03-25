#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

RUNTIME="${CONTAINER_RUNTIME:-docker}"
TAG="${1:-latest}"
IMAGE_NAME="netclaw-agent:${TAG}"

echo "Building ${IMAGE_NAME} with ${RUNTIME}..."

"${RUNTIME}" build \
  -t "${IMAGE_NAME}" \
  -f "${SCRIPT_DIR}/Dockerfile" \
  "${PROJECT_ROOT}"

echo "Built ${IMAGE_NAME}"
