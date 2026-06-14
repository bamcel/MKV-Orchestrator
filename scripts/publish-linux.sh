#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet publish "$ROOT_DIR/src/MKVOrchestrator.App/MKVOrchestrator.App.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o "$ROOT_DIR/artifacts/publish/linux-x64"
