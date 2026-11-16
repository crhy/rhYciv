#!/usr/bin/env python3
"""Build clean-room compatibility sheets from rhYciv's individual FOSS art."""

from __future__ import annotations

from pathlib import Path

import resource_icons

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "RaylibUI" / "FOSSart"
OUT = ART / "Standalone"

UNIT_FILES = (
    "settlers", "engineers", "warriors", "phalanx", "archers", "legions", "pikemen", "musketeers",
    "fanatics", "partisans", "alpinetroops", "riflemen", "marines", "paratroopers", "mechanizedinfantry",
    "horsemen", "chariot", "elephant", "crusaders", "knights", "dragoons", "cavalry", "armour",
    "catapult", "cannon", "artillery", "howitzer", "fighters", "bombers", "helicopter", "stealthfighter",
    "stealthbomber", "trireme", "caravel", "galleon", "frigate", "ironclad", "destroyer", "cruiser",
    "aegiscruiser", "battleship", "submarine", "carrier", "transport", "cruisemissile", "nuclearmissile",
    "diplomat", "spy", "caravan", "freight", "explorer",
)

TERRAIN_FILES = (
    "desert", "plains", "grassland", "grassland", "hills", "mountains", "tundra", "glacier", "swamp",
    "jungle", "ocean",
)

# One entry per colour slot, matching the flag painted into the same column of
# CITIES.png just below. These are read back as the text colour a civilisation's
# city name, size marker and unit shield are drawn in, so a slot whose colour
# disagrees with its flag gives a civilisation two identities at once: slot 5 was
# orange here and cyan on the flag, which is what the Celts were showing.
PLAYER_COLORS = (
    (198, 42, 42),    # 0 red     - barbarians
    (238, 238, 238),  # 1 white
    (56, 152, 40),    # 2 green
    (34, 96, 206),    # 3 blue
    (226, 176, 26),   # 4 yellow
    (36, 158, 214),   # 5 cyan
    (240, 110, 18),   # 6 orange
    (134, 62, 178),   # 7 purple
    (128, 72, 24),    # 8 brown
)


def contain(source: Image.Image, size: tuple[int, int], padding: int = 0) -> Image.Image:
    source = source.convert("RGBA")
    bounds = source.getchannel("A").getbbox()
    if bounds:
        source = source.crop(bounds)
    available = (max(1, size[0] - 2 * padding), max(1, size[1] - 2 * padding))
    source.thumbnail(available, Image.Resampling.LANCZOS)
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    result.alpha_composite(source, ((size[0] - source.width) // 2, size[1] - padding - source.height))
    return result


def terrain_source(name: str, variant: int = 1) -> Path:
    """Base terrain art, preferring a PNG diamond over the legacy square JPEG.

    Civ II keeps two interchangeable tiles per terrain so a landmass does not
    visibly repeat; a "_b" file supplies the second one where it exists.
    """
    if variant == 2:
        alternate = ART / "Terrain" / f"{name}_b.png"
        if alternate.exists():
            return alternate

    png = ART / "Terrain" / f"{name}.png"
    return png if png.exists() else ART / "Terrain" / f"{name}.jpg"


def special_source(name: str, slot: int) -> Path | None:
    """Painted art for a terrain's special resource, if it has been made yet."""
    path = ART / "Terrain" / "Specials" / f"{name}_{slot}.png"
    return path if path.exists() else None


def diamond_texture(path: Path) -> Image.Image:
    with Image.open(path) as loaded:
        tile = ImageOps.fit(loaded.convert("RGB"), (64, 32), Image.Resampling.LANCZOS)
    mask = Image.new("L", tile.size, 0)
    ImageDraw.Draw(mask).polygon([(32, 0), (63, 15), (32, 31), (0, 16)], fill=255)
    tile.putalpha(mask)
    return tile


def build_units() -> None:
    sheet = Image.new("RGBA", (612, 344), (0, 0, 0, 0))
    for index in range(63):
        name = UNIT_FILES[index] if index < len(UNIT_FILES) else "warriors"
        path = ART / "Units" / f"{name}.png"
        if not path.exists():
            continue
        with Image.open(path) as loaded:
            sprite = contain(loaded, (64, 48), 1)
        x, y = 1 + 65 * (index % 9), 1 + 49 * (index // 9)
        sheet.alpha_composite(sprite, (x, y))
        sheet.putpixel((x + 5, y - 1), (0, 0, 255, 255))
        sheet.putpixel((x - 1, y + 31), (0, 0, 255, 255))

    draw = ImageDraw.Draw(sheet)
    shield = [(1, 0), (10, 0), (11, 4), (9, 16), (6, 19), (3, 16), (0, 4)]
    for left in (586, 599):
        draw.polygon([(left + x, 1 + y) for x, y in shield], fill=(210, 45, 45, 255), outline=(30, 30, 30, 255))
    draw.polygon([(597 + x, 30 + y) for x, y in shield], fill=(210, 45, 45, 255), outline=(30, 30, 30, 255))
    sheet.save(OUT / "UNITS.png", optimize=True)


def build_cities() -> None:
    sheet = Image.new("RGBA", (600, 472), (0, 0, 0, 0))
    cultures = ("Aztec", "German", "Greek", "Japanese", "London", "USA")
    for row, culture in enumerate(cultures):
        for column in range(8):
            path = ART / "Cities" / culture / f"city_{column + 1:02}.png"
            with Image.open(path) as loaded:
                city = contain(loaded, (64, 48))
            x = 1 + 65 * (column % 4) if column < 4 else 334 + 65 * (column - 4)
            y = 39 + 49 * row
            sheet.alpha_composite(city, (x, y))
            sheet.putpixel((x + 5, y - 1), (0, 0, 255, 255))
            sheet.putpixel((x - 1, y + 28), (0, 0, 255, 255))
            sheet.putpixel((x + 50, y - 1), (255, 155, 0, 255))
            sheet.putpixel((x - 1, y + 39), (255, 155, 0, 255))

    draw = ImageDraw.Draw(sheet)
    for column, color in enumerate(PLAYER_COLORS):
        x = 1 + 15 * column
        draw.rectangle((x, 423, x + 13, 423), fill=(*color, 255))
        for row in range(2):
            flag_path = ART / "Flags" / f"flag_{row * 9 + column + 1:02}.png"
            with Image.open(flag_path) as loaded:
                flag = contain(loaded, (14, 22))
            sheet.alpha_composite(flag, (x, 425 + 23 * row))

    improvements = ART / "Terrain" / "Overlays" / "Improvements"
    for name, location in {
        "fortify": (143, 423), "fort": (208, 423), "airstrip": (273, 423), "airstripfull": (338, 423)
    }.items():
        with Image.open(improvements / f"{name}.png") as loaded:
            sheet.alpha_composite(contain(loaded, (64, 48)), location)
    sheet.save(OUT / "CITIES.png", optimize=True)


# Where each connection sprite ends on the 64x32 diamond, in slot order: slot 0
# is the isolated stub and slots 1-8 follow the neighbour order in
# MapNavigationFunctions.Neighbours (NE, E, SE, S, SW, W, NW, N). A neighbour
# that shares an edge is reached at that edge's midpoint; one that only touches a
# corner is reached at the corner.
_N, _E, _S, _W = (32, 0), (63, 16), (32, 31), (0, 16)
_MID = lambda a, b: ((a[0] + b[0]) // 2, (a[1] + b[1]) // 2)
CONNECTION_ENDS = [
    None,                # slot 0: isolated, drawn as a stub rather than a spoke
    _MID(_N, _E),        # NE
    _E,                  # E
    _MID(_E, _S),        # SE
    _S,                  # S
    _MID(_S, _W),        # SW
    _W,                  # W
    _MID(_W, _N),        # NW
    _N,                  # N
]


def draw_connections(draw: ImageDraw.ImageDraw, origin: tuple[int, int], slot: int,
                     railroad: bool = False) -> None:
    """Draw one connection sprite: a single spoke from the tile centre outwards.

    The renderer composites one of these per connected neighbour, so each slot
    must carry only its own spoke. Drawing the full rosette into every slot -- as
    this did before -- meant a tile with one neighbour showed roads running off
    all four sides, and a tile with four neighbours drew the same rosette four
    times over.

    This is the fallback for when the painted spokes in
    FOSSart/Terrain/Overlays/{Roads,Railroads} are not on disk; TerrainLoader
    replaces these whenever that art is present.
    """
    x, y = origin
    color = (212, 212, 205, 255) if railroad else (123, 81, 45, 255)
    width = 3 if railroad else 2
    center = (x + 32, y + 16)

    end = CONNECTION_ENDS[slot]
    if end is None:
        # Isolated: a short stub through the centre, not a spoke to an edge.
        draw.line((x + 26, y + 16, x + 38, y + 16), fill=color, width=width)
        return

    finish = (x + end[0], y + end[1])
    draw.line((center, finish), fill=color, width=width)
    if railroad:
        # Sleepers laid across the spoke, perpendicular to its run.
        span_x, span_y = finish[0] - center[0], finish[1] - center[1]
        length = max(abs(span_x), abs(span_y), 1)
        across = (-span_y / length, span_x / length)
        steps = max(int(length / 5), 1)
        for step in range(1, steps + 1):
            px = center[0] + span_x * step / steps
            py = center[1] + span_y * step / steps
            draw.line((px - across[0] * 3, py - across[1] * 3,
                       px + across[0] * 3, py + across[1] * 3),
                      fill=(55, 55, 52, 255), width=1)


# --- Tile overlays -----------------------------------------------------------
#
# Small isometric markers drawn onto a transparent 64x32 diamond. The diamond
# corners are (32, 0), (63, 15), (32, 31) and (0, 16).


def _overlay_canvas() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    canvas = Image.new("RGBA", (64, 32), (0, 0, 0, 0))
    return canvas, ImageDraw.Draw(canvas, "RGBA")


def tile_irrigation() -> Image.Image:
    """Two crossed water channels running along the tile's axes."""
    canvas, draw = _overlay_canvas()
    water = (86, 156, 214, 235)
    sheen = (168, 214, 244, 220)
    for offset in (-6, 6):
        top = 16 + offset
        draw.line((12, 16 + offset // 2, 32, top - 8), fill=water, width=3)
        draw.line((32, top - 8, 52, 16 + offset // 2), fill=water, width=3)
        draw.line((12, 16 + offset // 2, 32, top + 8), fill=water, width=3)
        draw.line((32, top + 8, 52, 16 + offset // 2), fill=water, width=3)
    draw.line((14, 16, 50, 16), fill=sheen, width=1)
    return canvas


def tile_farmland() -> Image.Image:
    """A denser irrigated lattice with cultivated rows between the channels."""
    canvas, draw = _overlay_canvas()
    water = (86, 156, 214, 225)
    crop = (150, 176, 76, 200)
    for step in (-9, -3, 3, 9):
        draw.line((14, 16 + step // 2, 50, 16 + step // 2), fill=crop, width=2)
    for step in (-12, -4, 4, 12):
        draw.line((32 + step, 6 + abs(step) // 3, 32 + step, 26 - abs(step) // 3),
                  fill=water, width=2)
    return canvas


def tile_mine() -> Image.Image:
    """A cut into the hillside with spoil heaped beside it."""
    canvas, draw = _overlay_canvas()
    draw.polygon([(28, 12), (44, 12), (48, 22), (24, 22)], fill=(58, 50, 44, 235))
    draw.polygon([(30, 13), (42, 13), (45, 19), (27, 19)], fill=(28, 24, 21, 245))
    for cx, cy, r in ((21, 21, 4), (49, 20, 3), (26, 24, 3)):
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(122, 116, 108, 235),
                     outline=(70, 66, 60, 245))
    draw.line((34, 16, 40, 14), fill=(214, 186, 96, 235), width=2)
    return canvas


def tile_pollution() -> Image.Image:
    """Sour ground and a low haze."""
    canvas, draw = _overlay_canvas()
    for cx, cy, rx, ry in ((26, 18, 10, 5), (40, 15, 9, 5), (33, 22, 11, 4)):
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(96, 84, 62, 215))
    for cx, cy, rx, ry in ((28, 12, 7, 4), (41, 11, 6, 3)):
        draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(126, 122, 118, 165))
    for x, y in ((24, 20), (31, 17), (38, 20), (44, 16)):
        draw.point((x, y), fill=(48, 40, 30, 255))
    return canvas.filter(ImageFilter.GaussianBlur(0.6))


def tile_shield() -> Image.Image:
    """The grassland resource marker."""
    canvas, draw = _overlay_canvas()
    shield = [(32, 8), (39, 11), (38, 20), (32, 25), (26, 20), (25, 11)]
    draw.polygon(shield, fill=(78, 92, 64, 245), outline=(232, 232, 220, 255))
    draw.polygon([(32, 11), (36, 13), (35, 19), (32, 22), (29, 19), (28, 13)],
                 fill=(140, 160, 108, 245))
    return canvas


def tile_hut() -> Image.Image:
    """A small shelter marking an unexplored find."""
    canvas, draw = _overlay_canvas()
    draw.polygon([(24, 24), (24, 16), (40, 16), (40, 24)], fill=(196, 164, 106, 245),
                 outline=(120, 94, 52, 255))
    draw.polygon([(21, 17), (32, 8), (43, 17)], fill=(158, 116, 62, 250),
                 outline=(104, 74, 36, 255))
    draw.rectangle((30, 19, 34, 24), fill=(58, 42, 26, 255))
    draw.line((24, 20, 40, 20), fill=(150, 120, 74, 200), width=1)
    return canvas


def build_terrain1() -> None:
    sheet = Image.new("RGBA", (586, 480), (0, 0, 0, 0))
    draw = ImageDraw.Draw(sheet)
    for row, name in enumerate(TERRAIN_FILES):
        for variant, x in enumerate((1, 66), 1):
            sheet.alpha_composite(diamond_texture(terrain_source(name, variant)),
                                  (x, 1 + 33 * row))
        for special, x in enumerate((131, 196), 1):
            art = special_source(name, special)
            if art is not None:
                with Image.open(art) as loaded:
                    resource = contain(loaded, (64, 32))
            else:
                # No painting for this resource yet; a clear marker still reads.
                resource = Image.new("RGBA", (64, 32), (0, 0, 0, 0))
                rdraw = ImageDraw.Draw(resource)
                rdraw.ellipse((27, 10, 37, 20),
                              fill=((246, 205, 47, 230) if special == 1 else (235, 235, 235, 230)),
                              outline=(40, 40, 40, 255), width=1)
            sheet.alpha_composite(resource, (x, 1 + 33 * row))

    for slot in range(9):
        draw_connections(draw, (1 + 65 * slot, 363), slot, False)
        draw_connections(draw, (1 + 65 * slot, 397), slot, True)

    # The six 64x32 slots in this column are read by the renderer as irrigation,
    # farmland, mine, pollution, the grassland shield and a goody hut. They each
    # need their own overlay; reusing one icon for all six puts a fortress on
    # every shielded grassland square.
    for y, overlay in (
        (100, tile_irrigation()),
        (133, tile_farmland()),
        (166, tile_mine()),
        (199, tile_pollution()),
        (232, tile_shield()),
        (265, tile_hut()),
    ):
        sheet.alpha_composite(overlay, (456, y))

    dither = Image.new("RGBA", (64, 32), (160, 160, 160, 255))
    dd = ImageDraw.Draw(dither)
    for y in range(32):
        for x in range(64):
            if (x + y) % 2:
                dd.point((x, y), fill=(0, 0, 0, 255))
    sheet.alpha_composite(dither, (1, 447))
    sheet.alpha_composite(diamond_texture(terrain_source("ocean")), (131, 447))
    sheet.save(OUT / "TERRAIN1.png", optimize=True)


def build_terrain2() -> None:
    sheet = Image.new("RGBA", (530, 480), (0, 0, 0, 0))
    overlay_groups = (("Rivers", "river", 67), ("Forest", "forest", 133), ("Mountains", "mountain", 199), ("Hills", "hill", 265))
    for directory, stem, y in overlay_groups:
        for index in range(16):
            path = ART / "Terrain" / "Overlays" / directory / f"{stem}_{index % 8 + 1:02}.png"
            with Image.open(path) as loaded:
                item = contain(loaded, (64, 32))
            sheet.alpha_composite(item, (1 + 65 * (index % 8), y + 33 * (index // 8)))

    draw = ImageDraw.Draw(sheet)
    for index in range(16):
        x, y = 1 + 65 * (index % 8), 1 + 33 * (index // 8)
        draw.line((x, y + 16, x + 63, y + 16), fill=(68, 132, 178, 120), width=2)
    for column in range(4):
        x = 1 + 65 * column
        draw.arc((x + 4, 335, x + 60, 355), 180, 360, fill=(110, 187, 220, 220), width=3)
    for index in range(8):
        x = 1 + 66 * index
        draw.line((x, 445, x + 31, 429), fill=(100, 180, 215, 255), width=2)
        draw.line((x, 446, x + 31, 461), fill=(100, 180, 215, 255), width=2)
        draw.line((x, 463, x + 31, 479), fill=(100, 180, 215, 255), width=2)
        draw.line((x + 33, 479, x + 64, 463), fill=(100, 180, 215, 255), width=2)
    sheet.save(OUT / "TERRAIN2.png", optimize=True)


def build_icons() -> None:
    sheet = Image.new("RGBA", (600, 480), (0, 0, 0, 0))
    draw = ImageDraw.Draw(sheet)
    for y in range(sheet.height):
        shade = 78 + (y % 32) * 2
        draw.line((0, y, sheet.width, y), fill=(shade, shade, min(255, shade + 8), 255))

    draw.rounded_rectangle((199, 322, 262, 353), 7, fill=(65, 69, 78, 255), outline=(205, 210, 220, 255), width=2)
    draw.rounded_rectangle((298, 190, 329, 221), 5, fill=(218, 221, 227, 255), outline=(75, 78, 86, 255), width=2)
    draw.polygon([(199, 272), (231, 256), (262, 272), (231, 287)], outline=(245, 245, 245, 255), fill=(40, 55, 70, 180))
    for visible, x in ((False, 183), (True, 248)):
        for i in range(0, 64, 8):
            draw.line((x + i, 430, x + i, 461), fill=(245, 245, 245, 210 if visible else 110))
        for i in range(0, 32, 8):
            draw.line((x, 430 + i, x + 63, 430 + i), fill=(245, 245, 245, 210 if visible else 110))

    for column in range(8):
        x = 1 + 33 * column
        draw.ellipse((x + 4, 360, x + 27, 383), outline=(255, 215 - column * 12, 45, 255), width=3)
    for row_y, color in ((290, (85, 180, 255, 255)), (305, (255, 130, 45, 255))):
        for column in range(4):
            x = 49 + 15 * column
            draw.rectangle((x, row_y, x + 13, row_y + 13), fill=tuple(max(0, c - 18 * column) if i < 3 else c for i, c in enumerate(color)), outline=(25, 25, 25, 255))


    draw.rectangle((1, 389, 16, 404), fill=(185, 65, 65, 255), outline=(245, 245, 245, 255))
    draw.line((5, 393, 12, 400), fill=(255, 255, 255, 255), width=2)
    draw.line((12, 393, 5, 400), fill=(255, 255, 255, 255), width=2)
    for x, symbol in ((18, "+"), (35, "-")):
        draw.rectangle((x, 389, x + 15, 404), fill=(70, 100, 130, 255), outline=(245, 245, 245, 255))
        draw.text((x + 4, 388), symbol, fill=(255, 255, 255, 255))

    draw.ellipse((16, 320, 29, 333), fill=(245, 196, 45, 255), outline=(80, 55, 15, 255))
    draw.ellipse((31, 320, 44, 333), fill=(90, 185, 245, 255), outline=(20, 55, 80, 255))
    draw.polygon([(71, 343), (76, 334), (81, 343)], fill=(245, 225, 145, 255))

    for row in range(4):
        for column in range(5):
            x, y = 343 + 37 * column, 211 + 21 * row
            draw.rectangle((x, y, x + 35, y + 19), fill=(55 + 35 * column, 65 + 30 * row, 120 + 15 * column, 255), outline=(225, 225, 225, 255))
    # The view-piece cursor. Drawn onto a transparent scratch tile and pasted, so
    # the corners of its 64x32 slot stay clear: cutting the diamond straight out of
    # the sheet took the background with it and the cursor showed as a grey box.
    marker = Image.new("RGBA", (64, 32), (0, 0, 0, 0))
    ImageDraw.Draw(marker).polygon([(0, 16), (32, 0), (63, 16), (32, 31)],
                                   outline=(245, 245, 245, 255), fill=(40, 55, 70, 90))
    sheet.paste(marker, (199, 256))

    # Last, so nothing is drawn over them: the trade arrow above used to clip the
    # small trade icon.
    resource_icons.draw_all(lambda x, y, rgba: sheet.putpixel((x, y), rgba))

    sheet.save(OUT / "ICONS.png", optimize=True)
    build_view_piece_cursor()


def build_view_piece_cursor() -> None:
    """The marker drawn on the tile under the pointer.

    It came out of the 64x32 ICONS slot, so at the zoom this build reaches it was
    a five-times magnification of a stair-stepped outline, inside an opaque square
    that covered the map around it. This is the same diamond at map-art
    resolution with a soft dark backing to hold it against pale terrain.
    """
    w, h, ss = 300, 150, 4
    big = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
    draw = ImageDraw.Draw(big)
    points = [(0, h * ss // 2), (w * ss // 2, 0), (w * ss - 1, h * ss // 2), (w * ss // 2, h * ss - 1)]

    draw.line(points + [points[0]], fill=(15, 20, 28, 210), width=9 * ss // 2)
    draw.line(points + [points[0]], fill=(248, 250, 252, 255), width=3 * ss // 2)

    cursor = big.resize((w, h), Image.LANCZOS)
    cursor.save(OUT / "VIEWPIECE.png", optimize=True)



# The four backgrounds behind every dialog and the city screen. Each keeps the
# colour it has always had -- that colour is what tells the city screen's land,
# river and ocean variants apart, and what sets the contrast of the text drawn
# over the panel -- but the colour is now carried by the painted slate sheet the
# dialog chrome is already cut from, instead of a flat fill with a stripe every
# 26 px. The origin fractions are the same four well-separated regions of the
# sheet ImageUtils.TryBuildStoneWallpaper cuts its frame tiles from, so a dialog
# sits on the same painting its frame is cut from rather than on a flat panel.
BACKGROUND_SLOTS = (
    ("panel", (68, 74, 84), (0.05, 0.08)),
    ("city_land", (93, 125, 77), (0.62, 0.12)),
    ("city_river", (70, 119, 137), (0.28, 0.70)),
    ("city_ocean", (40, 90, 132), (0.88, 0.82)),
)

# How far the slate's own relief is allowed to reach around the slot colour. At
# 1.0 the painted sheet keeps exactly the contrast it has on disk; the sheet is
# already a low-contrast painting (about 17 levels of spread per channel), so
# pushing this higher buys noise in the JPEG rather than stone you can read.
STONE_RELIEF = 1.0


def channel_mean(plane: Image.Image) -> float:
    histogram = plane.histogram()
    return sum(level * count for level, count in enumerate(histogram)) / (plane.width * plane.height)


def stone_background(sheet: Image.Image, size: tuple[int, int], tint: tuple[int, int, int],
                     origin: tuple[float, float]) -> Image.Image:
    """Cut a region of the painted slate sheet and hold it at ``tint``.

    Every channel of the crop is re-based so its mean lands on the slot colour
    and its deviation from that mean is the painting's own. The relief therefore
    survives untouched while the background keeps exactly the colour it has always
    had: land, river and ocean stay distinguishable, and the panel stays as dark
    under the dialog text as it was.
    """
    if sheet.width < size[0] or sheet.height < size[1]:
        raise ValueError(f"slate sheet {sheet.size} is smaller than the {size} footprint")

    left = round((sheet.width - size[0]) * origin[0])
    top = round((sheet.height - size[1]) * origin[1])
    crop = sheet.crop((left, top, left + size[0], top + size[1]))

    planes = []
    for index, channel in enumerate("RGB"):
        plane = crop.getchannel(channel)
        centre = channel_mean(plane)
        base = tint[index]
        planes.append(plane.point([
            min(255, max(0, round(base + (level - centre) * STONE_RELIEF)))
            for level in range(256)
        ]))
    return Image.merge("RGB", planes)


def build_backgrounds() -> None:
    backgrounds = OUT / "Backgrounds"
    backgrounds.mkdir(parents=True, exist_ok=True)
    menu = Image.new("RGB", (1600, 900))
    draw = ImageDraw.Draw(menu)
    for y in range(menu.height):
        t = y / (menu.height - 1)
        draw.line((0, y, menu.width, y), fill=(round(16 + 14 * t), round(38 + 38 * t), round(62 + 42 * t)))
    for x in range(-400, 1800, 160):
        draw.ellipse((x, 500 + (x % 240), x + 650, 1040 + (x % 240)), fill=(37, 86, 65), outline=(80, 130, 95), width=4)
    menu = menu.filter(ImageFilter.GaussianBlur(2))
    menu.save(backgrounds / "main_menu.jpg", quality=88, optimize=True, progressive=True)

    # The painted slate sheet ships in the repo and is project-original (ASSET-MANIFEST
    # line for it), so cutting from it needs no new provenance. It is not generated
    # by this script, hence the explicit complaint rather than a bare traceback.
    sheet_path = backgrounds / "stonetexture.png"
    if not sheet_path.exists():
        raise FileNotFoundError(
            f"{sheet_path} is missing; the dialog and city-screen backgrounds are cut from it")

    with Image.open(sheet_path) as loaded:
        sheet = loaded.convert("RGB")
        for name, color, origin in BACKGROUND_SLOTS:
            image = stone_background(sheet, (1280, 520), color, origin)
            image.save(backgrounds / f"{name}.jpg", quality=86, optimize=True, progressive=True)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    build_units()
    build_cities()
    build_terrain1()
    build_terrain2()
    build_icons()
    build_backgrounds()
    print(f"Built standalone compatibility art in {OUT}")


if __name__ == "__main__":
    main()
