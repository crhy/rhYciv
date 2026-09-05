#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
DIST="$HERE/dist"
ART="$ROOT/RaylibUI/FOSSart"
rm -rf "$DIST" && mkdir -p "$DIST/assets"
cp "$HERE"/{index.html,styles.css,script.js,favicon.svg,site.webmanifest,robots.txt,sitemap.xml,_headers,_redirects,404.html} "$DIST/"
for f in archers armour battleship bombers caravel; do cp "$ART/Units/$f.png" "$DIST/assets/$f.png"; done
cp "$ART/Advances/fusionpower.jpg" "$DIST/assets/fusionpower.jpg"
cp "$ART/rhyciv-app-icon.png" "$DIST/assets/rhyciv-app-icon.png"
echo "Built $DIST"
