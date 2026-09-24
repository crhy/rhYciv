#!/usr/bin/env python3
"""Paint three more versions of every terrain diamond, so a field is not a quilt.

Every tile of a terrain used to be the same painting, and a desert of twenty
tiles was twenty copies of the same dunes in a grid (#18, #156). This writes
three variants beside each painting in RaylibUI/FOSSart/Terrain/:

- ``<name>_1.png``: the painting mirrored left to right.
- ``<name>_2.png`` and ``<name>_3.png``: patchworks of the painting and its
  mirror image, cut along soft irregular blobs, each with its own blobs.

The map picks a variant per tile from its position, and where two tiles of the
same terrain carry different variants it blends them along a wandering border,
exactly as it does between two terrains.

Each variant keeps the original's framing: the game crops the same central part
of it (TerrainLoader keeps 82%, past the painted slab's rim), so only that part
is repainted.

Usage
-----
    python3 scripts/prepare_terrain_variants.py
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image


REPOSITORY = Path(__file__).resolve().parents[1]
TERRAIN = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain"
NAMES = ["desert", "plains", "grassland", "tundra", "glacier", "swamp", "jungle"]
# TerrainLoader crops to this much of the painting before fitting it to a tile.
KEEP = 0.82


def blob_mask(height: int, width: int, seed: int) -> np.ndarray:
    """A soft-edged mask of irregular blobs covering about half the image."""
    rng = np.random.default_rng(seed)
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float64)
    u, v = xs / width, ys / height * 0.5      # isometric: squash the blobs
    total = np.zeros_like(u)
    for frequency, weight in [(2.3, 1.0), (4.7, 0.55), (9.1, 0.28), (17.3, 0.14)]:
        for _ in range(2):
            angle = rng.uniform(0, np.pi)
            phase = rng.uniform(0, 2 * np.pi)
            total += weight * np.sin(2 * np.pi * frequency * (u * np.cos(angle) + v * np.sin(angle) * 2.0) + phase)
    total /= np.abs(total).max()
    return np.clip(total / 0.06 + 0.5, 0.0, 1.0)


def main() -> int:
    written = 0
    for name in NAMES:
        path = TERRAIN / f"{name}.png"
        if not path.exists():
            continue
        image = np.asarray(Image.open(path).convert("RGBA")).astype(np.float64)
        rgb, alpha = image[..., :3], image[..., 3] / 255.0
        mirrored = rgb[:, ::-1]
        height, width = alpha.shape
        variants = {1: mirrored}
        for index, seed in [(2, 11), (3, 29)]:
            mask = blob_mask(height, width, seed + len(name))[..., None]
            variants[index] = rgb * mask + mirrored * (1.0 - mask)
        for index, colour in variants.items():
            a = np.maximum(alpha, alpha[:, ::-1])
            data = np.dstack([colour, a * 255.0]).astype(np.uint8)
            Image.fromarray(data, "RGBA").save(TERRAIN / f"{name}_{index}.png", optimize=True)
            written += 1
    print(f"  terrain: wrote {written} variants to {TERRAIN}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
