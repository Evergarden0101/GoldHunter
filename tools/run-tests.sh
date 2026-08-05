#!/usr/bin/env bash
# Verifies everything that can be verified without a Unity licence.
#
#   1. The engine-independent core compiles and its match/economy tests pass.
#   2. The Unity layer compiles against a stub UnityEngine, so a typo in a
#      MonoBehaviour fails here instead of when someone opens the editor.
#
# Usage: tools/run-tests.sh [--matches N] [--difficulty easy|normal|hard] [--verbose]
set -euo pipefail

cd "$(dirname "$0")/.."
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$PATH:/usr/lib/dotnet:/usr/share/dotnet"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: the .NET SDK is required (dotnet not found on PATH)" >&2
  exit 1
fi

echo "==> Unity layer (compiled against the UnityEngine stub)"
dotnet build tools/UnityStubCheck -v quiet --nologo
echo "    compiles clean"
echo

echo "==> Core simulation tests + balance"
dotnet run --project tools/CoreTests -c Release --nologo -- "$@"
