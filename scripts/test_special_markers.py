#!/usr/bin/env python3
"""The four painted special-resource markers must exist and must not be discs.

crhy/rhYciv#18: coal, iron, tusked ivory and the oil slick were the last
specials drawn as the flat fallback ellipse in build_terrain1. This pins that
each cutout is really there, is a keyed 300x300 RGBA sheet in the shape the
pipeline expects, and carries its own art at its centre instead of the two
fallback disc colours build_standalone_sheets draws when Specials/ is empty.
It fails if a cutout is deleted or flattened (a pasted background or an RGB
mode leaves the corner non-transparent or the mode wrong).

It also pins the regenerated TERRAIN2 sheet: the 16 river cells the
compatibility UIs read at runtime must still hold a painted river, not the
blank row build_standalone_sheets used to leave when river_NN.png was missing.

PIL and stdlib only; non-zero exit on any failure.
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

REPOSITORY = Path(__file__).resolve().parents[1]
SPECIALS = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Specials"
SHEETS = REPOSITORY / "RaylibUI" / "FOSSart" / "Standalone"

MARKERS = ("hills_1", "mountains_2", "glacier_1", "swamp_2")

# The two fallback ellipse fills in build_standalone_sheets.build_terrain1.
DISC_YELLOW = (246, 205, 47)
DISC_GREY = (235, 235, 235)

# A painted river band covers its cell centre; this only has to be enough to
# tell art from the stray pixel a blank row leaves behind.
MIN_RIVER_PIXELS = 32

failures: list[str] = []


def check(marker: str) -> None:
    path = SPECIALS / f"{marker}.png"
    if not path.exists():
        failures.append(f"{marker}: {path.relative_to(REPOSITORY)} is missing")
        return
    with Image.open(path) as image:
        if image.mode != "RGBA":
            failures.append(f"{marker}: is {image.mode}, expected keyed RGBA")
            return
        if image.size != (300, 300):
            failures.append(f"{marker}: is {image.size}, expected 300x300")
            return
        alpha = image.getchannel("A")
        bbox = alpha.getbbox()
        if bbox is None:
            failures.append(f"{marker}: is fully transparent")
            return
        if alpha.getpixel((0, 0)) != 0:
            failures.append(f"{marker}: corner (0,0) is not transparent")
        if bbox[2] - bbox[0] < 100 or bbox[3] - bbox[1] < 50:
            failures.append(f"{marker}: alpha bbox {bbox} is too small to be a "
                            "painted marker (the fallback disc is ~11x10)")

        # The centre of the artwork must be the artwork, not the flat fallback
        # disc colour build_terrain1 used to draw there.
        cx = (bbox[0] + bbox[2]) // 2
        cy = (bbox[1] + bbox[3]) // 2
        for dx in range(-12, 13, 4):
            for dy in range(-12, 13, 4):
                x, y = cx + dx, cy + dy
                r, g, b, a = image.getpixel((x, y))
                if a == 0:
                    continue
                if (r, g, b) in (DISC_YELLOW, DISC_GREY):
                    failures.append(f"{marker}: pixel {(x, y)} is the flat "
                                    f"fallback disc colour {(r, g, b)}")
                    return


def check_river_row() -> None:
    """Every TERRAIN2 river cell must still carry painted pixels.

    crhy/rhYciv#18 follow-up: regenerating the sheet once left those rows blank,
    because build_standalone_sheets skipped a missing river_NN.png and nothing
    else painted the cell. UI.CompatAlternate and UI.Compact read exactly these
    16 cells (CompatAlternateInterface.cs:217-219) as the runtime river sprites,
    so a blank cell silently takes every river off that UI's map.
    """
    path = SHEETS / "TERRAIN2.png"
    if not path.exists():
        failures.append(f"terrain2: {path.relative_to(REPOSITORY)} is missing")
        return
    with Image.open(path) as opened:
        sheet = opened.convert("RGBA")
    if sheet.size != (530, 480):
        failures.append(f"terrain2: is {sheet.size}, expected the 530x480 sheet")
        return
    for index in range(16):
        x = 1 + 65 * (index % 8)
        y = 67 + 33 * (index // 8)
        alpha = sheet.crop((x, y, x + 64, y + 32)).getchannel("A")
        painted = sum(alpha.histogram()[1:])
        if painted < MIN_RIVER_PIXELS:
            failures.append(
                f"terrain2: river cell {index} at ({x}, {y}) holds {painted} painted "
                f"pixels, not a painted river; build_standalone_sheets should compose it "
                f"from Overlays/Rivers/river_mask_{index:02}.png")


def main() -> int:
    for marker in MARKERS:
        check(marker)
    check_river_row()
    if failures:
        for failure in failures:
            print(f"FAIL {failure}", file=sys.stderr)
        return 1
    print(f"special markers ok: {', '.join(MARKERS)}")
    print("terrain2 river row ok: all 16 cells painted")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
