#!/usr/bin/env python3
"""Cut the mountain-gold special out of the orphaned Other/gold.jpg painting.

The map's Gold resource sits in the first special slot of the mountains row.
Without art there, `build_standalone_sheets.build_terrain1` draws a flat yellow
disc -- the "stupid" gold icon of issue #150. `FOSSart/Other/gold.jpg` is a
project-original photograph of a nugget on a rock face that nothing has ever
loaded; this keys the nugget out of it and writes the 300x300 cutout every
other special uses.

Usage:
    python3 scripts/import_special_from_other.py [--check]
"""

from __future__ import annotations

import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

REPOSITORY = Path(__file__).resolve().parents[1]
SOURCE = REPOSITORY / "RaylibUI" / "FOSSart" / "Other" / "gold.jpg"
OUT = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Specials" / "mountains_1.png"

# Centred on the nugget in the 1024x1024 painting.
ELLIPSE = (555, 610, 230, 190)
# Longer side of the cutout inside the 300x300 canvas, matching the other
# specials (they are painted into roughly that footprint before bottom-align).
TARGET = 260
SIZE = 300


def gold_mask(rgb: np.ndarray) -> np.ndarray:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = np.maximum(r, np.maximum(g, b))
    mn = np.minimum(r, np.minimum(g, b))
    sat = (mx - mn) / np.maximum(mx, 1e-6)
    return (
        (r > 105) & (g > 70) & (b < 135)
        & (r > b + 45) & (g > b + 20)
        & (sat > 0.32) & (r >= g * 0.88) & (g >= b + 15)
    )


def cut() -> Image.Image:
    with Image.open(SOURCE) as loaded:
        photo = loaded.convert("RGB")
    rgb = np.asarray(photo).astype(np.float32)
    gold = gold_mask(rgb)

    height, width = gold.shape
    yy, xx = np.mgrid[0:height, 0:width]
    cx, cy, rx, ry = ELLIPSE
    inside = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= 1.0
    seed = gold & inside
    if not seed.any():
        raise SystemExit(f"{SOURCE.name}: no gold pixels in the nugget ellipse")

    # Densest 21x21 window of the seed is the flood start.
    k = 21
    integral = np.zeros((height + 1, width + 1), dtype=np.int32)
    integral[1:, 1:] = np.cumsum(np.cumsum(seed.astype(np.int32), axis=0), axis=1)
    windows = (
        integral[k:, k:] - integral[:-k, k:] - integral[k:, :-k] + integral[:-k, :-k]
    )
    wy, wx = np.unravel_index(int(np.argmax(windows)), windows.shape)
    start = (int(wy + k // 2), int(wx + k // 2))

    component = np.zeros_like(gold, dtype=bool)
    if not gold[start]:
        raise SystemExit(f"{SOURCE.name}: densest seed window is not gold")
    queue: deque[tuple[int, int]] = deque([start])
    component[start] = True
    while queue:
        y, x = queue.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < height and 0 <= nx < width and gold[ny, nx] and not component[ny, nx]:
                component[ny, nx] = True
                queue.append((ny, nx))

    # Pull in seed pixels that touch the component but were not 4-connected yet
    # (thin bright cracks the flood step skipped).
    for _ in range(8):
        dilated = np.asarray(
            Image.fromarray((component * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(3))
        ) > 0
        added = seed & dilated & ~component
        if not added.any():
            break
        component |= added

    if not component.any():
        raise SystemExit(f"{SOURCE.name}: empty nugget component")

    mask = Image.fromarray((component * 255).astype(np.uint8), "L")
    mask = mask.filter(ImageFilter.MaxFilter(9))
    mask = mask.filter(ImageFilter.MinFilter(7))
    mask = mask.filter(ImageFilter.MaxFilter(5))
    mask = mask.filter(ImageFilter.MinFilter(5))
    mask = mask.filter(ImageFilter.GaussianBlur(3.5))
    alpha = np.asarray(mask).astype(np.float32) / 255.0
    alpha = np.clip((alpha - 0.18) / 0.45, 0.0, 1.0)

    # Kill thin tendrils: a pixel only survives if its neighbourhood agrees.
    support = np.asarray(
        Image.fromarray((alpha * 255).astype(np.uint8), "L").filter(ImageFilter.BoxBlur(5))
    ).astype(np.float32) / 255.0
    alpha = alpha * np.clip((support - 0.2) / 0.25, 0.0, 1.0)

    if not (alpha > 0.2).any():
        raise SystemExit(f"{SOURCE.name}: alpha collapsed after cleanup")

    nugget = Image.fromarray(
        np.dstack([rgb, alpha * 255]).astype(np.uint8), "RGBA"
    ).crop(Image.fromarray((alpha * 255).astype(np.uint8), "L").getbbox())

    scale = TARGET / max(nugget.size)
    nugget = nugget.resize(
        (max(1, round(nugget.width * scale)), max(1, round(nugget.height * scale))),
        Image.Resampling.LANCZOS,
    )
    hard = nugget.getchannel("A").point(lambda v: 0 if v < 48 else v)
    nugget.putalpha(hard)

    pixels = np.asarray(nugget).astype(np.float32)
    lit = np.clip((pixels[..., :3] * 1.1 - 128) * 1.12 + 128, 0, 255)
    nugget = Image.fromarray(
        np.dstack([lit, pixels[..., 3]]).astype(np.uint8), "RGBA"
    )

    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(nugget, ((SIZE - nugget.width) // 2, SIZE - nugget.height))
    return canvas


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="fail if the shipped cutout is stale")
    args = parser.parse_args()

    if not SOURCE.exists():
        print(f"missing source painting: {SOURCE}", file=sys.stderr)
        return 1

    generated = cut()
    if args.check:
        if not OUT.exists():
            print(f"missing {OUT}", file=sys.stderr)
            return 1
        with Image.open(OUT) as shipped:
            if shipped.tobytes() != generated.tobytes() or shipped.size != generated.size:
                print(f"stale {OUT}; re-run scripts/import_special_from_other.py", file=sys.stderr)
                return 1
        print(f"ok {OUT.relative_to(REPOSITORY)}")
        return 0

    OUT.parent.mkdir(parents=True, exist_ok=True)
    generated.save(OUT, "PNG", optimize=True, compress_level=9)
    covered = float((np.asarray(generated)[..., 3] > 8).mean())
    print(f"  mountains_1.png  {generated.size[0]}x{generated.size[1]}  {100 * covered:.0f}% covered")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
