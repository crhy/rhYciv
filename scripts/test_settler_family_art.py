#!/usr/bin/env python3
"""Guard the settler-family art (rhYciv#200): settlers must not look like warriors.

PIL + stdlib only; exits non-zero on any failure. Checks, in order:

  1. RaylibUI/FOSSart/Units/settlers.png is RGBA 300x300, keyed on a transparent
     background (alpha == 0 at (0,0)), with a non-empty alpha bounding box.
  2. The sprite is a family at walking scale, not one blob: at least 3 separate
     alpha clusters inside the bounding box (counted by flood-fill on alpha > 0).
  3. It is measurably distinct from warriors.png: the fraction of pixels that
     both sprites cover opaquely and that are near-identical (channel-sum diff
     <= 30) is <= 0.10. The old, warrior-cloned settlers measured 0.329.
  4. The clean-room sheet RaylibUI/FOSSart/Standalone/UNITS.png is up to date
     with the new art: its first unit cell (build_units index 0 == settlers, at
     (1,1), blue marker pixels at (6,0) and (0,32)) is opaque and contains the
     new settler's alpha silhouette.
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[1]
SETTLERS = REPO / "RaylibUI" / "FOSSart" / "Units" / "settlers.png"
WARRIORS = REPO / "RaylibUI" / "FOSSart" / "Units" / "warriors.png"
UNITS = REPO / "RaylibUI" / "FOSSart" / "Standalone" / "UNITS.png"

MAX_DISTINCTNESS = 0.10
MIN_CLUSTERS = 3


def fail(msg: str) -> None:
    print(f"FAIL: {msg}")
    raise SystemExit(1)


def load(path: Path) -> Image.Image:
    if not path.exists():
        fail(f"missing file: {path.relative_to(REPO)}")
    return Image.open(path).convert("RGBA")


def alpha_bbox(im: Image.Image):
    return im.getchannel("A").getbbox()


def alpha_clusters(im: Image.Image) -> int:
    """4-connected components of alpha > 0, counted by flood-fill (no scipy)."""
    from collections import deque

    a = im.getchannel("A")
    w, h = a.size
    px = a.load()
    seen = bytearray(w * h)
    clusters = 0
    for y in range(h):
        for x in range(w):
            if px[x, y] > 0 and not seen[y * w + x]:
                clusters += 1
                q = deque([(x, y)])
                seen[y * w + x] = 1
                while q:
                    cx, cy = q.popleft()
                    for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                        if 0 <= nx < w and 0 <= ny < h and px[nx, ny] > 0 and not seen[ny * w + nx]:
                            seen[ny * w + nx] = 1
                            q.append((nx, ny))
    return clusters


def near_identical_fraction(a: Image.Image, b: Image.Image) -> float:
    """Fraction of pixels both sprites cover opaquely that are near-identical."""
    import numpy as np

    A = np.asarray(a).astype(int)
    B = np.asarray(b).astype(int)
    both = (A[:, :, 3] > 0) & (B[:, :, 3] > 0)
    if not both.any():
        return 0.0
    diff = np.abs(A[:, :, :3] - B[:, :, :3]).sum(axis=2)
    near = (diff <= 30) & both
    return near.sum() / both.sum()


def contain(source: Image.Image, size: tuple[int, int], padding: int = 0) -> Image.Image:
    """Same fit-to-cell placement build_standalone_sheets.contain() uses."""
    source = source.convert("RGBA")
    bounds = source.getchannel("A").getbbox()
    if bounds:
        source = source.crop(bounds)
    available = (max(1, size[0] - 2 * padding), max(1, size[1] - 2 * padding))
    source.thumbnail(available, Image.Resampling.LANCZOS)
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    result.alpha_composite(source, ((size[0] - source.width) // 2, size[1] - padding - source.height))
    return result


def check_sheet_has_new_art(settlers: Image.Image) -> None:
    units = load(UNITS)
    # build_units index 0 sits at cell (1,1); its two marker pixels are the
    # (6,0) and (0,32) blues that delimit the cell on the sheet.
    if units.getpixel((6, 0)) != (0, 0, 255, 255):
        fail("UNITS.png first-cell marker at (6,0) is not the expected blue")
    if units.getpixel((0, 32)) != (0, 0, 255, 255):
        fail("UNITS.png first-cell marker at (0,32) is not the expected blue")

    cell = contain(settlers, (64, 48), 1)
    cell_alpha = cell.getchannel("A")
    cell_px = cell_alpha.load()
    units_px = units.getchannel("A").load()
    missing = 0
    covered = 0
    for y in range(cell.height):
        for x in range(cell.width):
            if cell_px[x, y] > 24:
                covered += 1
                if units_px[1 + x, 1 + y] <= 0:
                    missing += 1
    if covered == 0:
        fail("settler silhouette projects to nothing in the UNITS.png cell")
    if missing:
        fail(f"UNITS.png first cell is stale: {missing} of {covered} settler pixels "
             f"are not opaque there (run scripts/build_standalone_sheets.py)")


def main() -> int:
    if SETTLERS.suffix != ".png":
        fail("unexpected path")

    settlers = load(SETTLERS)
    if settlers.size != (300, 300):
        fail(f"settlers.png must be 300x300, is {settlers.size}")
    if settlers.mode != "RGBA":
        fail(f"settlers.png must be RGBA, is {settlers.mode}")
    if settlers.getpixel((0, 0))[3] != 0:
        fail("settlers.png must be keyed on a transparent background (alpha at (0,0) != 0)")

    bbox = alpha_bbox(settlers)
    if not bbox:
        fail("settlers.png has an empty alpha bounding box (fully transparent)")
    print(f"  alpha bbox = {bbox}, size {bbox[2]-bbox[0]}x{bbox[3]-bbox[1]}")

    clusters = alpha_clusters(settlers)
    if clusters < MIN_CLUSTERS:
        fail(f"settlers.png has {clusters} alpha cluster(s); a walking family needs >= {MIN_CLUSTERS}")
    print(f"  alpha clusters = {clusters}")

    warriors = load(WARRIORS)
    frac = near_identical_fraction(settlers, warriors)
    if frac > MAX_DISTINCTNESS:
        fail(f"settlers still look like warriors: {frac:.3f} of covered pixels near-identical "
             f"(must be <= {MAX_DISTINCTNESS}; old art measured 0.329)")
    print(f"  near-identical vs warriors = {frac:.3f} (limit {MAX_DISTINCTNESS})")

    check_sheet_has_new_art(settlers)
    print("  UNITS.png first cell contains the new settler")

    print("OK: settler art is a distinct walking family and the sheet is fresh")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
