#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

RUNTIME="${CONTAINER_RUNTIME:-docker}"
TAG="${1:-latest}"
IMAGE_NAME="netclaw-agent:${TAG}"

ARCH="${TARGETARCH:-}"
if [[ -z "$ARCH" ]]; then
  case "$(uname -m)" in
    x86_64|amd64) ARCH="amd64" ;;
    aarch64|arm64) ARCH="arm64" ;;
    *)
      echo "Unsupported host architecture: $(uname -m)" >&2
      exit 1
      ;;
   esac
fi

echo "Building ${IMAGE_NAME} with ${RUNTIME}..."

"${RUNTIME}" build \
    --build-arg "TARGETARCH=${ARCH}" \
  -t "${IMAGE_NAME}" \
  -f "${SCRIPT_DIR}/Dockerfile" \
  "${PROJECT_ROOT}"

echo "Built ${IMAGE_NAME}"
