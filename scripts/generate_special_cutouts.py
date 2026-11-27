#!/usr/bin/env python3
"""Key the four special-resource markers out of source paintings.

crhy/rhYciv#18 left coal (hills 1), iron (mountains 2), tusked ivory
(glacier 1) and peat/oil (swamp 2) as the flat disc that
build_standalone_sheets.build_terrain1 draws when Specials/ has no art.
All four ship as 300x300 RGBA keyed cutouts, the way #150 cut the gold
nugget out of Other/gold.jpg (scripts/import_special_from_other.py):

* coal is keyed from the project-original Other/a-coal-mine-from-above.jpg --
  the dark coal benches of the open pit are the subject;
* iron is keyed from Other/looking-down-on-an-iron-mine-in-the-side-of-a-
  snowy-mountain-peak-766986932.jpg -- the rust-brown ore scar the snow
  does not cover;
* ivory is keyed from Other/ivory.jpg -- the lit tusk mass;
* the swamp-2 slick stays keyed out of the project-original alt painting
  ~/rhYcivtextures/terrain/alts/oil_slick.png, untouched since f242.

Provenance (docs/ASSET-PROVENANCE.md): every source is a project-original
painting already shipped in RaylibUI/FOSSart/Other or rhYcivtextures.
Each marker is composed over its terrain diamond into a contact sheet in
/tmp/f242-previews for eye-checking.

Usage:
    python3 scripts/generate_special_cutouts.py [--check]
"""

from __future__ import annotations

import argparse
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

REPOSITORY = Path(__file__).resolve().parents[1]
SPECIALS = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Specials"
TERRAIN = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain"
OTHER = REPOSITORY / "RaylibUI" / "FOSSart" / "Other"
COAL_SOURCE = OTHER / "a-coal-mine-from-above.jpg"
IRON_SOURCE = OTHER / "looking-down-on-an-iron-mine-in-the-side-of-a-snowy-mountain-peak-766986932.jpg"
IVORY_SOURCE = OTHER / "ivory.jpg"
OIL_SOURCE = Path.home() / "rhYcivtextures" / "terrain" / "alts" / "oil_slick.png"
PREVIEW_DIR = Path("/tmp/f242-previews")

SIZE = 300       # the canvas every special cutout ships on
TARGET = 280     # longest side of the keyed subject, filling the frame like
                 # the other specials (mountains_1 spans ~260, the slick 282)

# Ground line of the 300x300 slot: the bottom of the tile's far edge, where
# the other specials bottom-anchor (mountains_1 alpha bbox ends at 299).
GROUND = 296


def _subject_component(mask: np.ndarray, ellipse: tuple[int, int, int, int],
                       name: str) -> np.ndarray:
    """The one mask component the seed ellipse sits on, flood-reached.

    Same method as scripts/import_special_from_other.py keyed mountains_1 out
    of Other/gold.jpg: seed the subject, flood from its densest window, then
    pull in seed pixels the 4-connected walk skipped.
    """
    height, width = mask.shape
    yy, xx = np.mgrid[0:height, 0:width]
    cx, cy, rx, ry = ellipse
    inside = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= 1.0
    seed = mask & inside
    if not seed.any():
        raise SystemExit(f"{name}: no subject pixels inside the seed ellipse")

    k = 21
    integral = np.zeros((height + 1, width + 1), dtype=np.int32)
    integral[1:, 1:] = np.cumsum(np.cumsum(seed.astype(np.int32), axis=0), axis=1)
    windows = (
        integral[k:, k:] - integral[:-k, k:] - integral[k:, :-k] + integral[:-k, :-k]
    )
    wy, wx = np.unravel_index(int(np.argmax(windows)), windows.shape)
    start = (int(wy + k // 2), int(wx + k // 2))
    if not mask[start[0], start[1]]:
        raise SystemExit(f"{name}: densest seed window is not subject")

    component = np.zeros_like(mask, dtype=bool)
    queue: deque[tuple[int, int]] = deque([start])
    component[start] = True
    while queue:
        y, x = queue.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not component[ny, nx]:
                component[ny, nx] = True
                queue.append((ny, nx))

    for _ in range(8):
        dilated = np.asarray(
            Image.fromarray((component * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(3))
        ) > 0
        added = seed & dilated & ~component
        if not added.any():
            break
        component |= added
    if not component.any():
        raise SystemExit(f"{name}: empty subject component")
    return component


def _keyed_cutout(source: Path, mask: np.ndarray, ellipse: tuple[int, int, int, int],
                  name: str) -> Image.Image:
    """One 300x300 bottom-anchored RGBA cutout out of a source painting."""
    with Image.open(source) as loaded:
        photo = loaded.convert("RGB")
    rgb = np.asarray(photo).astype(np.float32)

    component = _subject_component(mask, ellipse, name)

    # Same smoothing the gold nugget cutout went through: close gaps, then
    # round the outline off so no ruler-straight colour bands survive.
    mask_image = Image.fromarray((component * 255).astype(np.uint8), "L")
    mask_image = mask_image.filter(ImageFilter.MaxFilter(9))
    mask_image = mask_image.filter(ImageFilter.MinFilter(7))
    mask_image = mask_image.filter(ImageFilter.MaxFilter(5))
    mask_image = mask_image.filter(ImageFilter.MinFilter(5))
    mask_image = mask_image.filter(ImageFilter.GaussianBlur(3.5))
    alpha = np.asarray(mask_image).astype(np.float32) / 255.0
    alpha = np.clip((alpha - 0.18) / 0.45, 0.0, 1.0)

    support = np.asarray(
        Image.fromarray((alpha * 255).astype(np.uint8), "L").filter(ImageFilter.BoxBlur(5))
    ).astype(np.float32) / 255.0
    alpha = alpha * np.clip((support - 0.2) / 0.25, 0.0, 1.0)
    if not (alpha > 0.2).any():
        raise SystemExit(f"{name}: alpha collapsed after cleanup")

    cut = Image.fromarray(
        np.dstack([rgb, alpha * 255]).astype(np.uint8), "RGBA"
    ).crop(Image.fromarray((alpha * 255).astype(np.uint8), "L").getbbox())

    scale = TARGET / max(cut.size)
    cut = cut.resize(
        (max(1, round(cut.width * scale)), max(1, round(cut.height * scale))),
        Image.Resampling.LANCZOS,
    )
    hard = cut.getchannel("A").point(lambda v: 0 if v < 48 else v)
    cut.putalpha(hard)

    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(cut, ((SIZE - cut.width) // 2, GROUND + 4 - cut.height))
    return _finish(canvas, name)


def coal() -> Image.Image:
    """hills 1: the dark coal benches of Other/a-coal-mine-from-above.jpg."""
    with Image.open(COAL_SOURCE) as loaded:
        rgb = np.asarray(loaded.convert("RGB")).astype(int)
    mx = np.maximum(np.maximum(rgb[..., 0], rgb[..., 1]), rgb[..., 2])
    mask = mx < 70  # coal is the darkest thing in the open pit
    return _keyed_cutout(COAL_SOURCE, mask, (320, 760, 300, 220), "hills_1 (coal)")


def iron() -> Image.Image:
    """mountains 2: the rust-brown ore scar in the iron-mine painting."""
    with Image.open(IRON_SOURCE) as loaded:
        rgb = np.asarray(loaded.convert("RGB")).astype(int)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    # Red-brown exposed ore, not the white snow (high green) and not the
    # blue-grey rock and sky (blue >= red).
    mask = (r > b + 18) & (r > 45) & (r < 215) & (g < 195)
    return _keyed_cutout(IRON_SOURCE, mask, (400, 700, 260, 240), "mountains_2 (iron)")


def ivory() -> Image.Image:
    """glacier 1: the lit tusk mass in Other/ivory.jpg."""
    with Image.open(IVORY_SOURCE) as loaded:
        rgb = np.asarray(loaded.convert("RGB")).astype(int)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = np.maximum(np.maximum(r, g), b)
    mask = (mx > 150) & (r > b + 8)  # warm ivory against the grey ground
    return _keyed_cutout(IVORY_SOURCE, mask, (760, 400, 240, 220), "glacier_1 (ivory)")


def _despill(cut: Image.Image) -> Image.Image:
    """Put the magenta matte's colour back where it never should have blended.

    A frayed edge of the eroded cut keeps a matte-tinted pixel; on black oil
    there is nothing magenta to save, so every matte-coloured pixel goes, exactly
    what scripts/clean_matte_fringe.py reports as a leftover generation matte.
    """
    data = np.asarray(cut.convert("RGBA")).astype(np.float64)
    matte = matte_like(data[..., 0], data[..., 1], data[..., 2], data[..., 3])
    if matte.any():
        data[..., 3][matte] = 0.0
    return Image.fromarray(data.astype(np.uint8), "RGBA")


def matte_like(r, g, b, a):
    """clean_matte_fringe.py's matte test, so the two never disagree."""
    return (a > 0) & (g < 45) & (np.minimum(r, b) - g > 90)


def slick() -> Image.Image:
    """swamp 2: the keyed oil-slick alt, cut the way #150 cut the gold."""
    if not OIL_SOURCE.exists():
        raise SystemExit(f"missing keyed source painting: {OIL_SOURCE}")
    source = Image.open(OIL_SOURCE).convert("RGB")
    src = source.resize((source.width // 3, source.height // 3), Image.Resampling.LANCZOS)

    rgb = np.asarray(src).astype(int)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    # The alt was painted on a saturated magenta matte; ink is everything else.
    matte = (np.abs(r - 231) < 46) & (np.abs(g - 4) < 40) & (np.abs(b - 213) < 46)
    ink = ~matte
    alpha = Image.fromarray((ink * 255).astype(np.uint8), "L")
    # Erode first so every matte-blended edge pixel is gone, not recoloured.
    alpha = alpha.filter(ImageFilter.MinFilter(7)).filter(ImageFilter.MaxFilter(5))
    alpha = alpha.filter(ImageFilter.GaussianBlur(1.6))
    alpha = alpha.point(lambda v: 0 if v < 64 else v)
    bbox = alpha.getbbox()
    if bbox is None:
        raise SystemExit("oil_slick.png: no ink outside the matte")
    cut = src.convert("RGBA")
    cut.putalpha(alpha)
    cut = _despill(cut)
    cut = cut.crop(bbox)

    # Bottom-anchored at the footprint the other wide specials use
    # (glacier_2's droplet spans the slot width).
    scale = 282 / cut.width
    cut = cut.resize((282, max(1, round(cut.height * scale))), Image.Resampling.LANCZOS)
    cut = _despill(cut)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(cut, ((SIZE - cut.width) // 2, GROUND + 4 - cut.height))
    return _finish(canvas, "swamp_2 (oil slick)")


def _finish(canvas: Image.Image, name: str) -> Image.Image:
    alpha = canvas.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        raise SystemExit(f"{name}: empty marker")
    print(f"  {name}  alpha bbox {bbox}")
    return canvas


def preview(name: str, terrain: str, marker: Image.Image) -> None:
    """Each marker over its terrain diamond: the contact sheet rhY checks by eye."""
    with Image.open(TERRAIN / f"{terrain}.png") as loaded:
        tile = loaded.convert("RGBA")
    sheet = Image.new("RGBA", (600, 300), (0, 0, 0, 0))
    sheet.alpha_composite(tile.resize((600, 300), Image.Resampling.LANCZOS), (0, 0))
    sheet.alpha_composite(marker, ((600 - SIZE) // 2, 300 - SIZE))
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    sheet.save(PREVIEW_DIR / f"{name}.png")


def generate() -> dict[str, Image.Image]:
    markers = {
        "hills_1": (coal(), "hills"),
        "mountains_2": (iron(), "mountains"),
        "glacier_1": (ivory(), "glacier"),
        "swamp_2": (slick(), "swamp"),
    }
    for name, (marker, terrain) in markers.items():
        preview(name, terrain, marker)
    return {name: marker for name, (marker, _) in markers.items()}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="fail if a shipped cutout is stale")
    args = parser.parse_args()

    generated = generate()
    if args.check:
        for name, marker in generated.items():
            path = SPECIALS / f"{name}.png"
            with Image.open(path) as shipped:
                if shipped.convert("RGBA").tobytes() != marker.tobytes():
                    print(f"stale {path.relative_to(REPOSITORY)}; re-run "
                          "scripts/generate_special_cutouts.py", file=sys.stderr)
                    return 1
            print(f"ok {path.relative_to(REPOSITORY)}")
        return 0

    SPECIALS.mkdir(parents=True, exist_ok=True)
    for name, marker in generated.items():
        marker.save(SPECIALS / f"{name}.png", "PNG", optimize=True, compress_level=9)
    print(f"previews: {PREVIEW_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
