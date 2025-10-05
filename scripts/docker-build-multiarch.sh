#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <dockerhub-repo> <version> [--push]"
  echo "Example: $0 sebfischer83/arquivomate2 1.2.3 --push"
  exit 1
fi

REPO="$1"
VERSION="$2"
PUSH="false"
if [ "${3-}" == "--push" ]; then
  PUSH="true"
fi

# Ensure QEMU registration for emulation (requires --privileged the first time)
echo "Registering QEMU for multi-arch emulation (may require root)"
docker run --rm --privileged multiarch/qemu-user-static --reset -p yes

# Create and bootstrap a builder
BUILDER_NAME="arquivomate2-multi-builder"
docker buildx create --name $BUILDER_NAME --use || true
docker buildx inspect --bootstrap

# Buildx command
BUILD_CMD=(docker buildx build --platform linux/amd64,linux/arm64 -f src/ArquivoMate2.API/Dockerfile --build-arg VERSION="$VERSION" -t "$REPO:$VERSION")
if [ "$PUSH" = "true" ]; then
  BUILD_CMD+=(--push)
else
  BUILD_CMD+=(--load)
fi

echo "Running: ${BUILD_CMD[*]}"
eval "${BUILD_CMD[*]}"

echo "Done. Image: $REPO:$VERSION"
