import os
from pathlib import Path
import numpy as np
from PIL import Image

W, H = 300, 150          # diamond footprint (2:1 isometric)
SS = 3                   # supersample factor
PAD = 300                # padded canvas size for the 300x300 variant
# Where the tiles land. The sixteen coast_*.png go on to
# RaylibUI/FOSSart/Terrain/Coast; the padded variant, contact sheet and demo are
# review aids and stay out of the shipped asset set.
OUT = os.environ.get("COASTLINE_OUT",
                     str(Path(__file__).resolve().parents[1] / "build" / "coastline_iso"))
os.makedirs(OUT, exist_ok=True)
os.makedirs(OUT + "/padded_300x300", exist_ok=True)

S = 300.0                # world-space tile size in px (drives all frequencies)
TAU = 2 * np.pi

# ---------------- periodic value noise sampled at arbitrary (u,v) ------------
rng = np.random.default_rng(20240517)

def grid(freq):
    return rng.random((freq, freq))

def nsample(g, u, v):
    f = g.shape[0]
    cu, cv = u * f, v * f
    iu = np.floor(cu).astype(np.int64); fu = cu - iu
    iv = np.floor(cv).astype(np.int64); fv = cv - iv
    iu %= f; iv %= f
    ju, jv = (iu + 1) % f, (iv + 1) % f
    su = fu * fu * fu * (fu * (fu * 6 - 15) + 10)
    sv = fv * fv * fv * (fv * (fv * 6 - 15) + 10)
    top = g[iv, iu] * (1 - su) + g[iv, ju] * su
    bot = g[jv, iu] * (1 - su) + g[jv, ju] * su
    return top * (1 - sv) + bot * sv

class FBM:
    def __init__(self, base, octaves, gain=0.5, lac=2):
        self.layers = []
        amp, f, norm = 1.0, base, 0.0
        for _ in range(octaves):
            self.layers.append((grid(f), amp)); norm += amp; amp *= gain; f *= lac
        self.norm = norm
    def __call__(self, u, v):
        tot = 0.0
        for g, amp in self.layers:
            tot = tot + amp * nsample(g, u, v)
        return tot / self.norm

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)

F_coast  = FBM(3, 6)
F_coast2 = FBM(11, 4)
F_grain  = FBM(100, 3)
F_grain2 = FBM(200, 2)
F_blotch = FBM(9, 3)
F_ripple = FBM(26, 2)
F_foam   = FBM(34, 4)
F_foam2  = FBM(70, 3)
F_bed    = FBM(5, 4)
F_caus   = FBM(17, 3)
F_caus2  = FBM(33, 2)
F_glint  = FBM(120, 2)
F_shelf  = FBM(4, 3)

# The open sea is this game's own ocean painting rather than anything generated
# here. Everything below paints a shoreline well and open water badly: every
# detail term is tied to distance from the waterline, so a tile with no land in
# it keeps none of them and comes out as a flat field. The painting is a diamond
# on the same 2:1 footprint as these tiles, so it lands square on them, and it is
# blended in only where the water is deep enough for the shoreline ramp to have
# finished -- the coast keeps the calm it was tuned for.
OCEAN_PAINTING = (Path(__file__).resolve().parents[1]
                  / "RaylibUI" / "FOSSart" / "Terrain" / "ocean.png")
F_swellw = FBM(7, 3)
F_spray  = FBM(150, 2)
F_tone   = FBM(13, 3)
F_blot2  = FBM(21, 2)
G_speck  = grid(300)
G_shell  = grid(150)

# ---------------- colour ramp -------------------------------------------------
# Signed distance to the waterline, in world pixels, to colour. Landward of the
# shoreline the tile used to be sand all the way out, which is why an ocean tile
# touching land showed a broad flat cream band: half the diamond sits on the land
# side of a marching-squares shoreline, and all of it was beach. A real shore is a
# narrow strip that gives way to whatever grows behind it, so the land end now
# runs sand, then dune growth, then the grassland tile's own colour, and the
# coast tile carries on into its neighbour instead of stopping at a hard edge.
# Measured against a satellite photograph of a fjord coast rather than against
# what looks dramatic on its own. In that photograph the sea is nearly black
# right up to the rock, the lightening near the shore is slight and short, and
# the beach is a thread rather than a band. The ramp used to run through
# saturated turquoise from 48 pixels out and put a wide cream beach behind it,
# which drew a glowing outline around every island -- the single loudest thing
# on the map, and nothing like a coast.
#
# That reading was taken too far. A fjord is nearly black because it is in
# shadow under rock; open sea in daylight is not, and every water tile on the map
# had been painted to the fjord's darkest note. Measured against this game's own
# ocean painting, `FOSSart/Terrain/ocean.png`, whose median is (6, 62, 123) and
# whose upper quartile reaches (30, 104, 176), the deep end here was both far too
# dark and, worse, almost perfectly flat: its colour varied by about 3 levels
# where the painting varies by 40. So deep water now carries the painting's tone,
# and the shelf still lightens towards the shore without the surf line glowing.
STOPS = [
    (-420, ( 12,  58, 112)), (-150, ( 13,  60, 114)), (-110, ( 14,  61, 113)),
    ( -80, ( 15,  60, 110)), ( -56, ( 16,  58, 104)), ( -38, ( 19,  61,  99)),
    ( -24, ( 22,  68, 102)), ( -14, ( 27,  78, 110)), (  -8, ( 33,  88, 118)),
    (  -3, ( 42,  98, 124)),
    (  -1, (116, 112,  90)), (   3, (146, 136, 108)), (   9, (174, 162, 128)),
    (  16, (166, 160, 116)), (  26, (136, 142,  80)), (  42, (106, 122,  48)),
    (  66, ( 88, 104,  28)), ( 150, ( 81,  99,  19)), ( 420, ( 81,  99,  19)),
]
_ds = np.array([s[0] for s in STOPS], float)
_cs = np.array([s[1] for s in STOPS], float)

def ramp(d):
    out = np.empty(d.shape + (3,))
    for k in range(3):
        out[..., k] = np.interp(d, _ds, _cs[:, k])
    return out

# ---------------- screen -> world (inverse isometric projection) -------------
# World square corners map to diamond vertices:
#   (u,v)=(0,0) -> N (top)    (1,0) -> E (right)
#   (1,1)       -> S (bottom) (0,1) -> W (left)
sw, sh = W * SS, H * SS
px = (np.arange(sw) + 0.5) / SS
py = (np.arange(sh) + 0.5) / SS
SX = np.tile(px, (sh, 1))
SY = np.tile(py[:, None], (1, sw))
a = SY / (H / 2)
b = (SX - W / 2) / (W / 2)
U_raw = 0.5 * (a + b)
V_raw = 0.5 * (a - b)

EPS = 0.005                      # hair of overlap so neighbours don't hairline
inside = ((U_raw > -EPS) & (U_raw < 1 + EPS) &
          (V_raw > -EPS) & (V_raw < 1 + EPS))
U = np.clip(U_raw, 0, 1)         # clamped for colour: extends art past the edge
V = np.clip(V_raw, 0, 1)         # so downsampling can't pull in a halo

# How far the shoreline wanders off the straight line between two corners. A
# real coast is lobed and fractal; at 0.115 this one read as a chain of straight
# facets, which is the "needs to be rounded a bit more, more rustic" of it. The
# noise is periodic, so however far it wanders two tiles still agree on the edge
# they share.
AMP = 0.175

# How the painted coast is pulled in towards the land. The sea reaches full depth
# at 150 / SHELF_REACH world pixels, so a smaller number is a narrower shelf.
# BEACH_TRIM is how far the shoreline is pushed back towards the land in world
# pixels. LAND_REACH scales what is left; at 1.0 the land side keeps the spacing
# the ramp's sand and grass stops were written for.
SHELF_REACH = 0.32
BEACH_TRIM = 16.0
LAND_REACH = 1.0

# Every one of these sixteen diamonds is drawn on an *ocean* tile: MapImage only
# reaches for the marching set when the tile's own type is Ocean. So however many
# of its four vertices are land, the tile is still water, and must still read as
# water. Left to the bilinear field alone it does not: with three or four land
# vertices the shoreline never crosses the diamond and the tile comes out as
# solid grass. A bay one tile wide was drawn as a meadow, and the whales living
# in it appeared to be breaching out of a field.
#
# This keeps a pool of open water at the tile centre by pushing the field down
# there, so the enclosed masks read as a cove or a tarn ringed by shore rather
# than as land. It is a smooth bump centred on the tile, so the corners are
# untouched and the open-sea and single-shoreline tiles are unchanged.
CENTRE_WATER = 0.75
CENTRE_SPREAD = 0.42

# How lopsided the held-open water is. Without this the enclosed masks came out
# as a perfect circle -- a bright ring with a dark middle, sitting in a field --
# which read as a cartoon pond rather than as a tarn. The wobble is taken from a
# periodic noise field, so two tiles still agree along the edge they share.
CENTRE_IRREGULARITY = 0.55

# Most surf the shoreline may ever be covered by.
FOAM_CEILING = 0.34

def _load_painting():
    """The ocean painting, resampled onto the supersampled canvas."""
    if not OCEAN_PAINTING.exists():
        print(f"note: {OCEAN_PAINTING.name} not found; open water stays procedural")
        return None
    art = Image.open(OCEAN_PAINTING).convert("RGB").resize((sw, sh), Image.LANCZOS)
    return np.asarray(art, dtype=float)


_painting = _load_painting()


def build(N, E, Sc, Wc):
    """Corners in world order TL,TR,BR,BL == screen N,E,S,W."""
    TL, TR, BR, BL = N, E, Sc, Wc
    f = ((1 - U) * (1 - V) * TL + U * (1 - V) * TR +
         U * V * BR + (1 - U) * V * BL)
    dfu = (1 - V) * (TR - TL) + V * (BR - BL)
    dfv = (1 - U) * (BL - TL) + U * (BR - TR)

    # Hold open water at the tile centre (see CENTRE_WATER). The gradient is
    # corrected too, because gmag is what converts the field into a distance in
    # world pixels, and every band painted below -- shelf, surf, wet sand -- is
    # placed by that distance.
    lopsided = 1.0 + CENTRE_IRREGULARITY * (F_blotch(U, V) - 0.5) * 2.0
    bump = CENTRE_WATER * lopsided * np.exp(-(((U - 0.5) ** 2 + (V - 0.5) ** 2)
                                              / CENTRE_SPREAD ** 2))
    f = f - bump
    dfu = dfu + bump * (2 * (U - 0.5) / CENTRE_SPREAD ** 2)
    dfv = dfv + bump * (2 * (V - 0.5) / CENTRE_SPREAD ** 2)

    gmag = np.maximum(np.sqrt(dfu ** 2 + dfv ** 2) / S, 0.42 / S)

    warp = AMP * (0.84 * (F_coast(U, V) - 0.5) +
                  0.16 * (F_coast2(U, V) - 0.5)) * 2.0
    d = np.clip((f - 0.5 + warp) / gmag, -150.0, 150.0)

    # Pull the whole coast in before anything is painted. Untouched, the sea did not
    # reach full depth until 150 world pixels out - half a tile - so an ocean tile
    # touching land was bright edge to edge, and everything landward of the
    # shoreline was sand, putting the beach half a tile out to sea.
    #
    # This moves the distance itself rather than only the colour ramp, so the wet
    # sand, the foam lace, the surf lip and the swell all follow the waterline to
    # its new place. Moving the ramp alone left those where they were and stranded
    # the wet-sand tint out on dry beach as a teal smear.
    d = np.where(d < 0, d / SHELF_REACH, (d - BEACH_TRIM) / LAND_REACH)

    n_shelf = F_shelf(U, V)
    shelf = ((n_shelf - 0.5) * 55 * smoothstep(-4, -30, d)
             * smoothstep(-148, -100, d))
    d_col = d + shelf
    img = ramp(d_col)

    n_grain, n_grain2 = F_grain(U, V), F_grain2(U, V)
    n_speck = nsample(G_speck, U, V)
    n_shell = nsample(G_shell, U, V)
    n_blotch, n_ripple = F_blotch(U, V), F_ripple(U, V)
    n_foam, n_foam2 = F_foam(U, V), F_foam2(U, V)
    n_bed, n_spray = F_bed(U, V), F_spray(U, V)

    # ---- sand ---------------------------------------------------------------
    land = smoothstep(-2, 6, d)
    grain = (n_grain - 0.5) * 20 + (n_grain2 - 0.5) * 14 + (n_speck - 0.5) * 10
    img += (land * grain)[..., None]
    img += land[..., None] * ((n_blotch - 0.5) * 14)[..., None] * np.array([1.0, .93, .80])
    img += land[..., None] * ((F_blot2(U, V) - 0.5) * 13)[..., None] * np.array([1.0, .95, .85])
    img += (smoothstep(0.93, 1.0, n_shell) * smoothstep(10, 40, d) * 26)[..., None]
    dune = (smoothstep(22, 95, d)
            * np.sin(TAU * (17 * U + 7 * V) + (n_ripple - 0.5) * 7.0) * 5.5)
    img += dune[..., None]
    wrack = (smoothstep(20, 27, d) * (1 - smoothstep(31, 46, d))
             * smoothstep(0.45, 0.75, n_foam))
    img -= (wrack * 18)[..., None] * np.array([1.0, 1.05, 1.15])
    wet = (1 - smoothstep(4, 20, d)) * smoothstep(-2, 2, d)
    img -= (wet * 26)[..., None] * np.array([1.0, 1.02, 0.86])
    img += (wet * 6)[..., None] * np.array([0.35, 0.75, 1.0])
    img -= ((1 - smoothstep(16, 34, d)) * smoothstep(2, 12, d) * 9)[..., None]

    # ---- water body ---------------------------------------------------------
    sea = 1 - smoothstep(-4, 2, d)
    patch = smoothstep(0.56, 0.86, n_bed) * sea * smoothstep(-120, -30, d_col)
    img[..., 0] -= patch * 30; img[..., 1] -= patch * 8; img[..., 2] -= patch * 14
    img += ((F_tone(U, V) - 0.5) * 34 * sea)[..., None] * np.array([0.5, 0.9, 1.0])
    img += ((n_bed - 0.5) * 20 * sea)[..., None] * np.array([0.5, 0.9, 1.0])
    # The ripple used to be gated to within seventy pixels of the shore, along
    # with the crest and the caustics below, so an open-water tile had none of
    # them and came out as a flat field. Deep water keeps a floor of it.
    sr = (np.sin(TAU * (7 * U + 2 * V) + (n_ripple - 0.5) * 9.0)
          * (0.45 + 0.55 * smoothstep(-70, -16, d_col)) * sea * 6)
    img += sr[..., None]

    sww = (F_swellw(U, V) - 0.5) * 5.0
    swell = np.sin(TAU * (3 * U + 5 * V) + sww)
    img += (swell * 6.5 * sea)[..., None] * np.array([0.7, 1.0, 1.0])
    swell2 = np.sin(TAU * (-5 * U + 3 * V) - sww)
    img += (swell2 * 5.0 * sea)[..., None] * np.array([0.7, 1.0, 1.0])
    crest = np.clip(swell, 0, 1) ** 3 * (0.5 + 0.5 * smoothstep(-150, -60, d_col)) * sea
    img += (crest * 11)[..., None]

    cw = (F_caus(U, V) * 2 - 1) * 2.4 + (F_caus2(U, V) * 2 - 1) * 1.1
    c = np.clip(np.sin(TAU * 11 * U + cw) * np.sin(TAU * 10 * V - cw), 0, 1) ** 2
    cmask = smoothstep(-85, -22, d_col) * (1 - smoothstep(-15, -7, d_col)) * sea
    img += (c * cmask * 20)[..., None] * np.array([0.8, 1.0, .98])

    sw_ = (smoothstep(0.48, 0.82, n_foam)
           * (1 - smoothstep(-58, -44, d)) * smoothstep(-76, -62, d))
    img += (sw_ * 9)[..., None]

    # ---- the painted sea ----------------------------------------------------
    # Deep water takes its colour and its swell from the painting. The weight
    # goes to nothing by thirty pixels from the waterline, so the shelf, the
    # surf and the beach below are untouched and the coast still reads as the
    # quiet one it was tuned to be.
    if _painting is not None:
        deep = (1 - smoothstep(-130, -30, d_col)) * sea
        w = (0.88 * deep)[..., None]
        img = img * (1 - w) + _painting * w

    # ---- surf ---------------------------------------------------------------
    # Surf is a broken thread along the waterline, and it is off-white rather than
    # white. It used to be a wide band at nearly full strength, which is what put a
    # lit outline round every coast; capped here so it can accent the shore without
    # becoming the shore.
    band = smoothstep(-7, -3.5, d) * (1 - smoothstep(-0.5, 2.5, d))
    lace = smoothstep(0.46, 0.80, n_foam * 0.55 + n_foam2 * 0.45)
    foam = band * (0.10 + 0.45 * lace)
    lip = smoothstep(-2.6, -1.2, d) * (1 - smoothstep(-0.6, 1.2, d))
    foam = np.clip(foam + lip * 0.30, 0, FOAM_CEILING)
    foam = np.clip(foam + smoothstep(0.94, 0.995, n_spray)
                   * smoothstep(-10, -5, d) * (1 - smoothstep(2, 6, d)) * 0.18,
                   0, FOAM_CEILING)
    img = img * (1 - foam[..., None]) + np.array([232.0, 240.0, 240.0]) * foam[..., None]

    gl = smoothstep(0.80, 0.97, F_glint(U, V)) * smoothstep(-150, -60, d_col) * sea
    img += (gl * 22)[..., None]

    rgba = np.zeros((sh, sw, 4), np.uint8)
    rgba[..., :3] = np.clip(img, 0, 255).astype(np.uint8)
    rgba[..., 3] = np.where(inside, 255, 0)
    return Image.fromarray(rgba, "RGBA").resize((W, H), Image.LANCZOS)

# ---------------- 16 tiles ----------------------------------------------------
NAMES = {
    0: "ocean",          1: "corner_land_W",   2: "corner_land_S",
    3: "edge_land_SW",   4: "corner_land_E",   5: "diagonal_E_W",
    6: "edge_land_SE",   7: "cove_N",          8: "corner_land_N",
    9: "edge_land_NW",  10: "diagonal_N_S",   11: "cove_E",
    12: "edge_land_NE", 13: "cove_S",         14: "cove_W",
    15: "enclosed",
}
tiles = {}
for mask in range(16):
    n, e, s, w = (mask >> 3) & 1, (mask >> 2) & 1, (mask >> 1) & 1, mask & 1
    im = build(n, e, s, w)
    tiles[mask] = im
    im.save(f"{OUT}/coast_{mask:02d}_{NAMES[mask]}.png")
    pad = Image.new("RGBA", (PAD, PAD), (0, 0, 0, 0))
    pad.paste(im, ((PAD - W) // 2, PAD - H))      # bottom-anchored
    pad.save(f"{OUT}/padded_300x300/coast_{mask:02d}_{NAMES[mask]}.png")

# ---------------- contact sheet ----------------------------------------------
CW, CH = 200, 100
sheet = Image.new("RGBA", (CW * 4 + 40, CH * 4 + 40), (30, 30, 34, 255))
for mask in range(16):
    r, c = divmod(mask, 4)
    sheet.alpha_composite(tiles[mask].resize((CW, CH), Image.LANCZOS),
                          (8 + c * (CW + 8), 8 + r * (CH + 8)))
sheet.convert("RGB").save(f"{OUT}/_contact_sheet.png")

# ---------------- assembled isometric demo -----------------------------------
# This mirrors how MapImage actually uses the set, which is not how a textbook
# marching-squares field is drawn. The map is a grid of *typed tiles*, and the
# coast diamonds are reached for only when a tile's own type is Ocean; a land
# tile draws its terrain texture and never appears here. Each water tile's mask
# comes from its four vertices, and a vertex counts as land when any of the three
# tiles meeting it there is land -- exactly the rule in MapImage.MakeTileGraphic.
GW, GH = 11, 11
cy, cx = np.mgrid[0:GH, 0:GW]
ncx, ncy = cx / (GW - 1) - .5, cy / (GH - 1) - .5
r = np.sqrt((ncx * 1.3) ** 2 + (ncy * 1.3) ** 2)
lobe = 0.30 + 0.08 * np.sin(np.arctan2(ncy, ncx) * 3.0 + 1.1)
land_tile = (r < lobe)
land_tile[2, 8] = True
land_tile[8, 2] = True
# A one-tile bay inside the island: the case that used to be drawn as a meadow.
land_tile[5, 5] = False

def is_land(ty, tx):
    return bool(land_tile[ty, tx]) if 0 <= ty < GH and 0 <= tx < GW else False

TW, TH = 120, 60
ox, oy = GH * TW // 2, 20
demo = Image.new("RGBA", (TW * (GW + GH) // 2 + 40, TH * (GW + GH) // 2 + 80),
                 (16, 22, 40, 255))
small = {m: tiles[m].resize((TW, TH), Image.LANCZOS) for m in tiles}
grass = Image.new("RGBA", (TW, TH), (0, 0, 0, 0))
_gy, _gx = np.mgrid[0:TH, 0:TW]
_diamond = (np.abs(_gx + .5 - TW / 2) / (TW / 2)
            + np.abs(_gy + .5 - TH / 2) / (TH / 2)) <= 1
_g = np.zeros((TH, TW, 4), np.uint8)
_g[..., :3] = np.array([104, 124, 62], np.uint8)
_g[..., 3] = np.where(_diamond, 255, 0)
grass = Image.fromarray(_g, "RGBA")

for ty in range(GH):
    for tx in range(GW):
        X = ox + (tx - ty) * TW // 2
        Y = oy + (tx + ty) * TH // 2
        if is_land(ty, tx):
            demo.alpha_composite(grass, (X, Y))
            continue
        # Screen N is (ty-1, tx-1)'s side of the grid; the three tiles meeting
        # each vertex are the two edge neighbours either side of it and the
        # diagonal one between them.
        n = is_land(ty - 1, tx) or is_land(ty - 1, tx - 1) or is_land(ty, tx - 1)
        e = is_land(ty - 1, tx) or is_land(ty - 1, tx + 1) or is_land(ty, tx + 1)
        s = is_land(ty, tx + 1) or is_land(ty + 1, tx + 1) or is_land(ty + 1, tx)
        w = is_land(ty + 1, tx) or is_land(ty + 1, tx - 1) or is_land(ty, tx - 1)
        m = (n << 3) | (e << 2) | (s << 1) | w
        demo.alpha_composite(small[m], (X, Y))
demo.convert("RGB").save(f"{OUT}/_demo_island.png")
print("done")
