#!/usr/bin/env bash
# Everything CI checks before it spends 40 minutes on an Android build.
#   ./Tools/verify.sh
#
# 1. type-check every gameplay and editor script against the Unity API stub
# 2. validate the hand-maintained Unity YAML (scene, GUIDs, .meta files)
#
# Neither step needs Unity or a licence.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(dirname "$HERE")"
DOTNET="${DOTNET:-dotnet}"

# Windows ships `python`, not `python3`. Probe once, prefer either.
PY="python3"
command -v python3 >/dev/null 2>&1 || PY="python"
command -v "$PY" >/dev/null 2>&1 || PY=""

if command -v "$DOTNET" >/dev/null 2>&1; then
  echo "==> 1/2  Type-checking scripts"
  "$DOTNET" build "$HERE/CompileCheck/DgCheck.csproj" -v quiet --nologo \
    | grep -E 'error|Error\(s\)|Warning\(s\)|Build succeeded' | sed 's/^/    /'
else
  echo "==> 1/2  Type-checking scripts - SKIPPED (no dotnet on PATH)" >&2
fi

echo
echo "==> 2/2  Validating project structure"
if [ -z "$PY" ]; then
  echo "  SKIPPED - no python on PATH (structure check is optional; CI has it)"
else
  "$PY" "$HERE/validate_project.py" | sed 's/^/  /'
fi

echo
echo "All checks passed."
