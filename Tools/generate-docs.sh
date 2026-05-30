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
echo "   Open with: open Docs/doxygen/html/index.html"

