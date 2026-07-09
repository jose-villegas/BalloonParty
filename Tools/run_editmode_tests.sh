#!/usr/bin/env bash
#
# Run the EditMode test suite headlessly and write a compact summary to
# Tools/last-test-run.md (which Claude reads). Gated on a "major change"
# threshold so it no-ops when little has changed since the last run.
#
#   Tools/run_editmode_tests.sh           # run only if the threshold is crossed
#   Tools/run_editmode_tests.sh --force   # run regardless
#
# Requires the Unity editor to be CLOSED (batchmode can't take the project
# lock). While the editor is open, run tests from its Test Runner window
# instead, or use the editor menu item (see Tools/README).

set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STAMP="$ROOT/Tools/.test-run-stamp"
OUT="$ROOT/Tools/last-test-run.md"
RESULTS="$ROOT/Tools/.editmode-results.xml"
LOG="$ROOT/Tools/.editmode-run.log"

# "Major" = at least this many changed non-comment .cs lines OR files since the
# last run. Tune to taste.
MIN_LINES=40
MIN_FILES=3

FORCE=0
[ "${1:-}" = "--force" ] && FORCE=1

VERSION="$(awk '/^m_EditorVersion:/ {print $2}' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
if [ ! -x "$UNITY" ]; then
    echo "Unity $VERSION not found at $UNITY" >&2
    exit 1
fi

# --- Threshold gate ---------------------------------------------------------
if [ "$FORCE" -eq 0 ] && [ -f "$STAMP" ]; then
    BASE="$(cat "$STAMP")"
    # Committed + uncommitted .cs changes since BASE, minus comment/blank lines.
    CS_DIFF="$(git -C "$ROOT" diff "$BASE" -- '*.cs')"
    LINES="$(printf '%s\n' "$CS_DIFF" | grep -E '^[+-]' | grep -vE '^[+-][+-]' \
        | grep -vcE '^[+-][[:space:]]*(//|/\*|\*|$)')"
    FILES="$(git -C "$ROOT" diff --name-only "$BASE" -- '*.cs' | grep -c . )"
    if [ "$LINES" -lt "$MIN_LINES" ] && [ "$FILES" -lt "$MIN_FILES" ]; then
        echo "Skipping — $FILES .cs file(s) / $LINES non-comment line(s) changed since last run" \
             "(thresholds: $MIN_FILES files or $MIN_LINES lines). Use --force to run anyway."
        exit 0
    fi
    echo "Change threshold crossed: $FILES .cs file(s), $LINES non-comment line(s) since last run."
fi

# --- Editor lock check ------------------------------------------------------
if [ -f "$ROOT/Temp/UnityLockfile" ]; then
    echo "The Unity editor appears to hold the project lock (Temp/UnityLockfile)." >&2
    echo "Close the editor and retry, or run tests from its Test Runner window." >&2
    exit 2
fi

# --- Run --------------------------------------------------------------------
echo "Running EditMode tests via Unity $VERSION (can take a minute)…"
"$UNITY" -runTests -batchmode -projectPath "$ROOT" \
    -testPlatform EditMode -testResults "$RESULTS" -logFile "$LOG"
CODE=$?

if [ ! -f "$RESULTS" ]; then
    echo "No results file produced — the run did not start. See $LOG" >&2
    echo "# EditMode tests — run did not start (exit $CODE); see Tools/.editmode-run.log" > "$OUT"
    exit "$CODE"
fi

python3 "$ROOT/Tools/reduce_test_results.py" "$RESULTS" --out "$OUT"
HEAD_SHA="$(git -C "$ROOT" rev-parse HEAD)"
# Tag the run with its commit so the pre-push hook can judge freshness (same
# marker the in-editor runner writes).
printf '\n<!-- ran-against: %s -->\n' "$HEAD_SHA" >> "$OUT"
echo "$HEAD_SHA" > "$STAMP"
exit "$CODE"
