#!/usr/bin/env python3
"""Assert the shipped river art is the banded river and the mouths carry it through.

crhy/rhYciv#146 ("Rivers still look ridiculous") was fixed by e9823e4b, which
generates the river overlays from the painted channel (see
prepare_river_overlays.py): every river picture is drawn at four gauges, a
trickle where it rises through a stream and a river to an estuary at the sea,
and four river mouths carry the estuary through the beach the coast draws on
the sea tile. TerrainLoader.cs loads exactly that set
(LoadRiverBands, RiverMouth indexed by FossRiverDirections), so an art drop
that renames, regrows or un-bends the set silently changes what the loader
finds. Nothing else checks the shipped art against that contract, so the
quality gate runs this.

It checks, on RaylibUI/FOSSart/Terrain/Overlays/Rivers/ and nothing else:

- 84 files, all RGBA 512x256: river_mask_<00-15>.png, river_mask_<mm>_<0-3>.png
  (four gauges per mask) and river_mouth_<ne|se|sw|nw>.png.
- the gauges deepen: for every mask 1-15 the alpha>0 pixel count grows strictly
  from gauge 0 to gauge 3 (a mask with no connection draws no water).
- each mouth carries its channel: river_mouth_<dir> has alpha>0 on at least
  1000 pixels, and 1-5 ground px inside edge <dir> (t 0.4-0.6 along it, 84
  samples) its alpha matches river_mask_<1<<bit of dir>>, the single-connection
  river that pours through that edge, within 8.

Ground space is the generator's (x, 2y): corners N(256,0) E(512,256) S(256,512)
W(0,256), edges NE=(N,E) SE=(E,S) SW=(S,W) NW=(W,N); image row y = ground y/2.

Run from the repository root: python3 scripts/test_river_gauges_mouths.py
"""

from __future__ import annotations

import sys
from itertools import product
from pathlib import Path

import numpy as np
from PIL import Image

REPOSITORY = Path(__file__).resolve().parents[1]
OUT = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Overlays" / "Rivers"

WIDTH, HEIGHT = 512, 256
BAND_COUNT = 4
DIRECTIONS = ["ne", "se", "sw", "nw"]
RIVER_BIT = {"ne": 0, "se": 1, "sw": 2, "nw": 3}

# The diamond in ground space (x, 2y), matching prepare_river_overlays.py.
CORNERS = {"N": (256.0, 0.0), "E": (512.0, 256.0), "S": (256.0, 512.0), "W": (0.0, 256.0)}
EDGES = {"ne": ("N", "E"), "se": ("E", "S"), "sw": ("S", "W"), "nw": ("W", "N")}

MOUTHS_MIN_OPAQUE = 1000
MOUTH_AGREEMENT = 8
# 12 positions along the edge between t 0.4 and 0.6, at 7 depths 1-5 ground px
# inside it: 84 samples per mouth.
SAMPLE_T = np.linspace(0.4, 0.6, 12)
SAMPLE_DEPTH = np.linspace(1.0, 5.0, 7)


def expected_files() -> list[str]:
    names = [f"river_mask_{mask:02d}.png" for mask in range(16)]
    names += [f"river_mask_{mask:02d}_{band}.png"
              for mask in range(16) for band in range(BAND_COUNT)]
    names += [f"river_mouth_{direction}.png" for direction in DIRECTIONS]
    return names


def load_alpha(name: str) -> np.ndarray:
    with Image.open(OUT / name) as image:
        if image.mode != "RGBA" or image.size != (WIDTH, HEIGHT):
            raise SystemExit(f"Error: {name} is {image.mode} {image.size}, "
                             f"expected RGBA {WIDTH}x{HEIGHT}")
        return np.asarray(image, dtype=np.int64)[..., 3]


def sample(alpha: np.ndarray, gx: np.ndarray, gy: np.ndarray) -> np.ndarray:
    """Bilinear read at ground coords; pixel (x, y) is centred at (x+.5, (y+.5)*2)."""
    x = gx - 0.5
    y = gy / 2.0 - 0.5
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx, fy = x - x0, y - y0
    return (alpha[y0, x0] * (1 - fx) * (1 - fy) + alpha[y0, x0 + 1] * fx * (1 - fy)
            + alpha[y0 + 1, x0] * (1 - fx) * fy + alpha[y0 + 1, x0 + 1] * fx * fy)


def mouth_samples(direction: str) -> tuple[np.ndarray, np.ndarray]:
    first, last = (np.array(CORNERS[corner]) for corner in EDGES[direction])
    into_tile = np.array([256.0, 256.0]) - (first + last) / 2.0
    into_tile /= np.linalg.norm(into_tile)
    t = SAMPLE_T[:, None]
    depth = SAMPLE_DEPTH[None, :]
    points = ((1 - t) * first + t * last)[:, None, :] + depth[..., None] * into_tile
    return points[..., 0].reshape(-1), points[..., 1].reshape(-1)


def main() -> None:
    failures: list[str] = []

    present = sorted(p.name for p in OUT.glob("*.png"))
    expected = expected_files()
    missing = sorted(set(expected) - set(present))
    extra = sorted(set(present) - set(expected))
    if missing:
        failures.append(f"missing river art: {', '.join(missing)}")
    if extra:
        failures.append(f"unexpected files in {OUT.name}: {', '.join(extra)}")
    if failures:
        raise SystemExit("Error: " + "; ".join(failures))

    alpha = {name: load_alpha(name) for name in expected}

    print(f"{len(expected)} river files, RGBA {WIDTH}x{HEIGHT}")
    for mask in range(1, 16):
        counts = [int((alpha[f"river_mask_{mask:02d}_{band}.png"] > 0).sum())
                  for band in range(BAND_COUNT)]
        print(f"mask {mask:02d}: " + " < ".join(f"{count:,}" for count in counts))
        if not all(a < b for a, b in zip(counts, counts[1:])):
            failures.append(f"mask {mask:02d} gauges not trickle < ... < estuary: {counts}")

    for direction in DIRECTIONS:
        mouth = alpha[f"river_mouth_{direction}.png"]
        opaque = int((mouth > 0).sum())
        river = alpha[f"river_mask_{1 << RIVER_BIT[direction]:02d}.png"]
        gx, gy = mouth_samples(direction)
        worst = int(np.abs(sample(mouth, gx, gy) - sample(river, gx, gy)).max())
        print(f"mouth {direction}: {opaque:,} alpha>0, worst diff {worst} "
              f"at {gx.size} samples inside edge {direction}")
        if opaque < MOUTHS_MIN_OPAQUE:
            failures.append(f"mouth {direction} has only {opaque} alpha>0 pixels, "
                            f"expected at least {MOUTHS_MIN_OPAQUE}")
        if worst > MOUTH_AGREEMENT:
            failures.append(f"mouth {direction} disagrees with "
                            f"river_mask_{1 << RIVER_BIT[direction]:02d} by {worst} "
                            f"at a sample point, allowed {MOUTH_AGREEMENT}")

    if failures:
        for failure in failures:
            print(f"Error: {failure}", file=sys.stderr)
        raise SystemExit(1)
    print("river gauges and mouths check passed.")


if __name__ == "__main__":
    main()
