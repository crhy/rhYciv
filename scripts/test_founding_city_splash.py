#!/usr/bin/env python3
"""The founding-city splash art must exist, be crisp, and pair to its painting.

crhy/rhYciv#176: the FOUNDED popup drew the 64x48 CityImages sheet cell and
stretched it ~12x, smearing the founding image. scripts/build_founding_splashes.py
now cuts a 900x900 city_splash_NN.png per shipped city from the matching
~/rhYcivtextures/cities/<culture>/ painting, and BuildCity.cs draws it. This
test pins that every pairing the build script would accept actually shipped:
the splash exists, is a 900x900 RGBA keyed cutout (transparent corner), scores
the same >= 0.65 grayscale-128 similarity to a source painting as the city art
beside it does, and that the popup looks the splash up instead of the cell.

PIL and stdlib only; non-zero exit on any failure.
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

REPOSITORY = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from build_founding_splashes import (  # noqa: E402
    CITIES, MIN_SIMILARITY, SPLASH, TEXTURES, best_match)

BUILD_CITY = REPOSITORY / "RaylibUI" / "RunGame" / "Commands" / "Orders" / "BuildCity.cs"

failures: list[str] = []


def check(culture: str, city_name: str) -> None:
    index = int(city_name.rsplit("_", 1)[1].split(".")[0])
    art_path = CITIES / culture / f"city_{index:02d}.png"
    splash_path = CITIES / culture / f"city_splash_{index:02d}.png"
    paintings = sorted((TEXTURES / culture).glob("*.png"))

    with Image.open(art_path) as art:
        score, _ = best_match(art, paintings)
    if score < MIN_SIMILARITY:
        if splash_path.exists():
            failures.append(f"{culture}/city_{index:02d}: its best painting scores "
                            f"{score:.2f}, below {MIN_SIMILARITY:.2f}, yet a splash shipped")
        return

    if not splash_path.exists():
        failures.append(f"{culture}/city_{index:02d}: pairs at {score:.2f} but "
                        f"{splash_path.relative_to(REPOSITORY)} is missing")
        return

    with Image.open(splash_path) as splash:
        if splash.mode != "RGBA":
            failures.append(f"{culture}/city_{index:02d}: splash is {splash.mode}, expected keyed RGBA")
            return
        if splash.size != (SPLASH, SPLASH):
            failures.append(f"{culture}/city_{index:02d}: splash is {splash.size}, expected {SPLASH}x{SPLASH}")
            return
        if splash.getpixel((0, 0))[3] != 0:
            failures.append(f"{culture}/city_{index:02d}: splash corner (0,0) is not transparent")

        splash_score, painting = best_match(splash, paintings)
        if splash_score < MIN_SIMILARITY:
            failures.append(f"{culture}/city_{index:02d}: splash pairs only at {splash_score:.2f} "
                            f"(best {painting.name}), below {MIN_SIMILARITY:.2f}")


def main() -> int:
    for culture_dir in sorted(p for p in CITIES.iterdir() if p.is_dir()):
        for city_path in sorted(culture_dir.glob("city_[0-9][0-9].png")):
            check(culture_dir.name, city_path.name)

    build_city = BUILD_CITY.read_text(encoding="utf-8")
    if "SplashImage" not in build_city:
        failures.append("BuildCity.cs does not look up the founding splash (SplashImage)")

    if failures:
        for failure in failures:
            print(f"FAIL {failure}", file=sys.stderr)
        return 1
    print("founding splashes ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
