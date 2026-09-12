"""Shared geometry and painting helpers for the isometric connection overlays.

The map renderer never draws a road, railway or river as one picture per tile.
It composites one *half-spoke* per connected neighbour, so two adjacent tiles
each draw their own half and the halves meet on the shared boundary. A spoke
that does not start exactly at the tile centre, or does not end exactly on the
boundary point where the neighbour is reached, produces the scattered dashes
that made roads look disconnected.

Cutting those halves out of painted source art by measuring where the ink lies
does not survive contact with the art: the painted segments are free-hand, so
their ink starts and ends wherever the brush did. This module takes the other
approach. The path is constructed exactly, from the tile centre to the required
boundary point, and the *painted material* is swept along it. The geometry is
then guaranteed correct and the art still supplies the surface.

The tile is Civ II's 64x32 diamond, with corners N(32,0) E(63,16) S(32,31)
W(0,16), authored here at 8x so the composed overlay keeps its detail at high
zoom.
"""

from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image

# Output canvas. The tile is 2:1 and the art is authored at that aspect, so the
# isometric angles survive the compose step in TerrainLoader without stretching.
SCALE = 8
CANVAS = (64 * SCALE, 32 * SCALE)

CORNER_N = (32.0 * SCALE, 0.0)
CORNER_E = (63.0 * SCALE, 16.0 * SCALE)
CORNER_S = (32.0 * SCALE, 31.0 * SCALE)
CORNER_W = (0.0, 16.0 * SCALE)
CENTRE = (32.0 * SCALE, 16.0 * SCALE)


def _midpoint(a: tuple[float, float], b: tuple[float, float]) -> tuple[float, float]:
    return ((a[0] + b[0]) / 2.0, (a[1] + b[1]) / 2.0)


# Where a spoke towards each neighbour has to end. A neighbour sharing an edge
# with this tile is reached at that edge's midpoint; one that only touches a
# corner is reached at the corner. Order matches the neighbour order in
# Engine/src/MapObjects/MapNavigationFunctions.
ENDPOINTS: dict[str, tuple[float, float]] = {
    "ne": _midpoint(CORNER_N, CORNER_E),
    "e": CORNER_E,
    "se": _midpoint(CORNER_E, CORNER_S),
    "s": CORNER_S,
    "sw": _midpoint(CORNER_S, CORNER_W),
    "w": CORNER_W,
    "nw": _midpoint(CORNER_W, CORNER_N),
    "n": CORNER_N,
}

# The eight neighbours, in the order MapImage indexes them as `neighbour + 1`.
EIGHT = ["ne", "e", "se", "s", "sw", "w", "nw", "n"]

# Rivers and dithering use only the four edge-sharing neighbours, in the order
# MapNavigationFunctions.DirectNeighbours yields them. MapImage builds the river
# sprite index as a bitmask over this order, bit 0 first.
FOUR = ["ne", "se", "sw", "nw"]

# How far past the tile centre a spoke is carried. Two opposite spokes meeting on
# an exact pixel boundary show a hairline seam; a little overlap hides it.
OVERLAP = 0.05


def diamond_mask(canvas: tuple[int, int] = CANVAS) -> np.ndarray:
    """The tile's diamond as a float mask, soft by one pixel at the rim."""
    width, height = canvas
    ys, xs = np.mgrid[0:height, 0:width]
    # |x - cx| / halfwidth + |y - cy| / halfheight <= 1 is the diamond.
    nx = np.abs(xs + 0.5 - width / 2.0) / (width / 2.0)
    ny = np.abs(ys + 0.5 - height / 2.0) / (height / 2.0)
    edge = nx + ny
    feather = 2.0 / width
    return np.clip((1.0 + feather - edge) / (2.0 * feather), 0.0, 1.0)


def key_matte(path: Path) -> tuple[np.ndarray, np.ndarray]:
    """Split painted source art into (rgb, alpha), keying out the magenta matte.

    The generation matte is a saturated magenta that is flat only to within a
    few levels, so it is keyed on the shape of the colour -- red and blue high,
    green low -- rather than on distance from one sampled value. The alpha ramp
    is then used to unmix the matte back out of the anti-aliased edge, which is
    what stops a pink rim surviving on thin brush strokes.
    """
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float64)
    red, green, blue = rgb[..., 0], rgb[..., 1], rgb[..., 2]

    # Matte-ness: how far this pixel is into "magenta" territory. The painted
    # material is tan, olive, blue or white -- none of them come close.
    magenta = np.minimum(red, blue) - green
    near, far = 40.0, 110.0
    alpha = np.clip((far - magenta) / (far - near), 0.0, 1.0)

    border = np.concatenate([rgb[:3].reshape(-1, 3), rgb[-3:].reshape(-1, 3),
                             rgb[:, :3].reshape(-1, 3), rgb[:, -3:].reshape(-1, 3)])
    matte = np.median(border, axis=0)

    with np.errstate(divide="ignore", invalid="ignore"):
        unmixed = (rgb - (1.0 - alpha)[..., None] * matte) / np.maximum(alpha, 1e-6)[..., None]
    cleaned = np.where(alpha[..., None] > 0.0, np.clip(unmixed, 0, 255), 0.0)
    return cleaned, alpha


# How far past the water's own width the profile reaches, to carry the banks.
BANK_MARGIN = 2.1


def _water_mask(colours: np.ndarray) -> np.ndarray:
    """Which of these painted pixels are water rather than land.

    The painted material is tan, olive, grey or blue; only the water is blue, and
    it is blue by a margin rather than marginally. Used to find the channel in a
    source that carries more than the channel.
    """
    red, green, blue = colours[:, 0], colours[:, 1], colours[:, 2]
    return (blue > red + 18) & (blue > green + 6)


def cross_section(path: Path, samples: int = 129) -> tuple[np.ndarray, ...]:
    """Reduce a painted segment to the profile that sweeping it will reproduce.

    Returns ``(colour, coverage, width, tone)``. The first two are the median
    colour and coverage across the stroke, which carry the crown, the shoulders
    and the frayed edge of the painted material. The last two are measured along
    the stroke -- how it thickens and thins, and how it lightens and darkens --
    and are what keep a swept spoke from reading as a smooth extrusion.
    """
    rgb, alpha = key_matte(path)
    ys, xs = np.nonzero(alpha > 0.25)
    if len(xs) < 64:
        raise SystemExit(f"{path.name}: almost no ink after keying the matte")

    points = np.stack([xs, ys], axis=1).astype(np.float64)

    # The axis is the *water's*, not the painting's.
    #
    # Keying the matte leaves whatever was painted on it, and that is not always
    # a ribbon of river: a source painted as an island with a river through it
    # leaves the whole island, whose axis is the island's. Measured against that,
    # the slice runs across vegetation, bank, a little water and bank again, and
    # the median of it is mud -- no channel in the profile at all, which is what
    # sweeping it then reproduced on every tile.
    #
    # So the frame is taken from the blue pixels, which are the water wherever the
    # river is in the frame, and the profile is sampled across it out to a margin
    # wide enough to carry the banks. A source that is already a ribbon gives the
    # same answer it always did; one painted as a scene now gives the right one.
    channel = _water_mask(rgb[ys, xs])
    frame_points = points[channel] if channel.sum() >= 512 else points

    centre = frame_points.mean(axis=0)
    _, _, vectors = np.linalg.svd(frame_points - centre, full_matrices=False)
    axis, normal = vectors[0], vectors[1]

    centred = points - centre
    u = centred @ axis      # along the stroke
    v = centred @ normal    # across it

    # How far out to sample. Measured on the water, then opened up to take in the
    # banks either side; measured on everything, it would be the island's width.
    channel_half = None
    if channel.sum() >= 512:
        channel_v = (frame_points - centre) @ normal
        channel_half = float(np.percentile(np.abs(channel_v), 98.0)) * BANK_MARGIN

    weights = alpha[ys, xs]
    colours = rgb[ys, xs]

    # The painted sources meander, so the straight PCA axis is not the stroke's
    # centreline: measured against it, a source that swings 100px up and down
    # looks 200px wide. Follow the stroke instead -- the median offset of the
    # ink in each slice along it -- and measure the profile from there. Without
    # this the swept spoke comes out as wide as the source's meander and as flat
    # as its average colour.
    slices = 128
    slice_edges = np.linspace(u.min(), u.max(), slices + 1)
    slice_index = np.clip(np.digitize(u, slice_edges) - 1, 0, slices - 1)
    centreline = np.zeros(slices)
    seen = np.zeros(slices, dtype=bool)
    for bin_index in range(slices):
        selected = slice_index == bin_index
        if selected.any():
            centreline[bin_index] = np.median(v[selected])
            seen[bin_index] = True
    positions = np.arange(slices)
    if seen.any():
        centreline = np.interp(positions, positions[seen], centreline[seen])
    v = v - centreline[slice_index]

    # Across: median colour and mean coverage in each of `samples` bins, spanning
    # the middle of the ink so a stray speck cannot widen the profile.
    # Never past the edge of what was painted: opening the span out to take in the
    # banks must not open it out into bare matte, where the bins have nothing to
    # take a median of and the profile ends in a block of whatever survived the
    # key at the very edge of the brush stroke.
    painted_half = float(np.percentile(np.abs(v), 99.0))
    half = min(channel_half, painted_half) if channel_half else painted_half
    edges = np.linspace(-half, half, samples + 1)
    index = np.clip(np.digitize(v, edges) - 1, 0, samples - 1)

    colour = np.zeros((samples, 3))
    coverage = np.zeros(samples)
    for bin_index in range(samples):
        selected = index == bin_index
        if not selected.any():
            continue
        colour[bin_index] = np.median(colours[selected], axis=0)
        # Coverage is the share of the stroke's length this offset is painted on.
        coverage[bin_index] = weights[selected].sum() / max(1.0, selected.sum())

    # Fill any empty bin from its neighbours so the sweep has no holes.
    filled = coverage > 0
    if filled.any():
        positions = np.arange(samples)
        for channel in range(3):
            colour[:, channel] = np.interp(positions, positions[filled], colour[filled, channel])
        coverage = np.interp(positions, positions[filled], coverage[filled])

    # Along: how the stroke thickens and how its tone shifts, as series over its
    # length. Both are normalised to their own mean, so they modulate whatever
    # width and colour the caller asks for rather than imposing the source's.
    bins = 256
    along_edges = np.linspace(np.percentile(u, 1.0), np.percentile(u, 99.0), bins + 1)
    along_index = np.clip(np.digitize(u, along_edges) - 1, 0, bins - 1)
    width = np.ones(bins)
    tone = np.ones(bins)
    luminance = colours @ np.array([0.299, 0.587, 0.114])
    for bin_index in range(bins):
        selected = along_index == bin_index
        if selected.any():
            width[bin_index] = np.percentile(np.abs(v[selected]), 95.0)
            tone[bin_index] = np.median(luminance[selected])
    width = np.clip(width / max(width.mean(), 1e-6), 0.75, 1.25)
    tone = np.clip(tone / max(tone.mean(), 1e-6), 0.82, 1.18)

    return colour, coverage, width, tone


def _path_points(start: tuple[float, float], end: tuple[float, float],
                 wobble: float, seed: int, steps: int = 512) -> np.ndarray:
    """A gently sinuous path from start to end, pinned exactly at both ends.

    The painted sources meander, and a ruler-straight spoke next to them looks
    machined. The wobble is a half sine, so it is zero at both ends: whatever it
    does in between, the spoke still starts at the tile centre and ends on the
    boundary point the neighbouring tile's spoke will meet.
    """
    t = np.linspace(0.0, 1.0, steps)
    start_array, end_array = np.array(start), np.array(end)
    line = start_array + (end_array - start_array) * t[:, None]

    direction = end_array - start_array
    length = float(np.hypot(*direction))
    if length < 1e-6:
        return line
    normal = np.array([-direction[1], direction[0]]) / length

    rng = np.random.default_rng(seed)
    phase = rng.uniform(0.0, math.tau)
    lateral = (np.sin(math.pi * t) * np.sin(math.tau * t + phase)) * wobble * length
    return line + normal * lateral[:, None]


def sweep(profile: tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray],
          start: tuple[float, float], end: tuple[float, float],
          width: float, seed: int, wobble: float = 0.05,
          canvas: tuple[int, int] = CANVAS,
          taper_end: float = 1.0) -> tuple[np.ndarray, np.ndarray]:
    """Sweep a painted cross-section along a path, returning (rgb, alpha).

    ``width`` is the full painted width in canvas pixels. ``taper_end`` scales
    the width at the far end, so a river can widen as it reaches the sea.
    """
    colour, coverage, along, tone = profile
    samples = len(coverage)
    width_pixels, height_pixels = canvas

    points = _path_points(start, end, wobble, seed)
    steps = len(points)

    rgb = np.zeros((height_pixels, width_pixels, 3))
    alpha = np.zeros((height_pixels, width_pixels))

    # Rasterise by walking the path and stamping the profile square to it. The
    # steps are dense enough that consecutive stamps overlap.
    tangents = np.gradient(points, axis=0)
    lengths = np.hypot(tangents[:, 0], tangents[:, 1])
    lengths[lengths < 1e-9] = 1e-9
    normals = np.stack([-tangents[:, 1] / lengths, tangents[:, 0] / lengths], axis=1)

    offsets = np.linspace(-1.0, 1.0, samples)
    for step in range(steps):
        t = step / (steps - 1)
        scale = width * (1.0 + (taper_end - 1.0) * t) / 2.0
        step_index = int(t * (len(along) - 1))
        scale *= along[step_index]
        shade = tone[step_index]

        base = points[step]
        normal = normals[step]
        xs = base[0] + normal[0] * offsets * scale
        ys = base[1] + normal[1] * offsets * scale

        xi = np.round(xs).astype(int)
        yi = np.round(ys).astype(int)
        inside = (xi >= 0) & (xi < width_pixels) & (yi >= 0) & (yi < height_pixels)
        if not inside.any():
            continue

        xi, yi = xi[inside], yi[inside]
        weight = coverage[inside]
        take = weight > alpha[yi, xi]
        if not take.any():
            continue
        xi, yi, weight = xi[take], yi[take], weight[take]
        alpha[yi, xi] = weight
        rgb[yi, xi] = np.clip(colour[inside][take] * shade, 0, 255)

    return rgb, alpha


def _sample(image_rgb: np.ndarray, image_alpha: np.ndarray,
            xs: np.ndarray, ys: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Bilinear sample of keyed source art, transparent outside its bounds."""
    height, width = image_alpha.shape
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    fx = (xs - x0)[..., None]
    fy = (ys - y0)[..., None]

    def at(dx: int, dy: int) -> tuple[np.ndarray, np.ndarray]:
        xi = np.clip(x0 + dx, 0, width - 1)
        yi = np.clip(y0 + dy, 0, height - 1)
        return image_rgb[yi, xi], image_alpha[yi, xi]

    c00, a00 = at(0, 0)
    c10, a10 = at(1, 0)
    c01, a01 = at(0, 1)
    c11, a11 = at(1, 1)

    top_c, bottom_c = c00 * (1 - fx) + c10 * fx, c01 * (1 - fx) + c11 * fx
    top_a = a00 * (1 - fx[..., 0]) + a10 * fx[..., 0]
    bottom_a = a01 * (1 - fx[..., 0]) + a11 * fx[..., 0]

    colour = top_c * (1 - fy) + bottom_c * fy
    coverage = top_a * (1 - fy[..., 0]) + bottom_a * fy[..., 0]

    outside = (xs < 0) | (xs > width - 1) | (ys < 0) | (ys > height - 1)
    coverage = np.where(outside, 0.0, coverage)
    return colour, coverage


class StraightSource:
    """A straight painted segment, measured in its own along/across frame.

    Railway track carries structure along its length -- rails, sleepers, ballast
    -- that a swept cross-section would smear into continuous stripes. The
    sources for it are painted straight, so the whole picture can be resampled
    along the spoke instead, which keeps the sleepers.
    """

    def __init__(self, path: Path):
        self.rgb, self.alpha = key_matte(path)
        ys, xs = np.nonzero(self.alpha > 0.25)
        if len(xs) < 64:
            raise SystemExit(f"{path.name}: almost no ink after keying the matte")

        points = np.stack([xs, ys], axis=1).astype(np.float64)
        self.centre = points.mean(axis=0)
        centred = points - self.centre
        _, _, vectors = np.linalg.svd(centred, full_matrices=False)
        self.axis, self.normal = vectors[0], vectors[1]

        along = centred @ self.axis
        across = centred @ self.normal
        self.along_min = float(np.percentile(along, 1.0))
        self.along_max = float(np.percentile(along, 99.0))
        self.half_width = float(np.percentile(np.abs(across), 99.0))

    def at(self, along_fraction: np.ndarray | float,
           across_fraction: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        """Sample at a fraction along the segment and across its width."""
        along = self.along_min + (self.along_max - self.along_min) * along_fraction
        across = across_fraction * self.half_width
        xs = self.centre[0] + self.axis[0] * along + self.normal[0] * across
        ys = self.centre[1] + self.axis[1] * along + self.normal[1] * across
        return _sample(self.rgb, self.alpha, xs, ys)


def warp(source: StraightSource, start: tuple[float, float], end: tuple[float, float],
         width: float, seed: int, wobble: float = 0.03,
         canvas: tuple[int, int] = CANVAS,
         reference_length: float | None = None) -> tuple[np.ndarray, np.ndarray]:
    """Resample a straight painted segment along a spoke, returning (rgb, alpha).

    ``reference_length`` is the spoke length at which the whole source is used.
    Shorter spokes take proportionally less of it, so structure painted along
    the segment -- sleepers, rail joints -- keeps the same pitch on every spoke
    instead of being squashed on the short diagonals.
    """
    width_pixels, height_pixels = canvas
    points = _path_points(start, end, wobble, seed)
    steps = len(points)

    length = float(np.hypot(*(np.array(end) - np.array(start))))
    used = 1.0 if not reference_length else min(1.0, length / reference_length)

    rgb = np.zeros((height_pixels, width_pixels, 3))
    alpha = np.zeros((height_pixels, width_pixels))

    tangents = np.gradient(points, axis=0)
    lengths = np.hypot(tangents[:, 0], tangents[:, 1])
    lengths[lengths < 1e-9] = 1e-9
    normals = np.stack([-tangents[:, 1] / lengths, tangents[:, 0] / lengths], axis=1)

    samples = 161
    offsets = np.linspace(-1.0, 1.0, samples)
    for step in range(steps):
        t = step / (steps - 1)
        base = points[step]
        normal = normals[step]
        xs = base[0] + normal[0] * offsets * width / 2.0
        ys = base[1] + normal[1] * offsets * width / 2.0

        colour, coverage = source.at((1.0 - used) / 2.0 + t * used, offsets)

        xi = np.round(xs).astype(int)
        yi = np.round(ys).astype(int)
        inside = (xi >= 0) & (xi < width_pixels) & (yi >= 0) & (yi < height_pixels)
        if not inside.any():
            continue
        xi, yi = xi[inside], yi[inside]
        weight, tone = coverage[inside], colour[inside]
        take = weight > alpha[yi, xi]
        if not take.any():
            continue
        alpha[yi[take], xi[take]] = weight[take]
        rgb[yi[take], xi[take]] = tone[take]

    return rgb, alpha


def fill_holes(rgb: np.ndarray, alpha: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Close the single-pixel gaps a point-stamped sweep leaves behind."""
    padded_alpha = np.pad(alpha, 1)
    padded_rgb = np.pad(rgb, ((1, 1), (1, 1), (0, 0)))

    neighbour_alpha = np.zeros_like(alpha)
    neighbour_rgb = np.zeros_like(rgb)
    count = np.zeros_like(alpha)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dx == 0 and dy == 0:
                continue
            shifted_alpha = padded_alpha[1 + dy:1 + dy + alpha.shape[0],
                                         1 + dx:1 + dx + alpha.shape[1]]
            shifted_rgb = padded_rgb[1 + dy:1 + dy + alpha.shape[0],
                                     1 + dx:1 + dx + alpha.shape[1]]
            neighbour_alpha += shifted_alpha
            neighbour_rgb += shifted_rgb * (shifted_alpha > 0)[..., None]
            count += shifted_alpha > 0

    hole = (alpha == 0) & (count >= 5)
    filled_alpha = alpha.copy()
    filled_rgb = rgb.copy()
    filled_alpha[hole] = neighbour_alpha[hole] / 8.0
    filled_rgb[hole] = neighbour_rgb[hole] / np.maximum(count[hole], 1)[..., None]
    return filled_rgb, filled_alpha


def to_image(rgb: np.ndarray, alpha: np.ndarray, clip_to_tile: bool = True) -> Image.Image:
    """Assemble a swept overlay into an RGBA image, clipped to the diamond."""
    if clip_to_tile:
        alpha = alpha * diamond_mask((rgb.shape[1], rgb.shape[0]))
    data = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255.0, 0, 255)]).astype(np.uint8)
    return Image.fromarray(data, "RGBA")


def spoke_path(name: str, overlap: float = OVERLAP) -> tuple[tuple[float, float], tuple[float, float]]:
    """Start and end of the half-spoke towards ``name``.

    The start is carried a little past the tile centre, away from the endpoint,
    so two opposite spokes overlap instead of butting together on a pixel edge.
    """
    end = ENDPOINTS[name]
    dx, dy = end[0] - CENTRE[0], end[1] - CENTRE[1]
    length = math.hypot(dx, dy)
    if length < 1e-6:
        return CENTRE, end
    back = overlap * CANVAS[0]
    start = (CENTRE[0] - dx / length * back, CENTRE[1] - dy / length * back)
    return start, end
