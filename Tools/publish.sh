#!/usr/bin/env bash
#
# publish.sh - the only command you need to run.
#
#   ./Tools/publish.sh
#
# It does the whole remaining pipeline:
#   1. verifies the working tree is committed
#   2. pushes to github.com/yourdaddy-cpu/Divergent-genesis-
#   3. waits for CI (type-check gate, then the ~40 min Android build)
#   4. downloads the divergent-genesis-apk artifact next to this script
#   5. prints the direct download URL
#
# Requires: git, and either `gh` (recommended) or a GITHUB_TOKEN in the
# environment. Everything else - Unity, the Android SDK, a Unity licence - is
# handled by the CI runner.
#
# Flags:
#   --no-watch   push and exit, don't wait for the build
#   --watch-only attach to an already-running build (run it again if this dies)
#
# Windows: run this from Git Bash, not PowerShell/cmd.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(dirname "$HERE")"
REPO="yourdaddy-cpu/Divergent-genesis-"
ARTIFACT="divergent-genesis-apk"
WATCH=1
[ "${1:-}" = "--no-watch" ] && WATCH=0
[ "${1:-}" = "--watch-only" ] && WATCH=1

cd "$ROOT"

say()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
warn() { printf '\033[33m    %s\033[0m\n' "$*"; }
die()  { printf '\n\033[31m!! %s\033[0m\n\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------- 1. auth
say "Checking GitHub access"
if ! command -v gh >/dev/null 2>&1 && [ -z "${GITHUB_TOKEN:-}" ]; then
  cat <<'EOF'
    No GitHub credentials found.

    Pick ONE:

      A) Install the GitHub CLI, then:
           curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
             | sudo dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg
           sudo apt install gh
           gh auth login            # browser sign-in, no token copy-pasting

      B) Create a token at https://github.com/settings/tokens/new
         with the 'repo' scope, then:
           export GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
           ./Tools/publish.sh
    On Windows, run this script from Git Bash - it is a bash script, not PowerShell.
EOF
  die "cannot continue without credentials"
fi

if command -v gh >/dev/null 2>&1; then
  gh auth status >/dev/null 2>&1 || die "gh is installed but not logged in - run: gh auth login"
  say "Authenticated as $(gh api user -q .login 2>/dev/null || echo 'unknown')"
else
  say "Authenticated via GITHUB_TOKEN"
fi

# ------------------------------------------------------- 2. local state
if [ -n "$(git status --porcelain)" ]; then
  git add -A
  git commit -m "WIP: $(date -u '+%Y-%m-%d %H:%M UTC')"
  say "Committed outstanding changes"
fi
say "HEAD is $(git log --oneline -1)"

# ---------------------------------------------------------- 3. push
if [ "$WATCH" = 1 ] || [ ! -z "$(git status --porcelain)" ]; then
  say "Pushing to origin/main"
  git push -u origin main
else
  say "Attaching to the latest run (--watch-only); not pushing"
fi

# -------------------------------------------- 4. check the licence secret
say "Checking that UNITY_LICENSE is set"
if command -v gh >/dev/null 2>&1; then
  if gh secret list --repo "$REPO" 2>/dev/null | grep -q '^UNITY_LICENSE'; then
    echo "    UNITY_LICENSE is present"
  else
    warn "UNITY_LICENSE is NOT set on the repo."
    cat <<'EOF'

      The build WILL fail at Unity activation. Fix it now:

        1. Sign in at https://license.unity3d.com/manual
        2. Select Personal -> download the .ulf file
        3. Add it as a secret:
             gh secret set UNITY_LICENSE --repo yourdaddy-cpu/Divergent-genesis- < /path/to/file.ulf
           (or: repo -> Settings -> Secrets and variables -> Actions -> New
            repository secret named UNITY_LICENSE, value = the whole .ulf)

      Re-run this script afterwards.
EOF
    exit 1
  fi
fi

[ "$WATCH" = 0 ] && { say "Done - not watching (--no-watch)"; exit 0; }

# --------------------------------------------------------- 5. watch CI
say "Waiting for CI to start (the type-check gate runs first, ~15s)"
RUN=""
for _ in $(seq 1 30); do
  if command -v gh >/dev/null 2>&1; then
    RUN=$(gh run list --repo "$REPO" --workflow android-apk.yml --limit 1 \
          --json databaseId,status --jq '.[0].databaseId' 2>/dev/null || true)
  fi
  [ -n "$RUN" ] && [ "$RUN" != "null" ] && break
  sleep 4
done
[ -n "$RUN" ] && [ "$RUN" != "null" ] || die "no CI run appeared - check https://github.com/$REPO/actions"

RUN_URL="https://github.com/$REPO/actions/runs/$RUN"
echo "    Live: $RUN_URL"
echo "    The Android build takes roughly 30-45 minutes. IL2CPP is compiling C++."
echo "    Press Ctrl-C to stop watching; the build keeps going on GitHub."
echo

if command -v gh >/dev/null 2>&1; then
  gh run watch "$RUN" --repo "$REPO" --exit-status || {
    warn "the run did not succeed"
    echo "    Logs: $RUN_URL"
    echo
    echo "    Paste me the failing step's log and I will fix it."
    exit 1
  }
fi

# -------------------------------------------------------- 6. artifact
say "Downloading artifact"
rm -rf "$HERE/../$ARTIFACT"
mkdir -p "$HERE/../$ARTIFACT"

if command -v gh >/dev/null 2>&1; then
  gh run download "$RUN" --repo "$REPO" --name "$ARTIFACT" --dir "$HERE/../$ARTIFACT" \
    || die "artifact download failed - see $RUN_URL"
else
  die "gh is required to download the artifact"
fi

APK=$(find "$HERE/../$ARTIFACT" -name '*.apk' | head -1)
[ -n "$APK" ] || die "the artifact did not contain an .apk - see $RUN_URL"

echo
printf '\033[32m    APK ready: %s\033[0m\n' "$(cd "$(dirname "$APK")" && pwd)/$(basename "$APK")"
printf '    size: %s\n' "$(du -h "$APK" | cut -f1)"
echo
echo "    Run:  adb install -r $(basename "$APK")"
echo "    Or copy it to the phone and open it."
echo
echo "    Re-download later:"
echo "      gh run download --repo $REPO -n $ARTIFACT -D $ARTIFACT"
echo
echo "    Run: https://github.com/$REPO/actions/runs/$RUN"
