#!/usr/bin/env python3
"""Regression check for issue #148: FOSS flag and city art must carry real alpha.

The chroma-key work described on #146 converts each painted scene RGB->RGBA so
the alpha sticks and clips it to the isometric diamond, so the flag/city art no
longer renders as a solid opaque field. #148 reported "some pink mask coming
through", but the pink inside the *art* is legitimate flag colour (flag_04,
flag_07); the actual bug would be art that lost its alpha and shows the
painting's matte field again.

So this checks the presence of alpha, not the absence of magenta: every
RaylibUI/FOSSart/Flags/flag_*.png and Cities/*/city_*.png must be RGBA, keep
transparent corners ((0,0) and bottom-right alpha == 0) and hold a non-empty
alpha bbox. A flattened, field-showing PNG fails the corner or bbox checks and
this script exits non-zero; correct keyed art passes.

Called from scripts/quality_gate.sh; run directly with
`python3 scripts/test_flag_city_alpha.py`. PIL + stdlib only, headless.
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "RaylibUI" / "FOSSart"

PATTERNS = ("Flags/flag_*.png", "Cities/*/city_*.png")


def check(path: Path) -> list[str]:
    """Return the reasons this file's alpha is broken (empty means it is fine)."""
    problems: list[str] = []
    with Image.open(path) as image:
        if image.mode != "RGBA":
            return [f"mode is {image.mode}, not RGBA (alpha was flattened away)"]
        alpha = image.getchannel("A")
        for corner, xy in (("top-left", (0, 0)),
                           ("bottom-right", (image.width - 1, image.height - 1))):
            if alpha.getpixel(xy) != 0:
                problems.append(f"{corner} alpha is {alpha.getpixel(xy)}, expected 0 "
                                "(matte field showing through)")
        if not alpha.getbbox():
            problems.append("alpha bbox is empty (fully transparent image)")
    return problems


def main() -> int:
    checked = sorted(path for pattern in PATTERNS for path in ART.glob(pattern))
    if not checked:
        print(f"FAIL: no flag/city art found under {ART}", file=sys.stderr)
        return 1
    failed = 0
    for path in checked:
        problems = check(path)
        if problems:
            failed += 1
            for problem in problems:
                print(f"FAIL: {path.relative_to(ROOT)}: {problem}", file=sys.stderr)
    print(f"flag/city alpha check: {len(checked)} files checked, "
          f"{len(checked) - failed} pass, {failed} fail")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
