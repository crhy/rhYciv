# Texture gap list

What is still procedural, placeholder, or on a legacy square tile after the
2026-09-01 terrain-art integration pass. Paired with
[STANDALONE-ASSET-AUDIT.md](STANDALONE-ASSET-AUDIT.md), which tracks the atlases
as a whole; this file is the shot list for painted replacements.

Provenance rules in [ASSET-PROVENANCE.md](ASSET-PROVENANCE.md) apply to every
item here: new art is project-original or generated from project-original
inputs, never traced or sampled from a commercial installation.

## Done: road and railroad connections

Painted spokes now replace the generator-drawn rosettes. The renderer composites
one sprite per connected neighbour, so the art is eight half-spokes running from
the tile centre to the point where each neighbour is reached -- the midpoint of
the shared edge for a neighbour that shares one, the diamond corner for a
neighbour that only touches at a corner -- plus an isolated stub for a tile with
no connections. `scripts/prepare_road_overlays.py` cuts them from the
straight-through pieces in `~/rhYcivtextures/roads` and `~/rhYcivtextures/railroads`
into `FOSSart/Terrain/Overlays/{Roads,Railroads}`, and `TerrainLoader.ApplyFossConnectionArt`
composes them at the working tile size, so they keep their detail at high zoom
instead of being routed through the 64x32 sheet cell.

The procedural fallback in `build_standalone_sheets.draw_connections` was fixed
at the same time: it had been drawing the full four-way rosette into all nine
sprite slots, so a tile with a single neighbour showed roads running off every
side. It now draws one spoke per slot, in the renderer's neighbour order.

## Done in this pass

Painted isometric base diamonds now feed `Standalone/TERRAIN1.png` for **every
row**: desert, plains, grassland, hills, mountains, tundra, glacier, swamp,
jungle, and ocean — each with a 2nd interchangeable variant except desert and
mountains. Painted special-resource cutouts now render for **desert 1/2 (oasis,
oil), plains 1/2 (bison, grain), grassland 1/2 (pheasant, sheep), hills 2
(wine), tundra 1/2 (game, furs), glacier 2 (oil), swamp 1 (sugarcane), jungle
1/2 (bananas, cacao), ocean 1/2 (salmon, whale)**. Source art lives in
`~/rhYcivtextures/terrain/`; the pipeline is
`scripts/prepare_custom_textures.py` → `scripts/build_standalone_sheets.py`.

## Missing — terrain base tiles

| Terrain | State | Note |
|---|---|---|
| river (as terrain tint) | legacy `Terrain/river.jpg` | overlays exist, base does not |
| desert / mountains 2nd variant | none | single diamond, so those terrains visibly repeat |
| forest | n/a in this renderer's TERRAIN1 rows | drawn as base + `Overlays/Forest`; a painted forest-floor diamond exists in `terrain/alts/` if the row is ever added |
| coast blending | generator-drawn strokes in `build_terrain2()` | painted ocean base exists now, but the shoreline transitions are still vector |

## Missing — special-resource paintings

Still the procedural coloured marker in `build_terrain1()`:

| Terrain / slot | Civ II resource | Have art? |
|---|---|---|
| grassland row-2 pair | (shield) | reuses grassland 1/2 |
| hills 1 | coal | no |
| mountains 1 | gold | **yes** — cut from `FOSSart/Other/gold.jpg` (#150) |
| mountains 2 | iron | no |
| glacier 1 | ivory | no tusked-ivory art (seal + crab keyed in `terrain/alts/`) |
| swamp 2 | peat / oil | no (swamp 1 uses sugarcane) |

Unused source art still keyed and available in `~/rhYcivtextures/terrain/alts/`:
oasis alt, grassland lawn/conifer variants, oil-slick alt, yak, seal, crab.

## Done: map markers (goody hut, killed-unit)

The goody hut and the killed-unit marker are cut from their magenta generation
matte by `scripts/import_map_markers.py` into `FOSSart/Terrain/goodyhut.png`
and `FOSSart/Other/deadtroop.png`. `TerrainLoader` composes the hut onto the
tile with `ComposeSpecialTile` when FOSS terrain art is applied (the normal
path); the procedural `tile_hut()` cell in `Standalone/TERRAIN1.png` is only
the fallback when that art is missing.

## Missing — overlays and markers (generator-drawn vector, not painted)

- `TERRAIN1`: irrigation, farmland, mine, pollution and grassland shield. The
  goody-hut cell is still procedural as a fallback, but the map draws the
  painted `FOSSart/Terrain/goodyhut.png` when FOSS art is on (see above). The
  road and railroad connection sprites are now painted -- see below.
- `TERRAIN2`: coast, river-mouth, and ocean-edge transition strokes.
- `Standalone/ICONS.png`: every map-UI glyph (view toggles, zoom controls,
  progress rings, resource swatches, category chips) is drawn procedurally in
  `build_standalone_sheets.build_icons()`.

## Missing — UI and screen art

- Panel and city-window backgrounds (`panel.jpg`, `city_land.jpg`,
  `city_river.jpg`, `city_ocean.jpg`) are flat colour with a stripe every 26px,
  from `build_backgrounds()`. These are the placeholders behind every dialog and
  the city screen, so they set the tone of the whole UI. Painted tileable stone
  exists unused in the source set (`stonetileable.png`, `graystonetileable.png`)
  and would be a straight upgrade.
- `main_menu.jpg` is also a procedural gradient, but nothing reads it: the menu
  uses the painted `NewCartographerBackground.png`. It is generated and shipped
  for nothing.
- Civilopedia concept and category illustrations remain concise or procedural.
- No painted frames for battle resolution, production progress, or
  global-warming state changes (global warming is out of scope for rules but the
  art hooks exist).

## Source-set audit (2026-09-05)

Every file in `~/rhYcivtextures` was checked against what ships in `FOSSart`.
Findings, so this does not have to be redone:

| Source | Status |
|---|---|
| `roads/`, `railroads/` (9 each) | **now used** — 8 spokes each cut by `prepare_road_overlays.py`; the remaining junction pieces are not needed |
| `units/` (51) | all wired via `UNIT_NAMES` |
| `cities/<culture>/` (6 x 8) | all wired; `cities/aborted/` is deliberately excluded |
| `trees/` (8) | all wired as `Overlays/Forest` |
| `mountains/` (24) | `[0:8]` are peaks -> Mountains, `[16:24]` are grassy mounds -> Hills. The middle 8 are a greener low-rocky tier that suits neither slot; leaving them out is correct, though they would serve as extra Mountain variants if more variety is wanted. The 8 `hills_row10_*.png` are an older 300px RGB set, superseded. |
| `rivers/` (12 + 8) | `[4:12]` are rivers and are wired. `[0:4]` are coastline/island pieces, not rivers — correctly excluded. The 8 `river_0N.png` are an older 300px RGB set, superseded. |
| `terrain/` (26) | wired through `prepare_custom_textures.py` |
| `terrain/alts/` (7) | still unused: oasis alt, grassland lawn/conifer, oil slick, yak, seal, crab. Candidates for the empty special slots listed above. |
| `swamp_jungle_sprites_300/` (7) | already folded into `terrain/` as the jungle/swamp rows |
| root `ocean.png`, `Ocean2.png`, `salmon.png`, `Whale.png`, `Crab.png` | byte-identical duplicates of files under `terrain/`; the `terrain/` copies are the ones the pipeline reads |
| `GoldenSunsetWin.png` | wired — `Backgrounds/victory_conquest.png` is this painting with the victory line baked in, re-rendered at 1280x720 |
| `GoldenSunsetWinText.png` | a personal variant carrying an extra line of political text. **Not shipped, and must not be** — it would go out in every bundle. |
| `stonetexture.png`, `NewCartographerBackground.png` | wired (`ImageUtils.StoneTextureAsset`, `CompactInterface` `backgroundImage`) |
| `stonetileable.png`, `graystonetileable.png` | unused; see the panel-background item above |
| `_archive/` (29) | archived by hand, not part of the pipeline |

## Orphaned reference images

`RaylibUI/FOSSart/Other/*.jpg` (19 files: buffalo, fish, whales, wine, gems,
gold, furs, silk, spice, oasis, ivory, fruit, pheasant, shield-grassland, and
several "from above" mine/well shots) are **referenced by no runtime code** —
except `gold.jpg`, which is the source for the mountain Gold special cutout
(`Terrain/Specials/mountains_1.png`, issue #150). They predate the
`Terrain/Specials` pipeline. Either wire the rest into the Civilopedia resource
pages or remove them; do not add more art to that folder.

## City styles

Six cultures (Aztec, German, Greek, Japanese, London, USA) each ship 8 size
sprites plus the generated walls / flags / status markers. Not yet spot-checked
per culture for size-progression continuity or wall alignment at 3x zoom.
