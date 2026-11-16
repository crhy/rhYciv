#!/usr/bin/env python3
"""Keep the dialog and city-screen backgrounds painted, not flat.

``panel.jpg``, ``city_land.jpg``, ``city_river.jpg`` and ``city_ocean.jpg`` sit
behind every dialog and behind the city screen, so they set the tone of the whole
UI. They used to be built by ``build_standalone_sheets.build_backgrounds()`` as a
flat fill with one lighter line every 26 px: measured on a 13 px sample grid that
scores **5 distinct colours**, which is what a placeholder looks like to a script
as well as to a player. They are now cut from the painted slate sheet the dialog
chrome is already cut from (``Backgrounds/stonetexture.png``), each held at its
own slot colour, and this check is what stops the flat version coming back.

Usage
-----
    python3 scripts/check_background_art.py [--check] [--root RaylibUI/FOSSart/Standalone/Backgrounds]

Without ``--check`` the four files are rebuilt from the slate sheet and measured;
with it nothing is written and a file that is missing, the wrong size, or too
close to a flat fill fails the run.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:  # pragma: no cover - depends on the machine, not the code
    Image = None

REPOSITORY = Path(__file__).resolve().parents[1]
BACKGROUNDS = REPOSITORY / "RaylibUI" / "FOSSart" / "Standalone" / "Backgrounds"

# The Civ II logical footprint these are drawn at: the city window and the
# full-screen dialog art. The renderer scales them to fit, so the shipped size
# is what the source master has to be.
SIZE = (1280, 520)

NAMES = ("panel", "city_land", "city_river", "city_ocean")

# Sampling every 13th pixel keeps the count cheap (40 x 99 samples) while still
# reading a 1 px stripe: the placeholder's stripe is 26 px apart, so a 13 px
# grid lands on both the stripe and the field between it.
SAMPLE_STEP = 13

# The placeholder scores 5 distinct colours here and a painted slate cut scores
# over a thousand. Anything under this is a fill, not a painting; the margin is
# wide so a legitimately quieter painting cannot trip it.
MIN_DISTINCT_COLORS = 100


def shown(path: Path) -> str:
    try:
        return str(path.relative_to(REPOSITORY))
    except ValueError:
        return str(path)


def measure(path: Path) -> tuple[tuple[int, int], int]:
    """Return the file's size and how many distinct colours a 13 px grid sees."""
    with Image.open(path) as image:
        size = image.size
        rgb = image.convert("RGB")
    data = rgb.tobytes()
    width, height = size
    colours = set()
    for y in range(0, height, SAMPLE_STEP):
        row = y * width * 3
        for x in range(0, width, SAMPLE_STEP):
            offset = row + x * 3
            colours.add(data[offset:offset + 3])
    return size, len(colours)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=BACKGROUNDS)
    parser.add_argument("--check", action="store_true",
                       help="verify the four backgrounds without writing them")
    args = parser.parse_args()

    # The gate runs on machines set up only to build and test. Reading images
    # needs Pillow, which the rest of the gate does not, so the check stands
    # aside rather than failing a build over a missing tool -- the same policy
    # clean_matte_fringe.py uses.
    if Image is None:
        print("  backgrounds: skipped (needs Pillow)")
        return 0

    if not args.check:
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        import build_standalone_sheets
        build_standalone_sheets.build_backgrounds()

    failures: list[str] = []
    for name in NAMES:
        path = args.root / f"{name}.jpg"
        if not path.exists():
            failures.append(f"{shown(path)}: missing")
            continue
        size, colours = measure(path)
        if size != SIZE:
            failures.append(f"{shown(path)}: {size[0]}x{size[1]}, "
                            f"expected {SIZE[0]}x{SIZE[1]}")
        elif colours < MIN_DISTINCT_COLORS:
            failures.append(f"{shown(path)}: {colours} distinct colours "
                            f"on a {SAMPLE_STEP} px grid, expected at least "
                            f"{MIN_DISTINCT_COLORS} (the flat placeholder scored 5)")
        else:
            print(f"  {name}.jpg  {size[0]}x{size[1]}  {colours} distinct colours")

    if failures:
        for failure in failures:
            print(f"ERROR: {failure}", file=sys.stderr)
        print("Run: python3 scripts/check_background_art.py", file=sys.stderr)
        return 1

    print(f"  backgrounds: {len(NAMES)} painted, {SIZE[0]}x{SIZE[1]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
