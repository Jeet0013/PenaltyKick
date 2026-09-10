#!/usr/bin/env bash
#
# Headless script compile, and a readable report of what Unity said.
#
# This is the check the stub harness cannot perform. `tools/unity-stubs/` proves
# the Unity layer is internally consistent; only the real editor can say whether
# it matches the real API. Everything in Assets/Scripts/Unity is UNVERIFIED
# until this passes once.
#
# Usage:  tools/unity-compile.sh
#
set -uo pipefail

PROJECT="$(cd "$(dirname "$0")/.." && pwd)/unity/CyberGoalShootout"
LOG="/tmp/cybergoal-unity-compile.log"

UNITY=$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | head -1)
if [ -z "$UNITY" ]; then
  echo "No Unity editor found under /Applications/Unity/Hub/Editor."
  echo "Install one with:"
  echo '  "/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install --version 6000.0.83f1'
  exit 2
fi

echo "Editor:  $UNITY"
echo "Project: $PROJECT"
echo "Log:     $LOG"
echo

# -quit exits after the project finishes importing and compiling.
# -nographics avoids needing a display; script compilation does not need one.
# Import of a fresh project takes several minutes and looks like a hang.
"$UNITY" \
  -batchmode \
  -quit \
  -nographics \
  -projectPath "$PROJECT" \
  -logFile "$LOG" \
  -disable-assembly-updater
STATUS=$?

echo "Unity exited with $STATUS"
echo

# ── Licence ───────────────────────────────────────────────────────────────
# The most likely first failure, and it is not a code problem. Reported
# separately so it is never mistaken for one.
if grep -qiE "No valid Unity Editor license|Licence not found|License is not|Failed to activate" "$LOG" 2>/dev/null; then
  echo "=============================================="
  echo " NOT A CODE ERROR: Unity has no licence yet."
  echo "=============================================="
  echo
  echo "Open Unity Hub, sign in with a Unity account, and take the free"
  echo "Personal licence. Then re-run this script."
  echo
  # Show the lines that FAILED, not simply the first ones mentioning a licence.
  # The first matches are successful IPC handshakes ("Successfully connected to
  # LicenseClient"), so `head` made a correct diagnosis look like a false
  # positive and cost a round of investigation.
  grep -iE "license|licence" "$LOG" \
    | grep -iE "error|no valid|not found|failed|entitlement" \
    | tail -6
  exit 3
fi

# ── Compile errors ────────────────────────────────────────────────────────
echo "=== Compile errors ==="
if grep -E "error CS[0-9]+" "$LOG" | sort -u | head -60; then
  :
fi
COUNT=$(grep -cE "error CS[0-9]+" "$LOG" 2>/dev/null || echo 0)
echo
echo "Total unique-ish error lines: $COUNT"

echo
echo "=== Other failures worth reading ==="
grep -iE "^\s*(Assertion|Exception|Fatal)|Scripts have compiler errors|Failed to load|Unable to" "$LOG" 2>/dev/null | head -20

if [ "$COUNT" -eq 0 ] && [ "$STATUS" -eq 0 ]; then
  echo
  echo "COMPILED CLEAN."
fi

exit $STATUS
