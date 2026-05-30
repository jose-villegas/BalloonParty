#!/usr/bin/env bash
# Tools/generate-docs.sh — Rebuild Doxygen documentation locally
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

echo "▶ Rendering Graphviz diagrams..."
for f in Docs/diagrams/*.dot; do
  dot -Tsvg "$f" -o "${f%.dot}.svg"
done
echo "  ✔ $(ls Docs/diagrams/*.svg | wc -l | tr -d ' ') SVGs rendered"

echo "▶ Generating Doxygen..."
doxygen Doxyfile 2>&1 | grep -c "warning:" | xargs -I{} echo "  ⚠ {} warnings"

PAGE_COUNT=$(find Docs/doxygen/html -name "*.html" | wc -l | tr -d ' ')
echo "  ✔ ${PAGE_COUNT} HTML pages generated"

echo ""
echo "✅ Documentation ready at: Docs/doxygen/html/index.html"

# Open in default browser
if command -v open &>/dev/null; then
  open "$REPO_ROOT/Docs/doxygen/html/index.html"
elif command -v xdg-open &>/dev/null; then
  xdg-open "$REPO_ROOT/Docs/doxygen/html/index.html"
fi

