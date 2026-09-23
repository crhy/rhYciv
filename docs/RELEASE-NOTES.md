**The crashes that surviving an evening of play found, and the civilopedia pages that were blank.**

0.2.1 shipped and was played hard. This release closes the crashes it surfaced — null interface commands, out-of-range dialog tokens, empty city scenery tables, dead-unit battle animations — plus rebalances the world generation to match Civilization II's ratios (about three water tiles to one land, rivers on ~3% of land).

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.2.2-win-x64.zip` — **extract the folder first**, then run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.2.2-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.2.2-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.2.2-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.2.2-x86_64.flatpak` |

**Extract before running.** Windows produces `Could not load file or assembly 'System.Runtime'` when the `.exe` is double-clicked while still inside the zip. Nothing is wrong with the download.

The builds are unsigned. **macOS**: `xattr -dr com.apple.quarantine /Applications/rhYciv.app`. **Windows**: SmartScreen → *More info* → *Run anyway*.

## The crashes

A null stand-in (`NullInterfaceCommands`) now serves AI and test players so dialogs no longer NRE when there is no UI (#110). `DialogUtils` leaves `%STRING`/`%NUMBER` tokens literal when the index is missing or out of range instead of indexing at -1. `CivDialog` handles optional button lists and shared options without nulling or corrupting later showings. `CityView` falls back to the panorama background when the scenery table is empty (standalone rulesets ship no `cv.dll`) (#145). `CityTileMap` redraws its composed texture first when missing, `MovingPieces` falls the refused active unit back to `WaitingView`, and `AttackAnimation` skips null locations, dead units, and empty battles instead of throwing. City capture forces a redraw before the popup so the flag has changed.

## The map

Twenty Civ II saves at 75×120 read 23.9–42.8% land, most between 24 and 31 — about three water tiles to one of land. The middle land-mass setting was 0.49 (exactly, a world half land, twice Civ II's) and is 0.30 now; the flanking settings moved to 0.20 and 0.42 so neither edge leaves the original's range. Rivers went from 3.7% to 2.8% of land. Measured after the change: 30.1–30.3% land, rivers 3.0–3.3% across three seeds.

## The civilopedia

Terrain Info pages were blank — no prose was shown for any terrain. `CivilopediaLoader.GetTerrainIndex` maps base terrains 0–32 and specials +length/+2*length; `GetDescription` is now wrapped and embedded as `PediaLabel`s below the left-column stats on both base and special pages (#106). The Description button was also removed from every civilopedia view, since the description is already on the page navigated from (#104, #106).

## Blur

The shipped `TextureFilter` was Trilinear (2); Point (0) is now the default. Runtime configs that already set `TextureFilter` are unaffected (#106).

## Remaining reports

- Every square on the city resource map can be clicked (#77, #168).
- The civilopedia government/concept/improvement pages were unreachable buttons — now correct (#104).
- Multisampling stays off by default; `RHYCIV_MSAA=1` re-enables it (#153).
- The Enter-key shortcut gap in `CivDialog.OnKeyPress` (only maps `Labels.Ok`) is tracked separately.