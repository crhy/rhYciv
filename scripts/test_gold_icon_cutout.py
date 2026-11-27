#!/usr/bin/env python3
"""Regression checks for the mountain Gold special cutout (crhy/rhYciv#150).

The map's Gold resource used to draw as the flat yellow disc that
`build_standalone_sheets.build_terrain1` falls back to when a terrain's
special has no painted art (fill `(246, 205, 47)` at :312). #150 landed
with `RaylibUI/FOSSart/Terrain/Specials/mountains_1.png`: a keyed cutout
of the gold nugget that `special_source()` picks up at :79/:131/:196,
so the painted cutout, not the disc, reaches TERRAIN1.png and the
runtime `ApplyFossSpecialArt` path. #198 and #182 report the same bug
and share this fix.

This script pins the landed cutout. If the file is deleted, flattened,
or replaced by a uniform yellow disc, one of these checks fails and the
script exits non-zero. Runnable headless: only PIL + stdlib.

Verified 2026-11-27 with PIL 13.0.0 on this checkout: RGBA 300x300,
alpha bbox (21, 174, 279, 299), corner (0,0) alpha==0, centre (150,150)
fully transparent (so not a flat yellow disc).
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PNG = ROOT / "RaylibUI" / "FOSSart" / "Terrain" / "Specials" / "mountains_1.png"

# build_terrain1's fallback disc fill for the gold special (slot 1).
DISC = (246, 205, 47)
TOL = 12


def fail(msg: str) -> None:
    print(f"FAIL: {msg}", file=sys.stderr)
    sys.exit(1)


def main() -> None:
    if not PNG.exists():
        fail(f"missing cutout: {PNG}")
    if PNG.stat().st_size == 0:
        fail(f"empty cutout file: {PNG}")

    with Image.open(PNG) as im:
        if im.mode != "RGBA":
            fail(f"cutout is {im.mode}, expected RGBA")
        bbox = im.getchannel("A").getbbox()
        if not bbox:
            fail("cutout has an empty alpha channel (fully transparent)")
        corner = im.getpixel((0, 0))
        if corner[3] != 0:
            fail(f"corner (0,0) is not transparent: {corner}")
        centre = im.getpixel((im.width // 2, im.height // 2))
        if centre[3] > 0 and all(abs(c - d) <= TOL for c, d in zip(centre[:3], DISC)):
            fail(f"cutout centre {centre} matches the flat yellow disc {DISC}")

    print(f"OK: {PNG.relative_to(ROOT)} is a keyed cutout, not a flat yellow disc")


if __name__ == "__main__":
    main()
