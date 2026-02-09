#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUTION_PATH="$ROOT_DIR/ScimitarBattery/ScimitarBattery.sln"
CONFIGURATION="${1:-Debug}"
shift $(( $# > 0 ? 1 : 0 ))

resolve_dotnet() {
  if [[ -n "${DOTNET_BIN:-}" ]]; then
    echo "$DOTNET_BIN"
    return
  fi

  # Prefer Homebrew dotnet@8 to honor repo global.json (8.0.123).
  if [[ -x "/opt/homebrew/opt/dotnet@8/bin/dotnet" ]]; then
    echo "/opt/homebrew/opt/dotnet@8/bin/dotnet"
    return
  fi

  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
    return
  fi

  echo ""
}

DOTNET_BIN="$(resolve_dotnet)"
if [[ -z "$DOTNET_BIN" ]]; then
  echo "dotnet not found. Install .NET SDK 8.0.123 or set DOTNET_BIN."
  exit 1
fi

if [[ ! -f "$SOLUTION_PATH" ]]; then
  echo "Solution not found: $SOLUTION_PATH"
  exit 1
fi

echo "Using dotnet: $DOTNET_BIN"
echo "Building: $SOLUTION_PATH (Configuration=$CONFIGURATION)"
"$DOTNET_BIN" build "$SOLUTION_PATH" -c "$CONFIGURATION" "$@"
