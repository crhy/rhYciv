**A map worth looking at, and the bugs that 0.2.0 shipped.**

0.2.0 went out and was played hard for an evening, which is the best thing that
can happen to a release. This is what that found: a crash that killed any loaded
game the moment a city changed hands, goody huts that came back from the dead,
rivers that never joined up, and a sea painted almost black.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.2.1-win-x64.zip` — **extract the folder first**, then run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.2.1-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.2.1-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.2.1-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.2.1-x86_64.flatpak` |

**Extract before running.** Windows produces `Could not load file or assembly
'System.Runtime'` when the `.exe` is double-clicked while still inside the zip.
Nothing is wrong with the download.

The builds are unsigned. **macOS**: `xattr -dr com.apple.quarantine
/Applications/rhYciv.app`. **Windows**: SmartScreen → *More info* → *Run anyway*.

## The crash that ended loaded games

Capturing a city — by anyone, in any game loaded from a rhYciv save — threw a
NullReferenceException and took the game down. It was reported from a game at
turn 165 where barbarians took a city on their turn.

`JsonSaveObjects.Scenario` was declared `null!` and nothing ever assigned it, so
**every game loaded from one of this game's own saves carried a null scenario**.
Nothing reads it until a city changes hands, and then the game dies at that
moment. It hid because Civilization II's own reader *does* build one, so every
position imported from a `.SAV` was safe.

## The map

**The sea was a flat near-black fill.** Open water was not being drawn with the
ocean painting at all — it came from the coastline tileset's own all-water tile,
which measures (6, 24, 50) and varies by two or three levels across the whole
diamond. The generator had been tuned against a photograph of a fjord, where the
sea is nearly black right up to the rock: a fair reading of a coast, applied to
every water tile on the map. Deep water now takes its colour and its swell from
the painting, and measures (24, 76, 132) varying by nearly forty.

**The coast met the grass along a hard line.** The shoreline ramp ended on exactly
the grassland painting's average colour and none of its grain — the right green,
perfectly smooth, against a tile full of texture. It takes the painting now, on
the far side of the sand so the beach is untouched.

**Rivers never joined up**, and it was not the art. A river is drawn as one
picture per tile chosen by which of its four *edge-sharing* neighbours also carry
a river — but the generator walked rivers through the *eight*-neighbour set,
including the four tiles that touch only at a corner. Two tiles sharing a corner
cannot be joined by any picture. Across three test worlds, 15 of 45, 18 of 50 and
19 of 44 river tiles were orphaned stubs: generated in one adjacency, drawn in
another.

**And rivers now widen as they go.** Four gauges — a trickle, a stream, a river,
an estuary — chosen by how far each tile sits from the sea along its own
watercourse, which is recovered by walking the river inland from its mouth. Every
painting in the art folder contributes: the cross-section is shared so tiles still
meet, and the variation along the stroke comes from a different painting for each
spoke.

## Everything else that was reported

- **Goody huts came back when a game was loaded.** Where they are is computed from
  the map seed rather than stored, so loading a game put one back on every square
  that ever had one — including all the ones already entered.
- **A long press never armed the go-to.** `MouseDown` is raised on every frame the
  button is held, so the timer was reset sixty times a second and the press was
  never more than one frame old.
- **Zooming stopped the active unit blinking**, because a zoom about the pointer
  replaced the mode's own view with a plain static one that has no active unit.
- **A Diplomat destroyed an empty size-1 city instead of offering to buy it.**
  Whether a move was an attack or a move was decided on the units standing there,
  and an undefended city has none — so the Diplomat walked in and took it the way
  a warrior would.
- **Barbarians announced declarations of war**, which Civ II never does: there they
  are permanently at war with everyone and it is never stated. They are no longer
  a party to the diplomatic model at all.
- **Civilisations you had never met offered you cease-fires**, because declaring
  war set the contact flag directly instead of making contact properly, so two
  sides became acquainted without either being told.
- **Selling a building read like a refusal**, and quitting the game only offered
  the main menu.
- **One crash was being reported twice**, the second time blaming the graphics
  driver, because the managed handler wrote its report and left the session record
  open for the next launch to find.

## Multisampling is off by default

Every hard crash this game has been reported with happened with 4x multisampling
on, because until this release that was the only way it ran; the one long session
played without it ended cleanly, and the next session with it back on crashed
inside seven minutes. Two sessions prove nothing, and it is acted on because the
trade is so one-sided: this is a game of 2D sprites blitted axis-aligned, where
multisampling has almost nothing to antialias. `RHYCIV_MSAA=1` turns it back on.

**The remaining hard crash is not explained.** It leaves no managed exception,
which means a fault below .NET. See issue #132.

## Measuring a world against Civilization II's

`RHYCIV_REPORT_MAP` prints what a world is made of and `RHYCIV_AUTOSTART_SIZE`
generates at a named size, so a generated map and a Civ II save of the same
dimensions can be set beside each other. Twenty Civ II saves at 75×120 read
between 23.9% and 42.8% land, most of them between 24 and 31, with rivers on 2.3%
to 3.4% of it.

The rules comparison against Civ II continues in `docs/CIV2-COMPARISON.md`, which
records what has been measured, what agreed, what did not, and every source.
