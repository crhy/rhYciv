#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if command -v dotnet >/dev/null 2>&1; then
    dotnet_cmd="$(command -v dotnet)"
elif [[ -x "${HOME}/.dotnet/dotnet" ]]; then
    dotnet_cmd="${HOME}/.dotnet/dotnet"
else
    echo "Error: .NET 9 SDK was not found in PATH or ${HOME}/.dotnet." >&2
    exit 1
fi

if command -v python3 >/dev/null 2>&1; then
    python_cmd="$(command -v python3)"
elif command -v python >/dev/null 2>&1; then
    python_cmd="$(command -v python)"
else
    echo "Error: Python 3 was not found in PATH." >&2
    exit 1
fi

cd "$repo_root"

echo "Auditing redistributable assets..."
"$python_cmd" scripts/generate_asset_manifest.py --check

echo "Checking for leftover generation matte in the art..."
"$python_cmd" scripts/clean_matte_fringe.py --check

echo "Checking that the map-marker cutouts are present and fresh..."
"$python_cmd" scripts/import_map_markers.py --check

echo "Verifying Civilopedia text..."
"$python_cmd" scripts/build_civilopedia_text.py --check

echo "Checking the Flatpak offline NuGet mirror..."
"$python_cmd" scripts/check_flatpak_sources.py

echo "Checking that the build version and the AppStream version agree..."
"$python_cmd" scripts/check_version_consistency.py

echo "Restoring dependencies..."
"$dotnet_cmd" restore rhYciv.sln

echo "Building solution..."
"$dotnet_cmd" build rhYciv.sln --no-restore

echo "Running tests..."
"$dotnet_cmd" test rhYciv.sln --no-build

echo "Quality gate passed."
