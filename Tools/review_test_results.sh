#!/usr/bin/env bash
# Reduce the newest TestResults_*.xml at the repo root to a compact pass/fail
# summary (the raw Unity export is hundreds of KB). Run when reviewing results.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# Timestamped names (TestResults_YYYYMMDD_HHMMSS.xml) sort chronologically.
LATEST="$(ls "$ROOT"/TestResults_*.xml 2>/dev/null | sort | tail -1)"

if [ -z "$LATEST" ]; then
    echo "No TestResults_*.xml at repo root ($ROOT)."
    exit 1
fi

echo "Latest: $(basename "$LATEST")"
python3 "$ROOT/Tools/reduce_test_results.py" "$LATEST"
