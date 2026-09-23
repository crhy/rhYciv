#!/usr/bin/env python3
"""Draw the river tiles and river mouths from the painted river in rhYcivtextures.

Background
----------
A river is one picture per tile, chosen by which of the tile's four
edge-sharing neighbours also carry a river (or are sea): `TerrainSet.River[mask]`
with bit 0 NE, 1 SE, 2 SW, 3 NW -- the order `MapNavigationFunctions.
DirectNeighbours` yields. Each picture is also drawn at four gauges
(`river_mask_<mm>_<band>`, trickle to estuary); MapImage picks the gauge from
the tile's distance to the sea.

Earlier sets either swept a one-pixel cross-section of the paintings along each
spoke (every spoke a different slice, so the banks became orange stripes) or
dropped whole painted scenes onto tiles (nothing joined up). This one keeps the
painting and bends it:

- `riverwide4.png` is a straight painted channel with its banks. It is
  straightened into a rectangle (see painted_strip.py) and made periodic along
  its length, so it can run through any number of tiles.
- Every tile's river runs through the midpoints of the edges it crosses, and
  leaves each edge at right angles. A tile with two connections draws one smooth
  curve from edge to edge; three or four meet at the centre.
- The painting's position along the channel is zero at every tile edge, and its
  two banks are mirror images, so the pixels either side of a tile edge are the
  same pixels whatever shape the two tiles have. Neighbouring tiles join without
  a seam.

River mouths are drawn on the sea tile and carry the channel through the beach
the coast draws there (prepare_coast_tiles.py), widening and fading into the
shallows.

Output
------
RaylibUI/FOSSart/Terrain/Overlays/Rivers/river_mask_<00-15>[_<0-3>].png and
river_mouth_<ne|se|sw|nw>.png, 512x256 (the 64x32 tile at 8x).

Usage
-----
    python3 scripts/prepare_river_overlays.py [--source ~/rhYcivtextures/rivers] [--preview out.png]
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

from painted_strip import Strip, bilinear

REPOSITORY = Path(__file__).resolve().parents[1]
OUT = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Overlays" / "Rivers"
DEFAULT_SOURCE = Path.home() / "rhYcivtextures" / "rivers"
STRIP_NAME = "riverwide4.png"

WIDTH, HEIGHT = 512, 256
CENTRE = np.array([256.0, 256.0])
# Midpoint of each edge, in ground space (x, 2y).
ENDPOINTS = {
    "ne": np.array([384.0, 128.0]),
    "se": np.array([384.0, 384.0]),
    "sw": np.array([128.0, 384.0]),
    "nw": np.array([128.0, 128.0]),
}
FOUR = ["ne", "se", "sw", "nw"]

# The painted strip, measured in its own ground px (see painted_strip.py). The
# water runs to about 140 either side of the centreline; beyond it are the
# banks, and the painting ends at about 235.
SOURCE_WATER = 140.0
SOURCE_EDGE = 232.0
SOURCE_FEATHER = 36.0
# One period of the channel along its length, and the cross-fade that closes it.
SOURCE_PERIOD = 1150.0
SOURCE_FADE = 220.0

# Half-width of the water on the map, in ground px, for each gauge. The tile's
# edge is 362 ground px long. The steps are small: neighbouring tiles are
# usually a gauge apart and meet at their own widths.
BANDS = [("trickle", 24.0), ("stream", 31.0), ("river", 39.0), ("estuary", 48.0)]
LEGACY_BAND = 1

# The slant, in radians, at which every river crosses every tile edge.
CROSSING = 0.42


def ground_grid() -> tuple[np.ndarray, np.ndarray]:
    ys, xs = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float64)
    return xs + 0.5, (ys + 0.5) * 2.0


def diamond(gx: np.ndarray, gy: np.ndarray) -> np.ndarray:
    edge = (np.abs(gx - 256.0) + np.abs(gy - 256.0)) / 256.0
    return np.clip((1.0 + 1.0 / 256.0 - edge) * 128.0, 0.0, 1.0)


class Texture:
    """The painted channel as a periodic rectangle: u in [0, 1), v >= 0."""

    def __init__(self, strip: Strip, nu: int = 1536, nv: int = 256):
        start = strip.u_range[0] + 90.0
        if start + SOURCE_PERIOD + SOURCE_FADE > strip.u_range[1] - 60.0:
            raise SystemExit("the painted strip is too short for one period")
        u = (np.arange(nu) + 0.5) / nu * SOURCE_PERIOD
        v = (np.arange(nv) + 0.5) / nv * SOURCE_EDGE
        uu, vv = np.meshgrid(u, v)
        # One bank only, mirrored: the channel then looks the same from either
        # side, which is what lets any two tiles meet whichever way they run.
        rgb, alpha = strip.sample(start + uu, vv)
        # Close the loop: the first stretch is blended with the painting just
        # past the period's end, which is what the last stretch runs into.
        rgb2, alpha2 = strip.sample(start + SOURCE_PERIOD + uu, vv)
        blend = np.clip(uu / SOURCE_FADE, 0.0, 1.0)
        blend = blend * blend * (3 - 2 * blend)
        self.rgb = rgb2 * (1 - blend[..., None]) + rgb * blend[..., None]
        self.alpha = alpha2 * (1 - blend) + alpha * blend
        feather = np.clip((SOURCE_EDGE - vv) / SOURCE_FEATHER, 0.0, 1.0)
        self.alpha = self.alpha * feather * feather
        self.nu, self.nv = nu, nv

    def sample(self, u: np.ndarray, v: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        x = np.mod(u, 1.0) * self.nu
        y = np.clip(v, 0.0, SOURCE_EDGE) / SOURCE_EDGE * self.nv
        # Wrap the u axis by padding one column.
        rgb = np.concatenate([self.rgb, self.rgb[:, :1]], axis=1)
        alpha = np.concatenate([self.alpha, self.alpha[:, :1]], axis=1)
        colour = bilinear(rgb, x, y)
        a = bilinear(alpha, x, y) * (v < SOURCE_EDGE)
        return colour, a


def rotate(vector: np.ndarray, angle: float) -> np.ndarray:
    c, s = np.cos(angle), np.sin(angle)
    return np.array([c * vector[0] - s * vector[1], s * vector[0] + c * vector[1]])


def inward(endpoint: np.ndarray) -> np.ndarray:
    direction = CENTRE - endpoint
    return direction / np.hypot(*direction)


def curve_points(start: np.ndarray, end: np.ndarray, start_heading: np.ndarray,
                 end_heading: np.ndarray, reach: float, samples: int = 160) -> np.ndarray:
    """A cubic curve leaving ``start`` along ``start_heading`` and arriving at
    ``end`` from ``-end_heading``."""
    t = np.linspace(0.0, 1.0, samples)[:, None]
    length = np.hypot(*(end - start))
    p1 = start + start_heading * reach * length
    p2 = end + end_heading * reach * length
    return ((1 - t) ** 3 * start + 3 * (1 - t) ** 2 * t * p1
            + 3 * (1 - t) * t ** 2 * p2 + t ** 3 * end)


def crossing(endpoint: np.ndarray) -> np.ndarray:
    """The heading a river takes into the tile from this edge.

    Every river crosses every tile edge at the same slant, turned CROSSING from
    square to the edge. Turning the two tiles' inward headings by the same angle
    gives two halves of one straight line through the crossing point, so the
    river runs smoothly from tile to tile; and because it never crosses square,
    it does not straighten up at each edge and swell between them.
    """
    return rotate(inward(endpoint), CROSSING)


# How far a curve is carried straight on past a tile edge it crosses.
OVERRUN = 90.0


class Field:
    """Distance to a curve, and how far along it the nearest point is.

    A curve that ends on a tile edge is carried straight on past it, along the
    heading it crosses at, so that the bank just inside the edge still finds a
    point on the river beside it rather than the curve's end. (It would
    otherwise be painted from a single column of the painting, fanned out.) The
    tile beyond runs along the same line there, so the two agree.
    """

    def __init__(self, points: np.ndarray, gx: np.ndarray, gy: np.ndarray,
                 overrun_start: bool = True, overrun_end: bool = True):
        body = np.diff(points, axis=0)
        body_length = float(np.hypot(body[:, 0], body[:, 1]).sum())
        before = 0.0
        if overrun_start:
            heading = points[0] - points[1]
            heading /= np.hypot(*heading)
            points = np.vstack([points[0] + heading * OVERRUN, points])
            before = OVERRUN
        if overrun_end:
            heading = points[-1] - points[-2]
            heading /= np.hypot(*heading)
            points = np.vstack([points, points[-1] + heading * OVERRUN])
        segment = np.diff(points, axis=0)
        lengths = np.hypot(segment[:, 0], segment[:, 1])
        cumulative = np.concatenate([[0.0], np.cumsum(lengths)]) - before
        self.length = body_length
        best = np.full(gx.shape, np.inf)
        along = np.zeros(gx.shape)
        for (ax, ay), (dx, dy), start, seg_len in zip(points[:-1], segment,
                                                      cumulative[:-1], lengths):
            denominator = max(dx * dx + dy * dy, 1e-12)
            t = np.clip(((gx - ax) * dx + (gy - ay) * dy) / denominator, 0.0, 1.0)
            distance = np.hypot(gx - ax - t * dx, gy - ay - t * dy)
            closer = distance < best
            best = np.where(closer, distance, best)
            along = np.where(closer, start + t * seg_len, along)
        self.distance = best
        self.along = along


def build_fields(gx: np.ndarray, gy: np.ndarray) -> dict:
    fields = {}
    for name in FOUR:
        # To the centre, arriving square-on so three or four can meet there.
        points = curve_points(ENDPOINTS[name], CENTRE, crossing(ENDPOINTS[name]),
                              -inward(ENDPOINTS[name]), 0.45)
        fields[name] = Field(points, gx, gy, overrun_end=False)
    for i, first in enumerate(FOUR):
        for second in FOUR[i + 1:]:
            points = curve_points(ENDPOINTS[first], ENDPOINTS[second],
                                  crossing(ENDPOINTS[first]), crossing(ENDPOINTS[second]),
                                  0.42)
            fields[first + second] = Field(points, gx, gy)
    heading = np.array([1.0, -1.0]) / np.sqrt(2.0)
    fields["spring"] = Field(curve_points(CENTRE - heading * 70, CENTRE + heading * 70,
                                          rotate(heading, 0.6), rotate(-heading, 0.6), 0.4),
                             gx, gy, overrun_start=False, overrun_end=False)
    return fields


def river_tile(texture: Texture, fields: dict, mask: int, half_width: float,
               gx: np.ndarray, gy: np.ndarray) -> Image.Image:
    scale = half_width / SOURCE_WATER          # map px per source px
    period = SOURCE_PERIOD * scale             # one loop of the painting on the map
    connected = [FOUR[bit] for bit in range(4) if mask & (1 << bit)]

    # Each part: (field, u at every pixel, width factor at every pixel).
    parts = []
    if len(connected) == 2:
        field = fields[connected[0] + connected[1]]
        cycles = max(1, round(field.length / period))
        parts.append((field, field.along / field.length * cycles, 1.0))
    elif len(connected) == 1:
        # A river's first tile: it rises here and thins to its source.
        field = fields[connected[0]]
        s = field.along / field.length
        taper = 1.0 - 0.6 * np.clip((s - 0.45) / 0.55, 0.0, 1.0) ** 1.5
        parts.append((field, field.along / period, taper))
    elif not connected:
        field = fields["spring"]
        s = field.along / field.length
        taper = 0.45 + 0.55 * np.sin(np.pi * s)
        parts.append((field, field.along / period, taper))
    else:
        for name in connected:
            field = fields[name]
            parts.append((field, field.along / period, 1.0))

    best = np.full(gx.shape, np.inf)
    u_best = np.zeros(gx.shape)
    for field, u, width in parts:
        v = field.distance / (scale * width)
        closer = v < best
        best = np.where(closer, v, best)
        u_best = np.where(closer, u, u_best)

    rgb, alpha = texture.sample(u_best, best)
    alpha = alpha * diamond(gx, gy)
    return to_image(rgb, alpha)


def mouth_tile(texture: Texture, direction: str, gx: np.ndarray, gy: np.ndarray) -> Image.Image:
    """Drawn on the sea tile: the estuary carried through the beach and out.

    Straight in from the edge's midpoint, widening, its banks giving out first
    and then the water itself fading into the shallows.
    """
    half_width = BANDS[-1][1]
    scale = half_width / SOURCE_WATER
    start = ENDPOINTS[direction]
    inward = CENTRE - start
    unit = inward / np.hypot(*inward)
    rx, ry = gx - start[0], gy - start[1]
    s = rx * unit[0] + ry * unit[1]
    lateral = np.abs(-rx * unit[1] + ry * unit[0])
    flare = 1.0 + 1.3 * np.clip(s / 120.0, 0.0, 1.0) ** 1.6
    v = lateral / (scale * flare)
    u = np.maximum(s, 0.0) / (SOURCE_PERIOD * scale)
    rgb, alpha = texture.sample(u, v)
    bank = v > SOURCE_WATER * 0.92
    fade_bank = np.clip((75.0 - s) / 30.0, 0.0, 1.0)
    fade_water = np.clip((125.0 - s) / 55.0, 0.0, 1.0)
    alpha = alpha * np.where(bank, fade_bank, fade_water) * (s >= -1.0)
    alpha = alpha * diamond(gx, gy)
    return to_image(rgb, alpha)


def to_image(rgb: np.ndarray, alpha: np.ndarray) -> Image.Image:
    data = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255.0, 0, 255)]).astype(np.uint8)
    return Image.fromarray(data, "RGBA")


def find_source(directory: Path) -> Path:
    for path in directory.glob("*.png"):
        if path.name.lower() == STRIP_NAME:
            return path
    raise SystemExit(f"no {STRIP_NAME} in {directory}")


def build(source: Path, preview: Path | None) -> int:
    texture = Texture(Strip(find_source(source)))
    gx, gy = ground_grid()
    fields = build_fields(gx, gy)

    images: dict[str, Image.Image] = {}
    for band, (_, half_width) in enumerate(BANDS):
        for mask in range(16):
            image = river_tile(texture, fields, mask, half_width, gx, gy)
            images[f"river_mask_{mask:02d}_{band}.png"] = image
            if band == LEGACY_BAND:
                images[f"river_mask_{mask:02d}.png"] = image
    for direction in FOUR:
        images[f"river_mouth_{direction}.png"] = mouth_tile(texture, direction, gx, gy)

    OUT.mkdir(parents=True, exist_ok=True)
    for name, image in images.items():
        image.save(OUT / name, optimize=True)
    print(f"  river: wrote {len(images)} sprites to {OUT}")
    if preview is not None:
        write_preview(images, preview)
    return 0


def write_preview(images: dict[str, Image.Image], target: Path) -> None:
    """A little map with rivers winding to the sea, to judge the joins by eye."""
    bits = {"ne": 1, "se": 2, "sw": 4, "nw": 8}
    step = {"ne": (1, -1), "se": (1, 1), "sw": (-1, 1), "nw": (-1, -1)}
    opposite = {"ne": "sw", "se": "nw", "sw": "ne", "nw": "se"}
    river: dict[tuple[int, int], int] = {}
    flow: dict[tuple[int, int], int] = {}
    sea = set()

    def run(start, course):
        tiles = [start]
        for move in course:
            dx, dy = step[move]
            tiles.append((tiles[-1][0] + dx, tiles[-1][1] + dy))
        sea.add(tiles[-1])
        for index, tile in enumerate(tiles[:-1]):
            mask = river.get(tile, 0) | bits[course[index]]
            if index > 0:
                mask |= bits[opposite[course[index - 1]]]
            river[tile] = mask
            flow[tile] = min(flow.get(tile, 99), len(tiles) - 2 - index)

    run((2, 2), ["se", "se", "ne", "se", "se", "sw", "se", "se"])
    run((6, 2), ["sw", "sw", "se"])        # a tributary joining the first
    run((7, 5), ["ne"])                    # a one-tile river into the sea
    canvas = Image.new("RGBA", (WIDTH * 5, HEIGHT * 6), (0, 0, 0, 255))
    grass = Image.new("RGBA", (WIDTH, HEIGHT), (96, 124, 52, 255))
    water = Image.new("RGBA", (WIDTH, HEIGHT), (30, 70, 118, 255))
    gx, gy = ground_grid()
    shape = Image.fromarray((diamond(gx, gy) * 255).astype(np.uint8), "L")
    for ty in range(-1, 13):
        for tx in range(-1, 11):
            if (tx + ty) % 2:
                continue
            position = (tx * WIDTH // 2, ty * HEIGHT // 2)
            canvas.paste(water if (tx, ty) in sea else grass, position, shape)
            if (tx, ty) in river:
                band = max(0, len(BANDS) - 1 - flow[(tx, ty)])
                canvas.alpha_composite(images[f"river_mask_{river[(tx, ty)]:02d}_{band}.png"],
                                       position)
    for (tx, ty) in sea:
        for name, (dx, dy) in step.items():
            if river.get((tx + dx, ty + dy), 0) & bits[opposite[name]]:
                canvas.alpha_composite(images[f"river_mouth_{name}.png"],
                                       (tx * WIDTH // 2, ty * HEIGHT // 2))
    canvas.convert("RGB").save(target)
    print(f"  preview: {target}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    return build(args.source, args.preview)


if __name__ == "__main__":
    raise SystemExit(main())
