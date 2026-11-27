#!/usr/bin/env python3
"""Pin the shipped rounded corner-disc shore masks and land masks.

Issue #147 asked for the inner sea corners to be "a bit rounded on the
outside edge, blending into the other terrain better". Commit e9823e4b
drove that from scripts/prepare_coast_tiles.py: a shore tile takes land
along each *edge* whose neighbour is land, as a strip, and where land
only touches a *corner*, as a quarter-disc of the same width around that
corner -- a rounded point, not the old whole-corner wedge -- and the
land part is cut from the neighbour's own terrain, shipped as a matching
land mask the map draws the neighbour's terrain under.

TerrainLoader.LoadShore loads both halves, `shore_<mask:03d>.png` and
`shoreland_<mask:03d>.png`, and bails to the old 16-corner CoastMarch art
if either half is missing, so this pins the shipped set without
regenerating anything:

- exactly the masks the prepare_coast_tiles.py tile-keys rule allows
  (bit 0-3 for land on the NE, SE, SW, NW edge, bit 4-7 for land
  touching only the N, E, S, W corner, and a corner bit only kept when
  both edges beside it are sea) -- which leaves 47 masks;
- the 15 corner-only masks are there, and their land masks hold a
  rounded disc: neither empty nor a whole-corner wedge (0 < land pixels
  < 20,000), and more corner bits mask more land (mask 16 < mask 240);
- every shore overlay is RGBA 512x256 and every land mask is a 512x256
  L grayscale that decodes, so a truncated, flattened or wrong-size
  file fails here instead of silently falling back to the old art.
"""

from __future__ import annotations

import sys
from pathlib import Path
from PIL import Image

REPOSITORY = Path(__file__).resolve().parents[1]
COAST = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Coast"

# The two diamond corners each diamond edge ends at, in mask bits:
# bit 0-3 = land on the NE, SE, SW, NW edge; bit 4-7 = land touching
# only the N, E, S, W corner (prepare_coast_tiles.py, tile keys).
CORNER_EDGES = {4: (0, 3), 5: (0, 1), 6: (1, 2), 7: (2, 3)}

TILE = (512, 256)  # the 64x32 tile at 8x

# A corner-only land mask is a quarter-disc of shore-strip width. The
# shipped discs run 966 px (one corner) to 5,483 px (all four); a whole
# corner wedge would be ~16,000 px, so 20,000 separates disc from wedge.
LAND_MIN = 0
LAND_MAX = 20_000


def legal_masks() -> set[int]:
    """The masks the tile-keys rule allows: 47 of the 256 combinations.

    A corner bit is only kept when both edges beside it are sea --
    otherwise the edge's strip already covers it.
    """
    masks = set()
    for mask in range(256):
        edges = mask & 0xF
        corners = (mask >> 4) & 0xF
        if all(
            not edges & (1 << e)
            for bit in range(4)
            if corners >> bit & 1
            for e in CORNER_EDGES[4 + bit]
        ):
            masks.add(mask)
    return masks


def mask_of(path: Path, prefix: str) -> int | None:
    part = path.name[len(prefix) : -len(".png")]
    return int(part) if len(part) == 3 and part.isdigit() else None


def land_pixels(path: Path) -> int:
    with Image.open(path) as im:
        im.verify()
        mode, size = im.mode, im.size
    if mode != "L":
        raise ValueError(f"{path.name} is {mode}, not L grayscale")
    if size != TILE:
        raise ValueError(f"{path.name} is {size}, not {TILE}")
    with Image.open(path) as im:
        return sum(1 for byte in im.tobytes() if byte)


def main() -> int:
    failures: list[str] = []

    shore, stray = {}, []
    for path in COAST.glob("shore_*.png"):
        mask = mask_of(path, "shore_")
        if mask is None:
            stray.append(path.name)
        else:
            shore[mask] = path
    if stray:
        failures.append(f"shore overlays with unexpected names: {sorted(stray)}")

    shipped = set(shore)
    allowed = legal_masks()
    if len(allowed) != 47:
        failures.append(f"the tile-keys rule derived {len(allowed)} masks, expected 47")
    for missing in sorted(allowed - shipped):
        failures.append(f"shore_{missing:03d}.png is missing")
    for extra in sorted(shipped - allowed):
        failures.append(f"shore_{extra:03d}.png is illegal under the tile-keys rule")

    corner_only = sorted(mask for mask in allowed if not mask & 0xF and mask >> 4)
    if len(corner_only) != 15:
        failures.append(f"derived {len(corner_only)} corner-only masks, expected 15")

    discs: dict[int, int] = {}
    for mask in corner_only:
        path = COAST / f"shoreland_{mask:03d}.png"
        if not path.is_file():
            failures.append(f"shoreland_{mask:03d}.png is missing")
            continue
        try:
            n = land_pixels(path)
        except (OSError, ValueError) as exc:
            failures.append(str(exc))
            continue
        discs[mask] = n
        if not LAND_MIN < n < LAND_MAX:
            failures.append(
                f"shoreland_{mask:03d}.png has {n} land pixels, "
                f"a corner disc must hold {LAND_MIN} < n < {LAND_MAX}"
            )
    for fewer in corner_only:
        for more in corner_only:
            if fewer in discs and more in discs:
                if bin(fewer >> 4).count("1") < bin(more >> 4).count("1"):
                    if not discs[fewer] < discs[more]:
                        failures.append(
                            f"mask {fewer} masks {discs[fewer]} px but its "
                            f"subset mask {more} only masks {discs[more]} px"
                        )

    for mask, path in shore.items():
        try:
            with Image.open(path) as im:
                im.verify()
                if im.mode != "RGBA":
                    failures.append(f"{path.name} is {im.mode}, not RGBA")
                if im.size != TILE:
                    failures.append(f"{path.name} is {im.size}, not {TILE}")
        except OSError as exc:
            failures.append(f"{path.name} does not decode: {exc}")

    land_files = sorted(COAST.glob("shoreland_*.png"))
    for path in land_files:
        mask = mask_of(path, "shoreland_")
        if mask is None:
            failures.append(f"{path.name} has an unexpected name")
        elif mask not in allowed:
            failures.append(f"{path.name} has no matching shore overlay")
        try:
            with Image.open(path) as im:
                im.verify()
                if im.mode != "L":
                    failures.append(f"{path.name} is {im.mode}, not L grayscale")
                if im.size != TILE:
                    failures.append(f"{path.name} is {im.size}, not {TILE}")
        except OSError as exc:
            failures.append(f"{path.name} does not decode: {exc}")
    for mask in sorted(allowed):
        if not (COAST / f"shoreland_{mask:03d}.png").is_file():
            failures.append(f"shoreland_{mask:03d}.png is missing")

    print(f"shipped shore overlays: {len(shore)}")
    print(f"shipped shoreland masks: {len(land_files)}")
    print(f"legal under the tile-keys rule: {len(allowed)}")
    print(f"corner-only (rounded disc) masks: {len(corner_only)}")
    if discs:
        print(
            f"corner disc land pixels: mask 16 = {discs.get(16)}, "
            f"mask 240 = {discs.get(240)} (all 15 in "
            f"{min(discs.values())}..{max(discs.values())})"
        )

    if failures:
        for failure in failures:
            print(f"FAIL: {failure}", file=sys.stderr)
        return 1
    print("Corner-disc shore masks OK.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
