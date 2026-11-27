#!/usr/bin/env python3
"""Draw the settler unit as a small family walking out to found a city.

rhYciv#200: the shipped settlers sprite was the warrior's silhouette with the
sword painted off, so on the map a settler and a warrior read as the same blob.
This generator composes a genuinely different sprite: two adults and two small
children on the move, one adult shouldering a bedroll, a packed hand-cart at the
end of the line, and no weapons or shields anywhere.

It is deterministic (seeded) and hand-made per docs/notes/rhyciv-generated-art-direction.md:
every contour gets a fixed wobble, the figures are spaced with a jitter instead
of a ruler, and the family is drawn pass by pass (silhouettes, then their
details) so the outlines join rather than stack. The palette is the warm brown
and ochre language of the rest of the unit art.

Usage:
    python3 scripts/generate_settler_family_art.py [out.png]
"""

from __future__ import annotations

import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

CANVAS = 300
REPO = Path(__file__).resolve().parents[1]
DEFAULT_OUT = REPO / "RaylibUI" / "FOSSart" / "Units" / "settlers.png"

# Warm earth palette shared with the other unit art (measured off warriors.png,
# engineers.png and caravan.png): dark brown contour, ochre bodies, tan cloth.
OUTLINE = (46, 28, 14)
BODY = (150, 96, 52)
BODY_HI = (196, 140, 84)
BODY_LO = (96, 58, 30)
CLOTH = (124, 78, 40)
CLOTH_HI = (176, 120, 66)
SKIN = (188, 132, 86)
BUNDLE = (224, 206, 176)
BUNDLE_LO = (188, 164, 128)
WOOD = (120, 80, 44)
WOOD_LO = (78, 50, 26)
WHEEL = (70, 48, 28)


def wobble_poly(points, rng, amount=2.2):
    """Add a deterministic bend to a polygon so no edge sits ruler-straight."""
    out = []
    for x, y in points:
        out.append((x + rng.uniform(-amount, amount), y + rng.uniform(-amount, amount)))
    return out


def head(draw, cx, cy, r, rng):
    n = 14
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n
        rr = r + rng.uniform(-1.4, 1.4)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    draw.polygon(pts, fill=SKIN)
    draw.polygon(pts, outline=OUTLINE)


def body(draw, cx, top, height, half_top, half_bottom, color, rng):
    """A slightly tapered torso/robe, hand-drawn on both sides."""
    left = [(cx - half_top + rng.uniform(-1.5, 1.5), top)]
    right = [(cx + half_top + rng.uniform(-1.5, 1.5), top)]
    steps = 3
    for i in range(1, steps + 1):
        t = i / steps
        y = top + height * t
        w = half_top + (half_bottom - half_top) * t
        left.append((cx - w + rng.uniform(-1.8, 1.8), y + rng.uniform(-1, 1)))
        right.append((cx + w + rng.uniform(-1.8, 1.8), y + rng.uniform(-1, 1)))
    poly = left + list(reversed(right))
    draw.polygon(poly, fill=color, outline=OUTLINE)
    # a lighter fold down one side so the flat body reads as cloth, not a slab
    draw.line((int(cx - half_top * 0.2), int(top + 3), int(cx - half_bottom * 0.4), int(top + height - 3)),
              fill=BODY_HI, width=max(1, int(half_top // 3)))


def adult(draw, cx, ground, scale, rng, tilt=0.0):
    """A standing adult: legs, robe, head, one arm. Returns nothing."""
    leg_h = int(46 * scale)
    torso_h = int(70 * scale)
    head_r = int(15 * scale)
    hip = ground - leg_h
    # two short legs
    draw.line((int(cx - 7 * scale), ground, int(cx - 6 * scale), hip), fill=BODY_LO, width=max(2, int(7 * scale)))
    draw.line((int(cx + 7 * scale), ground, int(cx + 6 * scale), hip), fill=BODY_LO, width=max(2, int(7 * scale)))
    color = CLOTH if tilt < 0 else CLOTH_HI
    body(draw, cx, hip - torso_h, torso_h, 15 * scale, 21 * scale, color, rng)
    top = hip - torso_h
    # a neck so the head joins the torso rather than floating above it (join, not stack)
    draw.rectangle((int(cx - 6 * scale), int(top - 6), int(cx + 6 * scale), int(top + 6)), fill=SKIN)
    head(draw, cx, top - head_r + 5, head_r, rng)
    # a single arm at the side (no weapon, no shield)
    draw.line((int(cx - 16 * scale), int(top + 6 * scale), int(cx - 20 * scale), int(hip - 4 * scale)),
              fill=color, width=max(2, int(6 * scale)))


def child(draw, cx, ground, scale, rng):
    """A small child - shorter, rounder head, one piece body."""
    body_h = int(40 * scale)
    head_r = int(15 * scale)
    hip = ground
    body(draw, cx, hip - body_h, body_h, 11 * scale, 14 * scale, BODY, rng)
    top = hip - body_h
    draw.rectangle((int(cx - 4 * scale), int(top - 4), int(cx + 4 * scale), int(top + 4)), fill=SKIN)
    head(draw, cx, top - head_r + 4, head_r, rng)


def bedroll_over_shoulder(draw, cx, cy, rng):
    """A rolled bedroll slung across the back - the settler's load, not a weapon."""
    draw.ellipse((cx - 12, cy - 8, cx + 12, cy + 8), fill=BUNDLE, outline=OUTLINE)
    for off in (-6, 0, 6):
        draw.line((cx - 10, cy + off, cx + 10, cy + off), fill=BUNDLE_LO, width=2)


def cart(draw, cx, ground, rng):
    """A packed two-wheel hand-cart at the tail of the family.

    Drawn as a low plank bed with a bundled load on top and one wheel below -
    not a solid box, which would read as a wall in silhouette.
    """
    bed_y = ground - 34
    x0, x1 = cx - 30, cx + 24
    # the plank bed: a short bar, hand-jittered
    draw.polygon(wobble_poly([(x0, bed_y), (x1, bed_y), (x1, bed_y + 8), (x0, bed_y + 8)], rng, 1.2),
                 fill=WOOD, outline=OUTLINE)
    # bundled bedroll + sack riding on the bed
    draw.ellipse((x0 + 3, bed_y - 16, cx + 2, bed_y + 1), fill=BUNDLE, outline=OUTLINE)
    draw.line((x0 + 6, bed_y - 8, cx, bed_y - 8), fill=BUNDLE_LO, width=2)
    draw.ellipse((cx + 1, bed_y - 12, x1 - 3, bed_y), fill=BUNDLE_LO, outline=OUTLINE)
    # one visible spoked wheel under the bed
    wy = ground - 12
    cxw = cx - 6
    for rr in (16, 7):
        draw.ellipse((cxw - rr, wy - rr, cxw + rr, wy + rr), fill=WHEEL, outline=OUTLINE)
    draw.line((cxw, wy - 7, cxw, wy + 7), fill=WOOD_LO, width=2)
    draw.line((cxw - 7, wy, cxw + 7, wy), fill=WOOD_LO, width=2)
    # the tongue reaching forward to the walking family
    draw.line((int(x0), int(bed_y + 4), int(x0 - 22), int(ground - 20)), fill=WOOD_LO, width=3)


def compose():
    rng = random.Random(0x2f44)
    img = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    ground = 248

    # --- pass 1: silhouettes, spaced with a jitter, walking left to right ---
    # a small family on the move: parent, toddler, parent, child, packed cart.
    # adult A (leading, shoulders a bedroll)
    x0 = 70 + rng.randint(-3, 3)
    adult(draw, x0, ground, 1.0, rng, tilt=-1)
    bedroll_over_shoulder(draw, x0 - 16, ground - 108, rng)
    # child 1 (toddler) trotting beside adult A
    child(draw, x0 + 30 + rng.randint(-2, 2), ground - 1, 0.55, rng)
    # adult B (the second parent, slightly shorter, closer to centre)
    x1 = 140 + rng.randint(-3, 3)
    adult(draw, x1, ground, 0.94, rng, tilt=1)
    # child 2 a bit behind adult B, clear of the cart
    child(draw, x1 + 36 + rng.randint(-2, 2), ground - 1, 0.5, rng)
    # --- packed hand-cart at the tail (separate cluster) ---
    cart(draw, 236 + rng.randint(-3, 3), ground, rng)

    return img


def apply_hand_edges(img):
    """Deterministic hand-drawn irregularity: displace alpha edges with smooth noise."""
    a = np.array(img)
    alpha = a[:, :, 3].astype(np.float64)
    # smooth low-frequency noise field
    nr = np.random.RandomState(0xb0b)
    field = nr.rand(24, 24) * 2 - 1
    fimg = Image.fromarray(((field + 1) * 127).astype(np.uint8)).resize((CANVAS, CANVAS), Image.BICUBIC)
    f = (np.array(fimg).astype(np.float64) - 127) / 127.0
    # only along alpha edges
    edge = (alpha > 20) & (alpha < 235)
    shift = (f * 1.6)
    a[:, :, 3] = np.clip(alpha + shift * (alpha > 0) * 90, 0, 255).astype(np.uint8)
    # soften the very hardest contour a touch, like a brush not a stencil
    out = Image.fromarray(a)
    small = out.filter(ImageFilter.SMOOTH)
    a2 = np.array(out).astype(np.float64)
    a3 = np.array(small).astype(np.float64)
    blend = np.where((alpha >= 20) & (alpha <= 235), 0.5, 0.0)[..., None]
    merged = a2 * (1 - blend) + a3 * blend
    return Image.fromarray(np.clip(merged, 0, 255).astype(np.uint8))


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_OUT
    img = compose()
    img = apply_hand_edges(img)
    # keep it keyed on a fully transparent background: alpha at (0,0) is 0
    a = np.array(img)
    a[0, 0, 3] = 0
    img = Image.fromarray(a)
    img.save(out, optimize=True)
    print(f"wrote {out} ({img.size[0]}x{img.size[1]})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
