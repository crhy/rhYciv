#!/usr/bin/env python3
"""Cut the founding-city popup's splash art from the project's own paintings.

crhy/rhYciv#176: the FOUNDED popup (RaylibUI/RunGame/Commands/Orders/BuildCity.cs)
drew the CityImages sheet cell -- a 64x48 piece of RaylibUI/Standalone/CITIES.png,
itself the 300x300 FOSS city_NN.png art shrunk 4.7x by build_standalone_sheets
-- and then stretched that cell to roughly 600 pixels. A ~12x upscale of an
already downsampled picture: the smeared founding image in the issue's
screenshot.

The map art is untouched: the CityImages entries keep their 300x300 MapImage.
What the popup gains instead is a 900x900 city_splash_NN.png per shipped city,
cut from the matching painting in ~/rhYcivtextures/cities/<culture>/ -- the
same city subject re-rendered at 1254x1254 (f242b's specials-from-source-photos
proved the pairing pattern; no new art is drawn, the painting is only resized
and keyed with the silhouette of the shipped cutout beside it).

Pairing is measured, not guessed: a grayscale 128x128 downscale of each shipped
city_NN.png is compared to each painting of its culture by mean-absolute
similarity inside the cutout's own alpha. A city whose best painting scores
below 0.65 gets no splash and is said so in the printed table.

Usage:
    python3 scripts/build_founding_splashes.py
"""

from __future__ import annotations

import sys
from pathlib import Path

try:
    import numpy as np
    from PIL import Image
except ImportError:  # pragma: no cover - depends on the machine, not the code
    np = None
    Image = None

sys.path.insert(0, str(Path(__file__).resolve().parent))
from clean_matte_fringe import clean  # noqa: E402

REPOSITORY = Path(__file__).resolve().parents[1]
CITIES = REPOSITORY / "RaylibUI" / "FOSSart" / "Cities"
TEXTURES = Path.home() / "rhYcivtextures" / "cities"

SPLASH = 900          # the square the FOUNDED dialog draws the splash into
PROBE = 128           # size the pairing is measured at
MIN_SIMILARITY = 0.65  # below this a painting is not the same city


def _gray(image: Image.Image) -> np.ndarray:
    return np.asarray(
        image.convert("L").resize((PROBE, PROBE), Image.LANCZOS)).astype(np.float64)


def similarity(art: Image.Image, painting: Image.Image) -> float:
    """Grayscale 128x128 mean-absolute-difference similarity of a shipped city
    cutout against a source painting, inside the cutout's own artwork."""
    art_small = np.asarray(art.resize((PROBE, PROBE), Image.LANCZOS).convert("RGBA"))
    painting_small = _gray(painting)
    artwork = art_small[..., 3] > 127
    if not artwork.any():
        return 0.0
    mad = np.abs(art_small[..., :3].mean(axis=-1)[artwork] - painting_small[artwork]).mean()
    return 1.0 - mad / 255.0


def best_match(art: Image.Image, paintings: list[Path]) -> tuple[float, Path]:
    scored = sorted(((similarity(art, Image.open(path)), path) for path in paintings),
                    key=lambda scored: -scored[0])
    return scored[0]


def build(culture: str, index: int, painting: Path) -> None:
    """One 900x900 splash: the painting resized, keyed with the silhouette of
    the shipped city art beside it, so the popup and the map agree on shape."""
    with Image.open(painting) as source:
        resized = source.convert("RGB").resize((SPLASH, SPLASH), Image.LANCZOS)
    with Image.open(CITIES / culture / f"city_{index:02d}.png") as city:
        silhouette = city.convert("RGBA").getchannel("A").resize(
            (SPLASH, SPLASH), Image.LANCZOS)
    splash = Image.merge("RGBA", [*resized.split(), silhouette])
    path = CITIES / culture / f"city_splash_{index:02d}.png"
    splash.save(path, optimize=True)
    # The paintings carry the magenta generation matte back into their scenes;
    # the shipped city art beside them was cleaned of it, so the splash is too
    # (the house tool, scripts/clean_matte_fringe.py, keeps clean --check green).
    clean(path)


def main() -> int:
    if np is None or Image is None:
        print("splashes: skipped (needs numpy and Pillow)", file=sys.stderr)
        return 1

    missing_textures = False
    print(f"{'city':<28} {'painting':<44} {'sim':>5}")
    for culture_dir in sorted(p for p in CITIES.iterdir() if p.is_dir()):
        culture = culture_dir.name
        paintings = sorted((TEXTURES / culture).glob("*.png")) if (TEXTURES / culture).is_dir() else []
        if not paintings:
            print(f"{culture}: no paintings under {TEXTURES / culture}, skipping",
                  file=sys.stderr)
            missing_textures = True
            continue

        for city_path in sorted(culture_dir.glob("city_[0-9][0-9].png")):
            index = int(city_path.stem.rsplit("_", 1)[1])
            with Image.open(city_path) as art:
                score, painting = best_match(art, paintings)
            label = painting.relative_to(TEXTURES)
            if score < MIN_SIMILARITY:
                print(f"{culture}/city_{index:02d}.png{'':<10} {str(label):<44} {score:5.2f}  "
                      f"REFUSED: no painting at or above {MIN_SIMILARITY:.2f}, no splash")
                continue
            build(culture, index, painting)
            print(f"{culture}/city_{index:02d}.png{'':<10} {str(label):<44} {score:5.2f}")

    return 1 if missing_textures else 0


if __name__ == "__main__":
    raise SystemExit(main())
