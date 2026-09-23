#!/usr/bin/env python3
"""Generate or verify the redistribution manifest for every shipped media asset."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path


REPOSITORY = Path(__file__).resolve().parents[1]
MANIFEST = REPOSITORY / "ASSET-MANIFEST.tsv"
FOSS_ART = REPOSITORY / "RaylibUI" / "FOSSart"
FREECIV_COMMIT = "94beba8bc7d6512e485ae35103bdb8fb55babb4f"
FREECIV_URL = f"https://github.com/freeciv/freeciv/tree/{FREECIV_COMMIT}/data/civ2"


def attribution(relative: str) -> tuple[str, str, str, str, str]:
    """Return kind, author, SPDX license, source, and generator for a path."""
    if relative.startswith("UI.Classic/Fonts/"):
        return (
            "third-party-font",
            "Google Corporation and Red Hat, Inc.; Liberation Fonts contributors",
            "OFL-1.1",
            "https://github.com/liberationfonts/liberation-fonts",
            "unmodified upstream font",
        )
    if relative == "UI.Classic/buttons.png":
        return (
            "upstream-project-original",
            "Reuben Evans",
            "GPL-3.0-only",
            "https://github.com/axx0/Civ2-clone/commit/652bb54eb2a8f984ff6d0ec208e67dd4f17bd7db",
            "none",
        )
    if relative == "UI.Classic/explorer_icons.png":
        return (
            "upstream-project-original",
            "axx0/Civ2-clone contributors",
            "GPL-3.0-only",
            "https://github.com/axx0/Civ2-clone/commit/19055289d19dd7ce1e65cd5bcc84135a46872e33",
            "none",
        )
    if relative in {
        "RaylibUI/FOSSart/Standalone/RULES.txt",
        "RaylibUI/FOSSart/Standalone/CITY.txt",
    }:
        return (
            "freeciv-derived-data",
            "Freeciv contributors; adapted by rhYciv contributors",
            "GPL-2.0-or-later",
            FREECIV_URL,
            "scripts/import_freeciv_rules.py",
        )
    if relative in {
        "RaylibUI/FOSSart/Standalone/describe.txt",
        "RaylibUI/FOSSart/Standalone/pedia.txt",
    }:
        return (
            "project-original-text",
            "rhYciv contributors",
            "GPL-3.0-only",
            "docs/CIVILOPEDIA-TEXT.md",
            "scripts/build_civilopedia_text.py",
        )
    if relative.startswith("RaylibUI/FOSSart/Standalone/") and Path(relative).name in {
        "CITIES.png", "ICONS.png", "TERRAIN1.png", "TERRAIN2.png", "UNITS.png",
        "VIEWPIECE.png"
    }:
        return (
            "generated-layout-adapter",
            "rhYciv contributors",
            "GPL-3.0-only",
            "project-original inputs listed in this manifest",
            "scripts/build_standalone_sheets.py",
        )
    if relative.startswith("RaylibUI/FOSSart/Icons/"):
        return (
            "generated-project-art",
            "crhy and rhYciv contributors",
            "GPL-3.0-only",
            "project-original Civilopedia art listed in this manifest",
            "scripts/build_foss_icons.sh",
        )
    if relative.startswith("RaylibUI/FOSSart/"):
        generator = "none"
        if "/People/" in f"/{relative}":
            generator = "scripts/prepare_people_sheet.py"
        elif "/Overlays/Roads/" in f"/{relative}" or "/Overlays/Railroads/" in f"/{relative}":
            # Connection spokes are cut from the straight-through road and rail
            # pieces by their own script, not the general texture cleaner.
            generator = "scripts/prepare_road_overlays.py"
        elif "/Units/" in f"/{relative}" or "/Cities/" in f"/{relative}" or "/Flags/" in f"/{relative}" or "/Overlays/" in f"/{relative}":
            generator = "scripts/prepare_custom_textures.py"
        elif "/Terrain/Specials/" in f"/{relative}":
            # Most specials are keyed from the rhYcivtextures source set; the
            # mountain gold cutout is taken from the orphaned Other/gold.jpg
            # painting by its own importer (issue #150).
            if relative.endswith("mountains_1.png"):
                generator = "scripts/import_special_from_other.py"
            else:
                generator = "scripts/prepare_custom_textures.py"
        elif relative in {
            "RaylibUI/FOSSart/Terrain/goodyhut.png",
            "RaylibUI/FOSSart/Other/deadtroop.png",
        }:
            # The two map markers (goody hut, killed-unit marker) are cut from
            # their magenta generation matte by their own importer (issue #77).
            generator = "scripts/import_map_markers.py"
        elif "/Terrain/" in f"/{relative}" and relative.endswith(".png"):
            # Painted base diamonds and special-resource cutouts are keyed and
            # downsampled from the rhYcivtextures source set by this script; the
            # legacy square .jpg tiles beside them are hand-made originals.
            generator = "scripts/prepare_custom_textures.py"
        elif "/Standalone/Backgrounds/" in f"/{relative}":
            generator = "scripts/build_standalone_sheets.py"
        return (
            "project-original-art",
            "crhy and rhYciv contributors",
            "GPL-3.0-only",
            "https://github.com/crhy/rhYciv",
            generator,
        )
    raise ValueError(f"No attribution rule for {relative}")


def assets() -> list[Path]:
    files = [
        REPOSITORY / "UI.Classic" / "buttons.png",
        REPOSITORY / "UI.Classic" / "explorer_icons.png",
        *sorted((REPOSITORY / "UI.Classic" / "Fonts").glob("*.ttf")),
    ]
    files.extend(
        path for path in FOSS_ART.rglob("*")
        if path.is_file() and path.name not in {"SOURCES.md", "ASSET-MANIFEST.tsv"}
    )
    # Sort on the POSIX relative path, not on the Path objects. Comparing Path
    # objects uses the platform's own rules, and on Windows those are
    # case-insensitive: "Overlays" sorts after "ocean.jpg" there and before it on
    # Linux. That reorders the manifest rows, so the committed file looked stale
    # on Windows and only on Windows.
    return sorted(files, key=lambda path: path.relative_to(REPOSITORY).as_posix())


def render() -> str:
    header = "path\tsha256\tbytes\tkind\tauthor\tlicense\tsource\tgenerator\n"
    rows = []
    for path in assets():
        relative = path.relative_to(REPOSITORY).as_posix()
        kind, author, license_id, source, generator = attribution(relative)
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        rows.append(
            "\t".join((relative, digest, str(path.stat().st_size), kind, author,
                        license_id, source, generator))
        )
    return header + "\n".join(rows) + "\n"


def audit(content: str) -> list[str]:
    errors: list[str] = []
    lines = content.splitlines()
    expected_header = "path\tsha256\tbytes\tkind\tauthor\tlicense\tsource\tgenerator"
    if not lines or lines[0] != expected_header:
        errors.append("asset manifest header is missing or invalid")
        return errors
    rows = [line.split("\t") for line in lines[1:] if line]
    for number, row in enumerate(rows, 2):
        if len(row) != 8 or any(not value for value in row):
            errors.append(f"manifest line {number} is incomplete")
    if any((REPOSITORY / "RhyCiv.Tests" / "TestFiles").glob("*.sav")):
        errors.append("legacy save fixtures are present in RhyCiv.Tests/TestFiles")
    forbidden_fonts = ("ARIAL.TTF", "times-new-roman.ttf", "times-new-roman-bold.ttf")
    for name in forbidden_fonts:
        if (REPOSITORY / "UI.Classic" / name).exists():
            errors.append(f"commercial font remains: UI.Classic/{name}")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="verify without rewriting")
    args = parser.parse_args()
    generated = render()
    errors = audit(generated)
    if MANIFEST.exists() and args.check and MANIFEST.read_text(encoding="utf-8") != generated:
        errors.append("ASSET-MANIFEST.tsv is stale; run scripts/generate_asset_manifest.py")
    elif not MANIFEST.exists() and args.check:
        errors.append("ASSET-MANIFEST.tsv is missing")
    elif not args.check:
        MANIFEST.write_text(generated, encoding="utf-8", newline="\n")
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        return 1
    print(f"Asset manifest verified: {len(assets())} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
