#!/usr/bin/env bash
# Reduce the newest Unity test-results XML to a compact pass/fail summary (the
# raw export is hundreds of KB). Considers both sources — the Test Runner
# window's TestResults_*.xml at the repo root and the batchmode runner's
# Tools/.editmode-results.xml — and picks whichever is most recent.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

candidates=()
for f in "$ROOT"/TestResults_*.xml "$ROOT/Tools/.editmode-results.xml"; do
    [ -f "$f" ] && candidates+=("$f")
done

if [ ${#candidates[@]} -eq 0 ]; then
    echo "No test-results XML found (repo root TestResults_*.xml or Tools/.editmode-results.xml)."
    exit 1
fi

# Newest by modification time — the two sources use different naming schemes.
LATEST="$(ls -t "${candidates[@]}" | head -1)"

echo "Latest: ${LATEST#$ROOT/}"
python3 "$ROOT/Tools/reduce_test_results.py" "$LATEST"
