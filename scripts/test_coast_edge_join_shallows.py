#!/usr/bin/env python3
"""Regression pin for #155 ("Weird coastal tiles", commit e9823e4b).

The old coast was sixteen pictures keyed by corners, which dammed channels
whose two ends touched land and always painted the same grass and sand. The
fix (see the docstring of scripts/prepare_coast_tiles.py, which is the
contract) keys the shore on edges plus kept corner bits, cuts the land part
out of the sea tile so the neighbour's own terrain shows through, and draws a
beach, a surf line and turquoise shallows over it.

This checks the shipped art in RaylibUI/FOSSart/Terrain/Coast/ against that
contract, without regenerating anything:

a. shore_000.png is fully transparent: open sea grows no band towards land;
b. coast joins: for every legal mask with land on an edge whose mirror mask
   (NE<->SW, SE<->NW edges and N<->S, E<->W corners swapped) is legal and
   shipped, the alpha sampled a few ground px inside that edge equals the
   mirror mask's alpha sampled inside its opposite edge, at the mirrored
   place, so neighbouring tiles meet on the same pixels at the same distance
   and channels stay open;
c. the shore is drawn from the land it meets: every shipped land mask has
   beach-ish pixels (>= 500), and translucent SHALLOW-ish pixels over its
   shoreland region (>= 500 with an edge bit, >= 100 for corner-only masks).

Ground space is the 512x512 tile unsquashed; the art is ground space with y
halved, so a ground point (x, gy) sits at image pixel (x, gy/2).

Exit code 0 when every check passes, 1 otherwise.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

REPOSITORY = Path(__file__).resolve().parents[1]
COAST = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Coast"

# Ground-space corners of the tile (x, 2y), and the edges between them.
N, E, S, W = (256.0, 0.0), (512.0, 256.0), (256.0, 512.0), (0.0, 256.0)
EDGES = [(N, E), (E, S), (S, W), (W, N)]  # NE, SE, SW, NW
CORNER_EDGES = [(3, 0), (0, 1), (1, 2), (2, 3)]  # the two edges beside N, E, S, W
CENTRE = (256.0, 256.0)

# The mirror seen from across a shared edge/point: edge bits 0<->2, 1<->3 and
# corner bits 4<->6, 5<->7.
MIRROR_BITS = {0: 2, 2: 0, 1: 3, 3: 1, 4: 6, 6: 4, 5: 7, 7: 5}

SAND = np.array([205.0, 184.0, 136.0])
WET_SAND = np.array([150.0, 132.0, 96.0])
SHALLOW = np.array([64.0, 156.0, 168.0])

DEPTHS = [1.0, 2.0, 3.0, 5.0]  # ground px inside the edge
TIMES = [0.4 + 0.2 * i / 20 for i in range(21)]  # along the edge, 84 samples per pair
ALPHA_TOLERANCE = 16

MIN_PAIRS = 40
MIN_BEACH = 500
MIN_SHALLOW_EDGE = 500
MIN_SHALLOW_CORNER = 100


def canonical(mask: int) -> int:
    for corner, (a, b) in enumerate(CORNER_EDGES):
        if mask & (1 << a) or mask & (1 << b):
            mask &= ~(1 << (4 + corner))
    return mask


def mirror(mask: int) -> int:
    out = 0
    for bit in range(8):
        if mask & (1 << bit):
            out |= 1 << MIRROR_BITS[bit]
    return out


def legal_masks() -> list[int]:
    return [m for m in range(256) if canonical(m) == m]


def load(name: str) -> np.ndarray:
    return np.asarray(Image.open(COAST / name).convert("RGBA"), dtype=np.int64)


def alpha_at(pixels: np.ndarray, gx: float, gy: float) -> int:
    col = int(round(gx - 0.5))
    row = int(round(gy / 2.0 - 0.5))
    if not (0 <= col < pixels.shape[1] and 0 <= row < pixels.shape[0]):
        raise ValueError(f"ground point ({gx}, {gy}) is outside the tile")
    return int(pixels[row, col, 3])


def inside_samples(pixels: np.ndarray, edge: int) -> list[int]:
    """Alphas at DEPTHS ground px inside the tile's `edge`, at TIMES along it."""
    (ax, ay), (bx, by) = EDGES[edge]
    out = []
    for t in TIMES:
        qx, qy = ax + t * (bx - ax), ay + t * (by - ay)
        dx, dy = CENTRE[0] - qx, CENTRE[1] - qy
        norm = (dx * dx + dy * dy) ** 0.5
        for d in DEPTHS:
            out.append(alpha_at(pixels, qx + d * dx / norm, qy + d * dy / norm))
    assert len(out) == len(DEPTHS) * len(TIMES)
    return out


def rgb_within(pixels: np.ndarray, colour: np.ndarray, tol: float) -> np.ndarray:
    return np.sqrt(((pixels[:, :, :3] - colour) ** 2).sum(axis=2)) <= tol


def main() -> int:
    failures: list[str] = []

    masks = legal_masks()
    if len(masks) != 47:
        failures.append(f"the corner rule leaves {len(masks)} masks, not 47")
    for mask in masks:
        for prefix in ("shore", "shoreland"):
            if not (COAST / f"{prefix}_{mask:03d}.png").exists():
                failures.append(f"{prefix}_{mask:03d}.png is not shipped")

    # a. Open sea draws no band at all.
    open_sea = load("shore_000.png")
    if open_sea[:, :, 3].max() != 0:
        failures.append(
            f"shore_000.png has max alpha {open_sea[:, :, 3].max()}, not 0"
        )

    # b. Coasts join exactly across shared edges: the alpha profile inside
    # every land edge matches the mirror mask's profile inside its opposite.
    shipped = {m: load(f"shore_{m:03d}.png") for m in masks}
    pairs_per_direction = [0] * 4
    worst_pair = None
    for mask in masks:
        for edge in range(4):
            if not mask & (1 << edge):
                continue
            other = mirror(mask)
            if canonical(other) != other or other not in shipped:
                continue
            here = inside_samples(shipped[mask], edge)
            across = inside_samples(shipped[other], MIRROR_BITS[edge])
            diff = max(abs(a - b) for a, b in zip(here, across))
            pairs_per_direction[edge] += 1
            if worst_pair is None or diff > worst_pair[0]:
                worst_pair = (diff, mask, edge)
            if diff > ALPHA_TOLERANCE:
                failures.append(
                    f"shore_{mask:03d} edge {edge} and its mirror edge "
                    f"{MIRROR_BITS[edge]} disagree by {diff} alpha"
                )
    total_pairs = sum(pairs_per_direction)
    if total_pairs < MIN_PAIRS:
        failures.append(f"only {total_pairs} join pairs checked, fewer than {MIN_PAIRS}")

    # c. The shore is drawn from the land it meets: a beach, and translucent
    # shallows over the cut-out land region.
    mins = {"beach": None, "edge": None, "corner": None}
    for mask in masks:
        if mask == 0:
            continue
        pixels = shipped[mask]
        land_region = load(f"shoreland_{mask:03d}.png")[:, :, 0] > 0
        alpha = pixels[:, :, 3]
        beach = (alpha > 0) & (
            rgb_within(pixels, SAND, 60) | rgb_within(pixels, WET_SAND, 60)
        )
        shallow = (
            (alpha > 0)
            & (alpha < 255)
            & land_region
            & rgb_within(pixels, SHALLOW, 100)
        )
        n_beach, n_shallow = int(beach.sum()), int(shallow.sum())
        kind = "edge" if mask & 0b1111 else "corner"
        for key, value in (("beach", n_beach), (kind, n_shallow)):
            mins[key] = value if mins[key] is None else min(mins[key], value)
        limit = MIN_SHALLOW_EDGE if mask & 0b1111 else MIN_SHALLOW_CORNER
        if n_beach < MIN_BEACH:
            failures.append(f"shore_{mask:03d} has only {n_beach} beach-ish pixels")
        if n_shallow < limit:
            failures.append(
                f"shore_{mask:03d} has only {n_shallow} translucent shallow "
                f"pixels over its land, below {limit}"
            )

    print(f"join pairs per direction (NE, SE, SW, NW): {pairs_per_direction}")
    print(f"total join pairs: {total_pairs}, worst alpha diff: {worst_pair[0] if worst_pair else 'n/a'}")
    print(f"min beach-ish pixels: {mins['beach']}, "
          f"min shallow-ish pixels: edge masks {mins['edge']}, corner-only {mins['corner']}")

    if failures:
        for failure in failures:
            print(f"FAIL: {failure}")
        return 1
    print("coast edge joins, open channels and neighbour-cut shallows check out")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
