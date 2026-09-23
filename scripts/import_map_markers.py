#!/usr/bin/env python3
"""Cut the painted map markers out of their generation matte.

These are the small pictures the map draws on top of a tile rather than as part
of the terrain: the goody hut, and the marker left where a unit was killed.

Both arrive as square art on a saturated magenta matte, which is keyed the same
way as the connection overlays -- on the shape of the colour rather than on
distance from one sampled value, because the matte is flat only to within a few
levels and the anti-aliased edge has to be unmixed rather than eroded.

The output keeps the source's aspect and is trimmed to its ink, so
TerrainLoader can scale it against the working tile.

Usage
-----
    python3 scripts/import_map_markers.py [--source ~/rhYcivtextures] [--check]
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

import isotile

REPOSITORY = Path(__file__).resolve().parents[1]
FOSSART = REPOSITORY / "RaylibUI" / "FOSSart"
DEFAULT_SOURCE = Path.home() / "rhYcivtextures"

# Source file -> where it ships. The goody hut sits with the terrain it is drawn
# on; the dead-unit marker with the other map overlays.
MARKERS = {
    "newgoodyhut.png": FOSSART / "Terrain" / "goodyhut.png",
    "deadtroop.png": FOSSART / "Other" / "deadtroop.png",
}

# Longest edge of the shipped marker. The map composes a tile at 128x64 at the
# default render scale and up to 512x256 zoomed in, and these are drawn well
# inside the tile, so more than this is never sampled.
SIZE = 384


def cut(path: Path) -> Image.Image:
    rgb, alpha = isotile.key_matte(path)
    image = Image.fromarray(
        np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255.0, 0, 255)]).astype(np.uint8),
        "RGBA")

    bounds = image.getbbox()
    if bounds is None:
        raise SystemExit(f"{path.name}: nothing left after keying the matte")
    image = image.crop(bounds)
    image.thumbnail((SIZE, SIZE), Image.LANCZOS)
    return image


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument(
        "--check", action="store_true",
        help="fail if a shipped marker is missing or stale against its source",
    )
    args = parser.parse_args()

    if args.check:
        stale = []
        for name, target in MARKERS.items():
            source = args.source / name
            if not target.exists():
                stale.append(f"{target.name}: missing")
                continue
            # When the source art is available, also require the shipped cutout
            # to match a fresh key of it; without the source, existence is all
            # this machine can verify.
            if source.exists():
                generated = cut(source)
                with Image.open(target) as shipped:
                    if (shipped.size != generated.size
                            or shipped.tobytes() != generated.tobytes()):
                        stale.append(f"{target.name}: stale")
            else:
                print(f"  note: {source} absent; only checked that "
                      f"{target.name} exists", file=sys.stderr)
        if stale:
            print(
                "stale map markers: " + ", ".join(stale)
                + "; re-run scripts/import_map_markers.py",
                file=sys.stderr,
            )
            return 1
        print(f"  markers: {len(MARKERS)} present")
        return 0

    missing = [name for name in MARKERS if not (args.source / name).exists()]
    if missing:
        print(f"missing source art in {args.source}: {', '.join(missing)}", file=sys.stderr)
        return 1

    for name, target in MARKERS.items():
        marker = cut(args.source / name)
        target.parent.mkdir(parents=True, exist_ok=True)
        marker.save(target, optimize=True)
        opaque = np.asarray(marker)[..., 3] > 8
        print(f"  {target.name}  {marker.size[0]}x{marker.size[1]}  "
              f"{100 * opaque.mean():.0f}% covered")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
