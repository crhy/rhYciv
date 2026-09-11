#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
DIST="$HERE/dist"
ART="$ROOT/RaylibUI/FOSSart"

# The version the site advertises comes from the one place the version is
# declared, rather than being typed into the page.
#
# It used to be written into index.html by hand, and so it stayed at 0.1.2 while
# five releases went out -- the download button on the front page offered a
# version that was months of work behind the one in the release the button led
# to. Anything the page states about the build is substituted here, at build
# time, so it cannot drift again.
VERSION="$("$ROOT/scripts/site_facts.py" version)"
TESTS="$("$ROOT/scripts/site_facts.py" tests)"
echo "Building the site for $VERSION ($TESTS tests)"

rm -rf "$DIST" && mkdir -p "$DIST/assets"
cp "$HERE"/{styles.css,script.js,site.webmanifest,robots.txt,sitemap.xml,_headers,_redirects,404.html,hero-map.jpg} "$DIST/"
sed -e "s/__RHYCIV_VERSION__/$VERSION/g" -e "s/__RHYCIV_TESTS__/$TESTS/g" \
    "$HERE/index.html" > "$DIST/index.html"

if grep -q "__RHYCIV_" "$DIST/index.html"; then
    echo "Error: the built page still carries an unsubstituted placeholder." >&2
    grep -o "__RHYCIV_[A-Z_]*__" "$DIST/index.html" | sort -u >&2
    exit 1
fi

for f in archers armour battleship bombers caravel; do cp "$ART/Units/$f.png" "$DIST/assets/$f.png"; done
cp "$ART/Advances/fusionpower.jpg" "$DIST/assets/fusionpower.jpg"
cp "$ART/rhyciv-app-icon.png" "$DIST/assets/rhyciv-app-icon.png"
echo "Built $DIST"
