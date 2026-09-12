#!/usr/bin/env python3
"""Paint the river connection tiles and river mouths from the generated art.

Background
----------
Unlike roads, a river is drawn as one picture per tile, chosen by which of the
tile's four edge-sharing neighbours also carry a river:
`TerrainSet.River[mask]`, where the mask has bit 0 for NE, 1 for SE, 2 for SW
and 3 for NW -- the order `MapNavigationFunctions.DirectNeighbours` yields, and
the order MapImage builds the index in. An ocean neighbour counts as connected,
because a river runs to the sea.

That is sixteen distinct pictures, and the bundled art was eight free-hand
meanders assigned as `mask % 8`. So the picture drawn had nothing to do with
which neighbours held a river: a river running north-south could be drawn as an
east-west meander, and no two adjacent tiles lined up. That is what "rivers
disconnected" was.

This script composes all sixteen from half-spokes, exactly as the roads are
built -- each spoke runs from the tile centre to the midpoint of the edge it
crosses, so adjacent tiles meet -- with the painted river swept along them. The
four river mouths are built the same way and fan out as they reach the sea.

Input
-----
~/rhYcivtextures/rivers, painted meanders on a magenta generation matte. Only
the material is taken from them: their own meander is not usable as tile art,
which is what the eight-variant set was trying to do.

Output
------
RaylibUI/FOSSart/Terrain/Overlays/Rivers/river_mask_<00-15>.png and
river_mouth_<ne|se|sw|nw>.png, at tile aspect (2:1).

Usage
-----
    python3 scripts/prepare_river_overlays.py [--source ~/rhYcivtextures/rivers] [--check]
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np

import isotile
from isotile import CANVAS, CENTRE, ENDPOINTS, FOUR

REPOSITORY = Path(__file__).resolve().parents[1]
OUT = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Overlays" / "Rivers"
DEFAULT_SOURCE = Path.home() / "rhYcivtextures" / "rivers"

# Painted width of the channel, in canvas pixels (the tile is 512 wide here). A
# river is a terrain feature rather than a line drawn on the terrain, so it is
# noticeably broader than a road.
RIVER_WIDTH = 44.0

# A river is not one gauge from its spring to the sea. Four bands are painted,
# each from its own source: a trickle, a stream, a river and an estuary. Which
# band a tile draws is decided at render time by how far it is from the mouth.
#
# The multiplier scales the swept channel; the source supplies the material at
# that width, which is the point of having four. Scaling one narrow meander up
# to estuary width stretches its texture and reads as an enlarged creek.
BANDS = [
    ("trickle", 0.55, None),        # no wide source: the narrow profile, run thin
    ("stream", 1.00, None),
    ("river", 1.65, "riverwide4"),
    ("estuary", 2.45, "riverwide6"),
]

# How much wider the channel is where it meets the sea, and how far back from
# the shore the mouth reaches.
MOUTH_FLARE = 1.9
MOUTH_REACH = 0.45


def straightness(path: Path) -> float:
    _, alpha = isotile.key_matte(path)
    ys, xs = np.nonzero(alpha > 0.25)
    points = np.stack([xs, ys], axis=1).astype(np.float64)
    centred = points - points.mean(axis=0)
    values = np.linalg.svd(centred, compute_uv=False)
    return float(values[1] / max(values[0], 1e-6))


def compose(layers: list[tuple[np.ndarray, np.ndarray]]) -> tuple[np.ndarray, np.ndarray]:
    """Merge spokes into one tile, keeping the most opaque contributor.

    Spokes of the same river overlap at the tile centre; averaging them there
    would lighten the channel exactly where it should be at its fullest.
    """
    rgb = np.zeros_like(layers[0][0])
    alpha = np.zeros_like(layers[0][1])
    for layer_rgb, layer_alpha in layers:
        take = layer_alpha > alpha
        alpha[take] = layer_alpha[take]
        rgb[take] = layer_rgb[take]
    return rgb, alpha


def isolated(profile, width: float = RIVER_WIDTH) -> tuple[np.ndarray, np.ndarray]:
    """A river tile whose neighbours carry none: a short stretch through it.

    This is rare -- a river normally reaches the sea or another river tile --
    but a one-tile spring should still read as water rather than as nothing.
    """
    reach = 0.20 * CANVAS[0]
    start = (CENTRE[0] - reach, CENTRE[1] - reach / 2.0)
    end = (CENTRE[0] + reach, CENTRE[1] + reach / 2.0)
    return isotile.sweep(profile, start, end, width * 0.85, seed=77, wobble=0.10)


def build(source_directory: Path, check: bool) -> int:
    sources = sorted(source_directory.glob("river_*.png"))
    if not sources:
        sources = sorted(p for p in source_directory.glob("*.png"))
    if not sources:
        print(f"no source art in {source_directory}", file=sys.stderr)
        return 1

    # The narrow bands come from the straightest meander: the profile is measured
    # along the stroke's own centreline, but a source that doubles back on itself
    # still mixes two passes of the channel into one slice.
    narrow = [path for path in sources if not path.stem.lower().startswith("riverwide")]
    profile = isotile.cross_section(min(narrow or sources, key=straightness))

    # And the wide bands from their own paintings, when they are there. A missing
    # one falls back to the narrow profile rather than failing: the band is then
    # a scaled-up stream, which is what every band was before these existed.
    def profile_for(stem: str | None):
        if stem is None:
            return profile
        for path in sources:
            if path.stem.lower() == stem:
                return isotile.cross_section(path)
        print(f"note: no {stem}.png; that band uses the narrow profile")
        return profile

    OUT.mkdir(parents=True, exist_ok=True)
    written = []

    # Each spoke is generated once and reused across every mask that includes it,
    # so a river crossing two adjacent tiles is drawn with the same channel.
    for band, (name, scale, stem) in enumerate(BANDS):
        band_profile = profile_for(stem)
        width = RIVER_WIDTH * scale

        spokes = {}
        for index, direction in enumerate(FOUR):
            start, end = isotile.spoke_path(direction)
            spokes[direction] = isotile.sweep(band_profile, start, end, width,
                                              seed=2000 + index + 100 * band,
                                              wobble=0.06)

        for mask in range(16):
            connected = [FOUR[bit] for bit in range(4) if mask & (1 << bit)]
            if connected:
                rgb, alpha = compose([spokes[direction] for direction in connected])
            else:
                rgb, alpha = isolated(band_profile, width)
            rgb, alpha = isotile.fill_holes(rgb, alpha)
            target = OUT / f"river_mask_{mask:02d}_{band}.png"
            if not check:
                isotile.to_image(rgb, alpha).save(target, optimize=True)
            written.append(target)

            # Band 1 is also written under the old unbanded name, so anything
            # that has not learned about bands still finds a river to draw.
            if band == 1:
                legacy = OUT / f"river_mask_{mask:02d}.png"
                if not check:
                    isotile.to_image(rgb, alpha).save(legacy, optimize=True)
                written.append(legacy)

    # River mouths are drawn on the *ocean* tile, pointing back at the land
    # neighbour whose river arrives there. The channel is swept from the shore
    # inwards and flares as it goes, so it reads as a delta opening into the sea,
    # and fades rather than ending in a hard edge in open water.
    for index, direction in enumerate(FOUR):
        end = ENDPOINTS[direction]
        inward = (end[0] + (CENTRE[0] - end[0]) * MOUTH_REACH,
                  end[1] + (CENTRE[1] - end[1]) * MOUTH_REACH)
        rgb, alpha = isotile.sweep(profile, end, inward, RIVER_WIDTH,
                                   seed=3000 + index, wobble=0.04,
                                   taper_end=MOUTH_FLARE)
        rgb, alpha = isotile.fill_holes(rgb, alpha)

        # Fade the seaward end out over the last third of its reach.
        height, width = alpha.shape
        ys, xs = np.mgrid[0:height, 0:width]
        towards = np.hypot(xs - end[0], ys - end[1]) / max(
            np.hypot(inward[0] - end[0], inward[1] - end[1]), 1e-6)
        alpha = alpha * np.clip((1.0 - towards) / 0.35 + 1.0, 0.0, 1.0)

        target = OUT / f"river_mouth_{direction}.png"
        if not check:
            isotile.to_image(rgb, alpha).save(target, optimize=True)
        written.append(target)

    missing = [path for path in written if not path.exists()]
    if check:
        if missing:
            print(f"missing river overlays: {', '.join(p.name for p in missing)}",
                  file=sys.stderr)
            return 1
        print(f"  river: {len(written)} sprites present")
        return 0

    print(f"  river: wrote {len(written)} sprites to {OUT}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    return build(args.source, args.check)


if __name__ == "__main__":
    raise SystemExit(main())
