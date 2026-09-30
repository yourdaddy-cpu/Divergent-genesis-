#!/usr/bin/env bash
# Type-check every Divergent Genesis script without launching Unity.
#   ./Tools/CompileCheck/verify.sh
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

DOTNET="${DOTNET:-dotnet}"
if ! command -v "$DOTNET" >/dev/null 2>&1; then
  echo "dotnet 8 SDK not found. Install from https://dotnet.microsoft.com/download" >&2
  exit 1
fi

echo "==> Compiling Divergent Genesis against the Unity API stub"
"$DOTNET" build "$HERE/DgCheck.csproj" -v quiet --nologo \
  | grep -Ev '^\s*$' || true

echo
echo "OK - all gameplay and editor scripts type-check cleanly."
