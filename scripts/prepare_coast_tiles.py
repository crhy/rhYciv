#!/usr/bin/env python3
"""Draw the sea and the shore: every sea tile's water, beach, surf and shallows.

Background
----------
The coast used to be sixteen pictures chosen by which of the sea tile's four
*corners* touched land. Two things follow from that and were both wrong on the
map: an edge between two sea tiles whose two ends both touch land was drawn as
land, so a one-tile channel was dammed and a two-tile lake became two ponds; and
the land drawn into the sea tile was always the same grass and sand, whatever
the land beside it was.

This draws the shore from the land itself:

- Land comes into a sea tile along each *edge* whose neighbour is land, as a
  strip; and where land only touches a *corner*, as a quarter-disc of the same
  width around that corner. Edges between two sea tiles stay open water.
- Worked in ground space (the tile unsquashed to a square), the strip and the
  disc both cross a neighbouring sea tile's edge at the same distance from the
  corner, at right angles, and that distance is the same seen from either tile.
  So neighbouring tiles' coastlines meet exactly, with no seam.
- The coastline's wobble is periodic in the map's tile lattice, so it too is the
  same value either side of every edge.
- Nothing in the land part is painted: the sprite is transparent there and ships
  a matching land mask, and the map draws the neighbouring land's own terrain
  under it. A desert coast is desert down to the sand.

The open sea is the painted ocean made seamless and periodic over a 4x4 block
of tiles, cut into the sixteen tiles of that block (`sea_<i>_<j>.png`), so the
sea no longer repeats every tile.

Tile keys
---------
`shore_<mask:03d>.png` / `shoreland_<mask:03d>.png`, where the mask has bit 0-3
for land on the NE, SE, SW, NW edge and bit 4-7 for land touching only the N,
E, S, W corner. A corner bit is only kept when both edges beside it are sea
(otherwise the edge's strip already covers it), which leaves 47 masks.

Output
------
RaylibUI/FOSSart/Terrain/Coast/, 512x256 (the 64x32 tile at 8x).

Usage
-----
    python3 scripts/prepare_coast_tiles.py [--preview out.png]
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image

from painted_strip import bilinear

REPOSITORY = Path(__file__).resolve().parents[1]
OUT = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Coast"
OCEAN = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "ocean.png"

WIDTH, HEIGHT = 512, 256

# Ground-space corners of the tile (x, 2y).
N, E, S, W = (np.array(p, dtype=np.float64) for p in [(256, 0), (512, 256), (256, 512), (0, 256)])
EDGES = [(N, E), (E, S), (S, W), (W, N)]          # NE, SE, SW, NW
CORNERS = [N, E, S, W]
# The two edges beside each corner.
CORNER_EDGES = [(3, 0), (0, 1), (1, 2), (2, 3)]

# How far land reaches into a sea tile, and how much it wanders, ground px.
# The tile's centre is 181 from each edge.
LAND = 62.0
WOBBLE = 24.0
BEACH = 11.0
SHALLOWS = 78.0
# How broadly a cove is rounded where two stretches of coast meet.
ROUNDING = 90.0

SAND = np.array([205.0, 184.0, 136.0])
WET_SAND = np.array([150.0, 132.0, 96.0])
SHALLOW = np.array([64.0, 156.0, 168.0])
FOAM = np.array([236.0, 244.0, 244.0])

# The sea repeats over this many tiles along each diagonal.
SEA_PERIOD = 4


def canonical(mask: int) -> int:
    for corner, (a, b) in enumerate(CORNER_EDGES):
        if mask & (1 << a) or mask & (1 << b):
            mask &= ~(1 << (4 + corner))
    return mask


def ground_grid() -> tuple[np.ndarray, np.ndarray]:
    ys, xs = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float64)
    return xs + 0.5, (ys + 0.5) * 2.0


def diamond(gx: np.ndarray, gy: np.ndarray) -> np.ndarray:
    edge = (np.abs(gx - 256.0) + np.abs(gy - 256.0)) / 256.0
    return np.clip((1.0 + 1.0 / 256.0 - edge) * 128.0, 0.0, 1.0)


def smoothstep(e0: float, e1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def lattice_noise(gx: np.ndarray, gy: np.ndarray, terms) -> np.ndarray:
    """A sum of waves that repeats with the tile lattice.

    Neighbouring tiles sit (+-256, +-256) apart in ground space; a wave with
    integer (a, b) of the same parity over a 512 period takes the same value at
    every one of them, so it agrees with itself across every tile edge.
    """
    total = np.zeros_like(gx)
    for a, b, phase, weight in terms:
        assert (a - b) % 2 == 0
        total += weight * np.sin(2.0 * np.pi * (a * gx + b * gy) / 512.0 + phase)
    return total


COAST_TERMS = [(1, 1, 0.4, 1.0), (3, -1, 2.1, 0.7), (2, 4, 4.4, 0.45), (5, 3, 1.3, 0.3),
               (-7, 5, 5.2, 0.2), (9, -7, 0.7, 0.12), (12, 10, 3.3, 0.08)]
SAND_TERMS = [(21, 17, 0.2, 1.0), (-29, 33, 1.9, 0.8), (40, -38, 4.0, 0.6), (57, 51, 2.2, 0.4)]


def segment_distance(gx, gy, a, b):
    d = b - a
    t = np.clip(((gx - a[0]) * d[0] + (gy - a[1]) * d[1]) / (d @ d), 0.0, 1.0)
    return np.hypot(gx - a[0] - t * d[0], gy - a[1] - t * d[1])


def smooth_min(a: np.ndarray, b: np.ndarray, k: float) -> np.ndarray:
    """min(a, b), rounded where the two are within k of each other."""
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b + (a - b) * h - k * h * (1.0 - h)


def land_distance(mask: int, gx, gy) -> np.ndarray:
    """Distance to the land this sea tile touches.

    Where two strips of land meet at a corner the water would come to a sharp
    point; the smooth minimum rounds it into a cove. Along an edge shared with
    another sea tile the only distances in play are those to that edge's two
    corners, and the other tile sees the same two, so the rounding agrees
    across the edge too.
    """
    parts = []
    for bit, (a, b) in enumerate(EDGES):
        if mask & (1 << bit):
            parts.append(segment_distance(gx, gy, a, b))
    for bit, corner in enumerate(CORNERS):
        if mask & (1 << (4 + bit)):
            parts.append(np.hypot(gx - corner[0], gy - corner[1]))
    distance = parts[0]
    for part in parts[1:]:
        distance = smooth_min(distance, part, ROUNDING)
    return distance


def shore(mask: int, gx, gy, wobble, grain) -> tuple[Image.Image, Image.Image]:
    """The overlay (beach, surf, shallows) and the land mask for one mask."""
    shape = diamond(gx, gy)
    if mask == 0:
        empty = np.zeros(gx.shape)
        return to_rgba(np.zeros(gx.shape + (3,)), empty), to_gray(empty)

    # Signed distance from the waterline: negative on land.
    delta = land_distance(mask, gx, gy) - (LAND + WOBBLE * wobble)

    # Shallows: turquoise over the painted sea, fading out to open water.
    shallow_a = 0.72 * (1.0 - smoothstep(0.0, SHALLOWS, delta)) ** 1.4
    # Surf: a bright line at the waterline and a haze just off it.
    foam_line = np.exp(-((delta - 2.5) / 2.6) ** 2)
    haze = 0.32 * np.exp(-np.maximum(delta, 0.0) / 11.0) * (delta > -1.0)
    foam_a = np.clip(0.85 * foam_line * (0.75 + 0.25 * grain) + haze, 0.0, 0.95)
    # Beach: dry sand at the top, darkening as it reaches the water.
    wet = smoothstep(-BEACH, 0.0, delta)
    sand_rgb = SAND + (WET_SAND - SAND) * wet[..., None]
    sand_rgb = sand_rgb * (0.93 + 0.07 * grain)[..., None]
    sand_a = smoothstep(-BEACH - 3.0, -BEACH + 1.0, delta) * (1.0 - smoothstep(-0.5, 1.5, delta))

    # Composite shallows < foam < sand, all "over".
    rgb = SHALLOW[None, None, :] * np.ones(gx.shape + (1,))
    alpha = shallow_a
    for layer_rgb, layer_a in [(np.broadcast_to(FOAM, rgb.shape), foam_a), (sand_rgb, sand_a)]:
        out_a = layer_a + alpha * (1.0 - layer_a)
        rgb = (layer_rgb * layer_a[..., None] + rgb * (alpha * (1.0 - layer_a))[..., None]) \
            / np.maximum(out_a, 1e-6)[..., None]
        alpha = out_a
    # Land shows through: nothing is drawn where the beach has ended.
    alpha = alpha * (delta > -BEACH - 4.0)

    land = 1.0 - smoothstep(-BEACH - 1.0, -BEACH + 5.0, delta)
    return to_rgba(rgb, alpha * shape), to_gray(land * shape)


def to_rgba(rgb, alpha) -> Image.Image:
    data = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255.0, 0, 255)]).astype(np.uint8)
    return Image.fromarray(data, "RGBA")


def to_gray(value) -> Image.Image:
    return Image.fromarray(np.clip(value * 255.0, 0, 255).astype(np.uint8), "L")


class Sea:
    """The painted ocean, made seamless and periodic over SEA_PERIOD tiles.

    ocean.png is one diamond of sea. Scaled so it spans SEA_PERIOD tiles, the
    diamonds tile the plane; four copies offset by half a diamond are blended so
    each copy is weighted to nothing at its own border, which removes the seams,
    and the blend is rescaled so the ripples keep their contrast.
    """

    def __init__(self, path: Path):
        source = np.asarray(Image.open(path).convert("RGB")).astype(np.float64)
        self.source = source
        self.mean = source[source.sum(axis=2) > 30].mean(axis=0)
        height, width = source.shape[:2]
        self.w_vertex = np.array([0.0, height / 2.0])
        self.a = np.array([width / 2.0, height / 2.0])     # W -> S
        self.b = np.array([width / 2.0, -height / 2.0])    # W -> N
        # Screen px of one tile step, in the source's own pixels.
        self.tile_step_scale = (width / 2.0) / (SEA_PERIOD * 256.0)

    def colour(self, sx: np.ndarray, sy: np.ndarray) -> np.ndarray:
        """Screen coordinates at 512-wide tile scale -> colour."""
        # Lattice coordinates: one period is SEA_PERIOD tile steps (256, 128).
        alpha = (sx / 256.0 + sy / 128.0) / 2.0 / SEA_PERIOD
        beta = (sx / 256.0 - sy / 128.0) / 2.0 / SEA_PERIOD
        total = np.zeros(sx.shape + (3,))
        weights_sq = np.zeros(sx.shape)
        for oa in (0.0, 0.5):
            for ob in (0.0, 0.5):
                fa = np.mod(alpha + oa, 1.0)
                fb = np.mod(beta + ob, 1.0)
                weight = np.sin(np.pi * fa) ** 2 * np.sin(np.pi * fb) ** 2
                # Keep clear of the painted diamond's own anti-aliased rim.
                fa_in = 0.015 + 0.97 * fa
                fb_in = 0.015 + 0.97 * fb
                px = self.w_vertex[0] + fa_in * self.a[0] + fb_in * self.b[0]
                py = self.w_vertex[1] + fa_in * self.a[1] + fb_in * self.b[1]
                sample = bilinear(self.source, px, py)
                total += weight[..., None] * (sample - self.mean)
                weights_sq += weight ** 2
        return self.mean + total / np.sqrt(np.maximum(weights_sq, 1e-6))[..., None]

    def tile(self, i: int, j: int) -> Image.Image:
        ys, xs = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float64)
        # Tile (i, j) along the two diagonals has its top-left corner here.
        ox = (i + j) * 256.0
        oy = (i - j) * 128.0
        rgb = self.colour(ox + xs + 0.5, oy + ys + 0.5)
        gx, gy = ground_grid()
        return to_rgba(rgb, diamond(gx, gy))


def build(preview: Path | None) -> int:
    gx, gy = ground_grid()
    wobble = lattice_noise(gx, gy, COAST_TERMS)
    wobble /= np.abs(wobble).max()
    grain = lattice_noise(gx, gy, SAND_TERMS)
    grain /= np.abs(grain).max()

    OUT.mkdir(parents=True, exist_ok=True)
    masks = sorted({canonical(m) for m in range(256)})
    images = {}
    for mask in masks:
        overlay, land = shore(mask, gx, gy, wobble, grain)
        overlay.save(OUT / f"shore_{mask:03d}.png", optimize=True)
        land.save(OUT / f"shoreland_{mask:03d}.png", optimize=True)
        images[mask] = (overlay, land)
    print(f"  coast: wrote {len(masks)} shore masks to {OUT}")

    sea = Sea(OCEAN)
    seas = {}
    for i in range(SEA_PERIOD):
        for j in range(SEA_PERIOD):
            seas[(i, j)] = sea.tile(i, j)
            seas[(i, j)].save(OUT / f"sea_{i}_{j}.png", optimize=True)
    print(f"  coast: wrote {len(seas)} sea tiles")

    if preview is not None:
        write_preview(images, seas, preview)
    return 0


def write_preview(images, seas, target: Path) -> None:
    """An island, a lake, a channel and a strait, drawn the way MapImage does."""
    art = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain"
    grass = Image.open(art / "grassland.png").convert("RGBA").resize((WIDTH, HEIGHT))
    desert = Image.open(art / "desert.png").convert("RGBA").resize((WIDTH, HEIGHT))
    rows = [
        "..........",
        "..GGGG....",
        ".GGGGGG...",
        ".GG.GGDD..",
        ".GG..GDD..",
        "..GGGG.D..",
        "...GG.DD..",
        "..........",
        ".G.....DD.",
        "..........",
    ]
    # Row r, column c is tile (x, y) = (2c + r%2, r) in doubled coordinates.
    land = {}
    for r, row in enumerate(rows):
        for c, ch in enumerate(row):
            if ch != ".":
                land[(2 * c + r % 2, r)] = grass if ch == "G" else desert

    def is_land(x, y):
        return (x, y) in land

    edge_offsets = [(1, -1), (1, 1), (-1, 1), (-1, -1)]       # NE SE SW NW
    corner_offsets = [(0, -2), (2, 0), (0, 2), (-2, 0)]        # N E S W
    canvas = Image.new("RGBA", (WIDTH * 11, HEIGHT * 6), (0, 0, 0, 255))
    for r in range(len(rows)):
        for c in range(len(rows[0])):
            x, y = 2 * c + r % 2, r
            position = (x * WIDTH // 2, y * HEIGHT // 2)
            if is_land(x, y):
                canvas.alpha_composite(land[(x, y)], position)
                continue
            mask = 0
            neighbour_land = None
            for bit, (dx, dy) in enumerate(edge_offsets):
                if is_land(x + dx, y + dy):
                    mask |= 1 << bit
                    neighbour_land = neighbour_land or land[(x + dx, y + dy)]
            for bit, (dx, dy) in enumerate(corner_offsets):
                if is_land(x + dx, y + dy):
                    mask |= 1 << (4 + bit)
                    neighbour_land = neighbour_land or land[(x + dx, y + dy)]
            mask = canonical(mask)
            tile = seas[((x + y) // 2 % SEA_PERIOD, (x - y) // 2 % SEA_PERIOD)].copy()
            if mask:
                overlay, land_mask = images[mask]
                under = neighbour_land.copy()
                under.putalpha(land_mask)
                tile.alpha_composite(under)
                tile.alpha_composite(overlay)
            canvas.alpha_composite(tile, position)
    canvas.convert("RGB").save(target, quality=90)
    print(f"  preview: {target}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    return build(args.preview)


if __name__ == "__main__":
    raise SystemExit(main())
