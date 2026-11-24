# Civ2 UI Additions compatibility

This document tracks the user-facing features from
[FoxAhead/Civ2-UI-Additions](https://github.com/FoxAhead/Civ2-UI-Additions), audited at upstream commit
`2ab67912467768ac9b9ce999c281b7401e583983` on 2026-08-30. rhYciv reimplements applicable behavior in
the native Raylib UI; it does not copy the patch's Delphi code, original Civ II assets, executable patches,
or corrected proprietary DLL resources.

Applicable completed features are enabled by default. They do not require a separate launcher or settings
switch.

This is an implementation comparison, not a runtime or source dependency. New
forks can remove it without changing the game. Asset and legal status are defined
by `ASSET-PROVENANCE.md`, not by this compatibility matrix.

| # | Upstream feature | rhYciv status |
|---:|---|---|
| 1 | Wheel support; Ctrl+wheel zoom; Ctrl+middle reset; middle center/drag | Implemented by default. Wheel events also reach lists, tax sliders, and city specialists. |
| 2 | Scrollable Activate Unit stack dialog | Implemented by default with mouse, keyboard, and an unrestricted scrollbar. |
| 3 | Settler/Engineer work counter | Implemented by default on the map shield. |
| 4 | Correct specialist click bounds | Implemented by per-citizen hit boxes. |
| 5 | Game turn in sidebar | Implemented. |
| 6 | Exact research progress in Science Advisor | Implemented as current beakers / required beakers. |
| 7 | CD track looping/progress | Native replacement implemented: streamed menu music loops reliably and shows track elapsed/total progress without CD dependencies. |
| 8 | Game icon executable fix | Native replacement implemented: the Raylib window and copied release asset use an original FOSS-safe rhYciv application icon. |
| 9 | Reset city-name prompts on new game | Native by construction: every new Game owns a fresh city-name sequence. |
| 10 | Zero-maintenance buildings in Trade Advisor | Implemented. |
| 11 | Supported-unit scrollbar and total | Implemented. |
| 12 | 64-bit edit-box patch | Not applicable: rhYciv is a native 64-bit .NET application with native text controls. |
| 13 | No-CD patch | Not applicable: rhYciv has no CD check. |
| 14 | Multiplayer compatibility patch | Not applicable as a Windows patch; native rhYciv multiplayer is not implemented yet. |
| 15 | Simultaneous multiplayer moves | Pending native multiplayer. |
| 16 | Sort supported units | Implemented with workers first, then combat units by domain/defense/attack, then remaining types. |
| 17 | Transfer Engineer work and reset coworker | Implemented in terrain-improvement orders. |
| 18 | Continue Go To through legal ZOC movement | Implemented: Go To continues while the next step remains legal under Civ II ZOC rules. |
| 19 | Improved Wait/unit rotation | Implemented: nearby-unit rotation, waiting-list reset, and manual stack activation all clear the wait state correctly. |
| 20 | Reset legacy MoveIteration counter | Not applicable: rhYciv has no global legacy MoveIteration state. |
| 21 | City/advisor focus correction | Native replacement: modal Raylib dialogs own focus and return it to their parent. |
| 22 | Yellow celebrating cities plus food status | Implemented in the Attitude Advisor. |
| 23 | City-window attitude indication | Implemented: the Citizens caption follows the original neutral, disorder-red, and celebration-yellow states. |
| 24 | Radio-button hotkeys | Implemented using each option's first letter, in addition to arrows and Space. |
| 25 | Ctrl-hover quick info | Implemented for terrain, cities, and units. |
| 26 | Vertically resizable advisors/lists | Lists resize and scroll; interactive advisor-height resizing remains. |
| 27 | Larger advisor drag area | Native headers already provide full-width draggable areas. |
| 28 | Improved unit-list scrolling/navigation | Implemented in the shared list control with wheel, arrows, Home/End, and Page Up/Down. |
| 29 | Cancel/Escape in Change Production | Implemented by the modal production dialog. |
| 30 | Production shield cost and maintenance | Implemented, including scenario shield-row size. |
| 31 | Enhanced City Status advisor | Partially implemented: city total, additional resource data, wonder coloring, accurate costs, and changing what a city is producing from the advisor are present; sortable columns remain. |
| 32 | Mass specialist changes | Implemented: Shift+wheel and Shift+click change every specialist. Specialist types and their output now survive saves. |
| 33 | Suppress simple popups into map overlay | Pending a native notification overlay and configurable classification. |
| 34 | Correct proprietary portrait/throne DLLs | Not applicable and intentionally not distributed; rhYciv uses replacement FOSS art. |
| 35 | Runtime color correction | Implemented with a full-window shader and persistent brightness, saturation, and gamma controls. |
| 36 | Shift+right-click mass move | Implemented for eligible units of the active unit's type. |
| 37 | Foreign Minister gold display | Pending the completed native Foreign Minister screen. |
| 38 | Alternate Arrange Windows layouts | Native replacement: the single Raylib window responds to arbitrary window sizes and panel layouts. |
| 39 | Unit and city-trade PathLines | Implemented: Shift-hover previews unit movement; with a city selected it previews candidate road paths, and hovering that city draws its active trade routes. |
| 40 | Dye/copper demand bug fix | Native by construction: managed demand data is initialized deterministically, so the executable garbage-value bug cannot occur; broader formula parity is tracked separately. |
| 41 | Improved sentry shading | Implemented through full-color-shape grayscale shading rather than a silhouette. |
| 42 | U toggles all ground units in a stack | Implemented in the native unload command, including transport membership where applicable. |
| 43 | New-game zoom defaults to 1:1 | Implemented (`Zoom = 0`). |
| 44 | AI attitude initialization fix | Not susceptible to the native uninitialized-variable bug; broader diplomacy/attitude parity remains tracked separately. |

## Other upstream patches

- rhYciv uses dynamic collections and scenario rules rather than CIV2.EXE's fixed 2,048-unit, map, gold,
  population, and retirement-year memory patches.
- The render loop already yields through the graphics backend instead of busy-spinning the original executable.
- Multiplayer-only changes will be designed for rhYciv's eventual native networking layer instead of emulating
  legacy DirectPlay behavior.

## Remaining compatibility work

The incomplete applicable items are 14–15, 26, 31, 33, and 37.
They should remain visible in the GitHub UI-additions tracking issue until each row is verified in a running game.
