#!/usr/bin/env bash
# q09: capture the four UI scenes the task reviews (main menu, top menu open,
# a dialog, the city window) through the game's own headless capture harness.
# Usage: capture_ui.sh <before|after>
# Run a build first, then this. Frames land in docs/screens/q09-<phase>-<scene>.png.
set -euo pipefail

phase="${1:-after}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

dll="$repo_root/RaylibUI/bin/Debug/net9.0/RaylibUI.dll"
[[ -f "$dll" ]] || { echo "no build at $dll; build first" >&2; exit 1; }

out_dir="$repo_root/docs/screens"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/q09-$phase-XXXX")"
trap 'rm -rf "$scratch"' EXIT
mkdir -p "$out_dir"

export DOTNET_ROOT="${DOTNET_ROOT:-/home/rhy/.dotnet}"
export LIBGL_ALWAYS_SOFTWARE=1
export HOME=/home/rhy

run_scene() {
    local scene="$1"; shift
    local shot_dir="$scratch/$scene"
    mkdir -p "$shot_dir"
    # One bash loop, bounded; the harness writes a frame per interval.
    env "$@" RHYCIV_SHOT_DIR="$shot_dir" RHYCIV_SHOT_INTERVAL=2 \
        timeout 22 xvfb-run -a "$DOTNET_ROOT/dotnet" "$dll" >/dev/null 2>&1 || true
    local latest; latest="$(ls -1t "$shot_dir"/*.png 2>/dev/null | head -1 || true)"
    if [[ -z "$latest" ]]; then
        echo "no frame captured for $scene" >&2; return 1
    fi
    cp "$latest" "$out_dir/q09-$phase-$scene.png"
    echo "captured $out_dir/q09-$phase-$scene.png ($(identify -format '%wx%h' "$latest" 2>/dev/null || echo '?'))"
}

# Main menu: no autostart, so the game sits at the main menu screen.
run_scene main-menu

# Top menu bar open: start a game and open the first menu dropdown.
RHYCIV_AUTOSTART=1 RHYCIV_AUTOSTART_SEED=1337 RHYCIV_AUTOSTART_CIVS=4 \
    RHYCIV_OPEN_MENU=0 \
    run_scene menu-open

# A dialog: the "A New City" name dialog, headless.
RHYCIV_AUTOSTART=1 RHYCIV_AUTOSTART_SEED=1337 RHYCIV_TEST_POPUP=NAMECITY \
    run_scene dialog

# The city window: found a city and open its window.
RHYCIV_AUTOSTART=1 RHYCIV_AUTOSTART_SEED=1337 RHYCIV_TEST_CITY=1 \
    run_scene city-window

echo "done: docs/screens/q09-$phase-*.png"
