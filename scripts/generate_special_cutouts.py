#!/usr/bin/env python3
"""Paint the four still-procedural special-resource markers as keyed cutouts.

crhy/rhYciv#18 left coal (hills 1), iron (mountains 2), tusked ivory
(glacier 1) and peat/oil (swamp 2) as the flat disc that
build_standalone_sheets.build_terrain1 draws when Specials/ has no art. This
script draws all four as 300x300 RGBA cutouts with transparent corners, in the
hand-made idiom of docs/notes/rhyciv-generated-art-direction.md: irregular
outlines, jittered detail, and the thing each resource communicates reading
first -- a speckly coal lump, a banded grey ore rock, an ivory tusk pair, a
black oil slick.

Provenance (docs/ASSET-PROVENANCE.md): the three drawn markers are original
vector work by this script; the swamp-2 slick is keyed out of the keyed
project-original alt painting ~/rhYcivtextures/terrain/alts/oil_slick.png the
same way #150 keyed mountains_1 out of Other/gold.jpg. Every marker is
composed over its terrain diamond into a contact sheet for eye-checking.

Usage:
    python3 scripts/generate_special_cutouts.py [--check]
"""

from __future__ import annotations

import argparse
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

REPOSITORY = Path(__file__).resolve().parents[1]
SPECIALS = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain" / "Specials"
TERRAIN = REPOSITORY / "RaylibUI" / "FOSSart" / "Terrain"
OIL_SOURCE = Path.home() / "rhYcivtextures" / "terrain" / "alts" / "oil_slick.png"
PREVIEW_DIR = Path("/tmp/f242-previews")

SIZE = 300       # the canvas every special cutout ships on
SS = 2           # drawn supersampling factor; the 300x300 result is filtered down

# The fallback discs in build_standalone_sheets we are replacing; nothing here
# may land near these colours so the shipped markers stay distinguishable.
DISC_YELLOW = (246, 205, 47)
DISC_GREY = (235, 235, 235)

# Ground line of the 300x300 slot: the bottom of the tile's far edge, where the
# other specials bottom-anchor (mountains_1 alpha bbox ends at 299).
GROUND = 296


def _blob(cx: float, cy: float, rx: float, ry: float, rng: random.Random,
          n: int = 11, wobble: float = 0.20) -> list[tuple[float, float]]:
    """An irregular closed outline around an ellipse: jittered angles, jittered
    radii. Nothing here comes out ruler-round."""
    angles = sorted(rng.uniform(0.0, math.tau) for _ in range(n))
    points = []
    for a in angles:
        k = rng.uniform(1.0 - wobble, 1.0 + wobble)
        points.append((cx + rx * k * math.cos(a), cy + ry * k * math.sin(a)))
    return points


def _scale(points: list[tuple[float, float]], factor: int) -> list[tuple[float, float]]:
    return [(x * factor, y * factor) for x, y in points]


def _bezier(p0, p1, p2, steps: int = 28) -> list[tuple[float, float]]:
    out = []
    for i in range(steps + 1):
        t = i / steps
        mt = 1.0 - t
        x = mt * mt * p0[0] + 2 * mt * t * p1[0] + t * t * p2[0]
        y = mt * mt * p0[1] + 2 * mt * t * p1[1] + t * t * p2[1]
        out.append((x, y))
    return out


def _tapered(center: list[tuple[float, float]], w0: float, w1: float) -> list[tuple[float, float]]:
    """A closed outline around a polyline whose width tapers w0 -> w1."""
    left, right = [], []
    for i, (x, y) in enumerate(center):
        px, py = center[max(i - 1, 0)]
        nx, ny = center[min(i + 1, len(center) - 1)]
        dx, dy = nx - px, ny - py
        length = max(math.hypot(dx, dy), 1e-6)
        nx, ny = -dy / length, dx / length
        t = i / max(len(center) - 1, 1)
        w = w0 + (w1 - w0) * t
        left.append((x + nx * w, y + ny * w))
        right.append((x - nx * w, y - ny * w))
    return left + right[::-1]


def _shadow(canvas: Image.Image, cx: float, cy: float, rx: float, ry: float) -> None:
    """A soft grounding shadow so the lump sits on the tile instead of floating."""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse(_scale([(cx - rx, cy - ry), (cx + rx, cy + ry)], SS),
                                  fill=(12, 14, 16, 70))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(3 * SS)))


def _finish(canvas: Image.Image, name: str) -> Image.Image:
    small = canvas.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    alpha = small.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        raise SystemExit(f"{name}: empty marker")
    print(f"  {name}  alpha bbox {bbox}")
    return small


def coal() -> Image.Image:
    """hills 1: a rough coal lump with speckles."""
    rng = random.Random(1918)
    canvas = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    lumps = [
        _blob(150, 238, 84, 48, rng, 12, 0.24),
        _blob(84, 262, 52, 30, rng, 10, 0.26),
        _blob(216, 264, 44, 28, rng, 10, 0.26),
    ]
    _shadow(canvas, 150, GROUND + 2, 118, 16)
    for pts in lumps:
        draw.polygon(_scale(pts, SS), fill=(26, 27, 31, 255), outline=(15, 15, 18, 255))

    # Facets: one or two broken planes per lump catching the light. Coal is
    # angular and half-gloss, so the facets are flat triangles, not shading.
    for pts in lumps:
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        mid_x, mid_y = sum(xs) / len(xs), sum(ys) / len(ys)
        for _ in range(2):
            a = rng.randrange(len(pts))
            b = rng.randrange(len(pts))
            apex = (mid_x + rng.uniform(-14, 14), mid_y + rng.uniform(-16, 8))
            tri = [pts[a], pts[b], apex]
            tone = rng.choice((41, 47, 55))
            draw.polygon(_scale(tri, SS), fill=(tone, tone + 3, tone + 8, 255))

    # Speckle: the glitter a coal seam reads as at a glance.
    for pts in lumps:
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        for _ in range(9):
            x = rng.uniform(min(xs) + 8, max(xs) - 8)
            y = rng.uniform(min(ys) + 6, max(ys) - 6)
            r = rng.uniform(1.6, 3.0)
            draw.ellipse(_scale([(x - r, y - r), (x + r, y + r)], SS),
                         fill=(222, 228, 234, rng.randint(150, 215)))
    return _finish(canvas, "hills_1 (coal)")


def iron() -> Image.Image:
    """mountains 2: a banded grey ore rock."""
    rng = random.Random(2027)
    canvas = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    rock = _blob(150, 232, 102, 56, rng, 13, 0.18)
    _shadow(canvas, 150, GROUND + 2, 122, 15)
    draw.polygon(_scale(rock, SS), fill=(88, 92, 98, 255), outline=(60, 62, 66, 255))

    # Bands: wobbly strata drawn across the whole rock, then joined to its
    # silhouette by a mask -- the bands end where the rock ends instead of
    # lying on top of a hard outline.
    mask = Image.new("L", canvas.size, 0)
    ImageDraw.Draw(mask).polygon(_scale(rock, SS), fill=255)
    bands = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    bdraw = ImageDraw.Draw(bands)
    tones = [(64, 67, 72), (118, 122, 128), (74, 77, 82), (138, 142, 148)]
    y = 168.0
    while y < GROUND - 4:
        step = rng.uniform(15, 26)
        pts = []
        x = 26.0
        while x <= 274.0:
            pts.append((x, y + rng.uniform(-3.5, 3.5)))
            x += rng.uniform(16, 30)
        tone = tones[rng.randrange(len(tones))]
        bdraw.line(pts, fill=(*tone, 255), width=rng.randint(6, 10))
        y += step
    bands = bands.filter(ImageFilter.GaussianBlur(0.8))
    canvas.paste(bands, (0, 0), ImageChops.multiply(bands.getchannel("A"), mask))

    # A few cold metallic flecks and one rust seam hint: ore reads grey first,
    # so these stay small.
    for _ in range(7):
        x = rng.uniform(60, 240)
        y = rng.uniform(180, 280)
        r = rng.uniform(1.6, 2.8)
        draw.ellipse(_scale([(x - r, y - r), (x + r, y + r)], SS),
                     fill=(196, 202, 210, rng.randint(160, 220)))
    draw.line(_scale([(96, 232), (128, 244), (158, 238), (186, 250)], SS),
              fill=(132, 84, 58, 200), width=4)
    return _finish(canvas, "mountains_2 (iron)")


def ivory() -> Image.Image:
    """glacier 1: a tusked ivory pair standing on a floe."""
    rng = random.Random(1861)
    canvas = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    # A low ice floe roots the pair on the glacier tile.
    floe = _blob(150, 282, 96, 24, rng, 12, 0.12)
    draw.polygon(_scale(floe, SS), fill=(196, 219, 231, 255), outline=(150, 182, 199, 255))
    draw.line(_scale([(70, 280), (120, 286), (180, 279), (228, 285)], SS),
              fill=(238, 246, 250, 200), width=3)
    _shadow(canvas, 150, 292, 100, 13)

    for lean in (-1, 1):
        base = (150 + 14 * lean, 284)
        ctrl = (150 + 78 * lean, 186)
        tip = (150 + 116 * lean, 96 + rng.uniform(-6, 6))
        center = _bezier(base, ctrl, tip)
        tusk = _tapered(center, 15.0, 2.5)
        draw.polygon(_scale(tusk, SS), fill=(238, 231, 208, 255), outline=(190, 178, 150, 255))
        # Schreger banding: short chevrons along the curve, drawn after the
        # outline so they join to it instead of crossing it.
        mask = Image.new("L", canvas.size, 0)
        ImageDraw.Draw(mask).polygon(_scale(tusk, SS), fill=255)
        rung = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        rdraw = ImageDraw.Draw(rung)
        for t in (0.18, 0.30, 0.44, 0.58, 0.72, 0.86):
            i = int(t * len(center))
            x, y = center[i]
            px, py = center[max(i - 1, 0)]
            nx, ny = center[min(i + 1, len(center) - 1)]
            dx, dy = nx - px, ny - py
            length = max(math.hypot(dx, dy), 1e-6)
            nx, ny = -dy / length, dx / length
            w = 15.0 + (2.5 - 15.0) * t
            rdraw.line(_scale([(x - nx * w * 0.85, y - ny * w * 0.85),
                               (x + nx * w * 0.85, y + ny * w * 0.85)], SS),
                       fill=(205, 191, 160, 210), width=3)
        canvas.paste(rung, (0, 0), mask)
    return _finish(canvas, "glacier_1 (ivory)")


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
