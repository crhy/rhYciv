#!/usr/bin/env python3
"""Check that the shipped river art joins across tile edges.

The fix for rhYciv#175 (commit e9823e4b, "Rivers bent from the painted river,
and a shore drawn from the land it meets") bends the painted channel along a
curve that crosses every tile edge at the same slant, so two neighbouring tiles
meet on the same pixels at the same angle whatever shape each tile has. This
checks the art that landed, not the generator:

  * every ``river_mask_<00-15>[_<0-3>].png`` is there and decodes as RGBA 512x256;
  * near-edge identity - a few ground pixels inside any edge, a tile with a river
    on that edge carries the same alpha as the single-edge picture there, so the
    two sides of the edge agree on what is drawn;
  * crossing-centroid identity - the weighted centre of where the river meets the
    edge does not depend on the tile's other edges, and is the same at every
    gauge.

Geometry is the generator's ground space: corners N(256,0) E(512,256) S(256,512)
W(0,256), edges NE=(N,E) SE=(E,S) SW=(S,W) NW=(W,N), and image row y = ground y/2.

Usage:
    python3 scripts/test_river_edge_join_strips.py
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

REPOSITORY = Path(__file__).resolve().parents[1]
OUT = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Overlays" / "Rivers"

WIDTH, HEIGHT = 512, 256
BANDS = 4
CENTRE = (256.0, 256.0)

# Bit order of TerrainSet.River: bit 0 NE, 1 SE, 2 SW, 3 NW.
DIRECTIONS = ["ne", "se", "sw", "nw"]
CORNERS = {"N": (256.0, 0.0), "E": (512.0, 256.0), "S": (256.0, 512.0), "W": (0.0, 256.0)}
EDGES = {"ne": ("N", "E"), "se": ("E", "S"), "sw": ("S", "W"), "nw": ("W", "N")}

# How far inside the edge to compare, and how strict the two identities are.
NEAR_DEPTHS = [1.0, 2.0, 3.0, 5.0]
NEAR_SAMPLES = 84
NEAR_TOLERANCE = 8.0
CENTROID_T = (0.2, 0.8)
CENTROID_SAMPLES = 121
CENTROID_DEPTH = 6.0
CENTROID_TOLERANCE = 0.02


def alpha_of(path: Path, problems: list[str]) -> np.ndarray | None:
    """The alpha channel of one overlay, or None and a problem note."""
    if not path.is_file():
        problems.append(f"missing {path.name}")
        return None
    image = Image.open(path)
    if image.mode != "RGBA":
        problems.append(f"{path.name}: mode {image.mode} is not RGBA")
        return None
    if image.size != (WIDTH, HEIGHT):
        problems.append(f"{path.name}: size {image.size} is not {(WIDTH, HEIGHT)}")
        return None
    return np.asarray(image)[:, :, 3].astype(np.float64)


def geometry(direction: str) -> tuple:
    """A start corner, the unit vector along the edge, its length, and the
    inward normal, all in ground pixels."""
    (ax, ay), (bx, by) = CORNERS[EDGES[direction][0]], CORNERS[EDGES[direction][1]]
    ex, ey = bx - ax, by - ay
    length = float(np.hypot(ex, ey))
    nx, ny = CENTRE[0] - (ax + bx) / 2.0, CENTRE[1] - (ay + by) / 2.0
    norm = float(np.hypot(nx, ny))
    return ax, ay, ex / length, ey / length, length, nx / norm, ny / norm


def sample(alpha: np.ndarray, direction: str, ts: np.ndarray, depth: float) -> np.ndarray:
    """The alpha at these positions along this edge, depth ground pixels in."""
    ax, ay, ux, uy, length, nx, ny = geometry(direction)
    gx = ax + ux * length * ts + nx * depth
    gy = ay + uy * length * ts + ny * depth
    # Ground space -> pixel index (the generator's ground_grid in reverse).
    xi = np.rint(gx - 0.5).astype(int)
    yi = np.rint(gy / 2.0 - 0.5).astype(int)
    if (xi < 0).any() or (xi >= WIDTH).any() or (yi < 0).any() or (yi >= HEIGHT).any():
        raise SystemExit(f"sampling outside the {direction} edge of a {WIDTH}x{HEIGHT} overlay")
    return alpha[yi, xi]


def centroid(alpha: np.ndarray, direction: str) -> float | None:
    """Where the river crosses this edge, as a position along it (None if dry)."""
    ts = np.linspace(*CENTROID_T, CENTROID_SAMPLES)
    weights = sample(alpha, direction, ts, CENTROID_DEPTH)
    total = float(weights.sum())
    if total <= 0.0:
        return None
    return float((weights * ts).sum() / total)


def main() -> int:
    problems: list[str] = []
    overlays: dict[str, np.ndarray] = {}
    for mask in range(16):
        for band in range(BANDS):
            name = f"river_mask_{mask:02d}_{band}"
            loaded = alpha_of(OUT / f"{name}.png", problems)
            if loaded is not None:
                overlays[name] = loaded
        name = f"river_mask_{mask:02d}"
        loaded = alpha_of(OUT / f"{name}.png", problems)
        if loaded is not None:
            overlays[name] = loaded
    print(f"  river strips: {len(overlays)} of 80 overlays decoded as RGBA "
          f"{WIDTH}x{HEIGHT}")

    near_checks = 0
    near_worst = 0.0
    centroid_checks = 0
    centroid_worst = 0.0

    for bit, direction in enumerate(DIRECTIONS):
        single = f"river_mask_{1 << bit:02d}"
        if single not in overlays:
            continue
        ts = np.linspace(0.4, 0.6, NEAR_SAMPLES)
        for mask in range(1, 16):
            name = f"river_mask_{mask:02d}"
            if not mask & (1 << bit) or name not in overlays:
                continue
            for depth in NEAR_DEPTHS:
                diff = float(np.abs(sample(overlays[name], direction, ts, depth)
                                    - sample(overlays[single], direction, ts, depth)).max())
                near_checks += 1
                near_worst = max(near_worst, diff)
                if diff > NEAR_TOLERANCE:
                    problems.append(f"{name}.png: alpha differs from {single}.png by {diff:.0f} "
                                    f"at depth {depth:g} inside the {direction} edge")

        for band in range(BANDS):
            reference = f"{single}_{band}"
            if reference not in overlays:
                continue
            wanted = centroid(overlays[reference], direction)
            if wanted is None:
                problems.append(f"{reference}.png: no river crossing the {direction} edge")
                continue
            for mask in range(1, 16):
                if not mask & (1 << bit):
                    continue
                name = f"river_mask_{mask:02d}_{band}"
                if name not in overlays:
                    continue
                got = centroid(overlays[name], direction)
                if got is None:
                    problems.append(f"{name}: dry on the {direction} edge but {reference} flows")
                    continue
                diff = abs(got - wanted)
                centroid_checks += 1
                centroid_worst = max(centroid_worst, diff)
                if diff > CENTROID_TOLERANCE:
                    problems.append(f"{name}: crosses the {direction} edge at t {got:.4f}, "
                                    f"{reference} at t {wanted:.4f}")

    print(f"  near-edge alpha: {near_checks} comparisons, worst difference "
          f"{near_worst:.0f} of 255 (tolerance {NEAR_TOLERANCE:g})")
    print(f"  crossing centroid: {centroid_checks} comparisons, worst offset "
          f"{centroid_worst:.4f} of the edge (tolerance {CENTROID_TOLERANCE:g})")

    if problems:
        for problem in problems:
            print(f"  FAIL: {problem}")
        print(f"  river edge joins: {len(problems)} problems")
        return 1
    print("  river edge joins: ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
