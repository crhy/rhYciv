**The map, under the hands.**

A short release on top of 0.1.8, all of it about how the map behaves while you
are working it: the zoom, the shading at the edge of what you have explored, and
one marker that was drawn several times the size it meant.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.1.9-win-x64.zip` — unzip, run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.1.9-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.1.9-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.1.9-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.1.9-x86_64.flatpak` |

Nothing else is needed. No commercial Civilization II installation, no runtime to install — each download carries its own .NET runtime and the complete art set.

### The builds are unsigned — please read this before reporting a launch failure

They are not code-signed, because signing certificates cost money this project does not have yet. Both desktop platforms will try to stop you:

**macOS** will say the app "is damaged and can't be opened" or is from an unidentified developer. It is not damaged; that is the quarantine flag on anything downloaded unsigned. Clear it:

```
xattr -dr com.apple.quarantine /Applications/rhYciv.app
```

**Windows** will show a SmartScreen warning. Choose *More info* → *Run anyway*.

**Linux Flatpak**:

```
flatpak install --user ./rhYciv-0.1.9-x86_64.flatpak
flatpak run io.github.crhy.rhYciv
```

## Zoom moves in even steps

The scale was linear in `(8 + zoom) / 8`. That sounds harmless and is not: it
made one step of the wheel do wildly different things depending on where you
already were.

| zoom | scale | one step of the wheel |
|---|---|---|
| −7 → −6 | 0.125 → 0.25 | **×2.0** |
| 0 → 1 | 1.0 → 1.125 | ×1.13 |
| 31 → 32 | 4.875 → 5.0 | **×1.026** |

A factor of nearly forty between the largest step and the smallest. Zoomed out
the map leapt about; zoomed in the wheel appeared to do nothing at all.

The scale is geometric now: **every step is nine per cent**, and eight of them
double it. The ends of the range moved to −24…19 so the reachable scale stays
about what it was — an eighth of normal up to five times it.

## Zoom goes towards the pointer

Ctrl and the wheel changed the zoom and left the view centred on the active
unit, so the square you were aiming at slid away from the cursor — worse the
further it was from the unit.

The point of the map under the cursor is now put back under the cursor after the
step. Not "centre the square under the cursor", which is a different thing and
looks worse: that throws the square to the middle of the screen on the first
click and swings the rest of the map around it.

From zoom 4 upwards the square under the pointer is identical at every step of a
sweep to the maximum. Below that it can still drift by up to a square a step,
where the tiles are small enough that a pixel of rounding is a sizeable fraction
of one.

## The weird diamond shadows

Every square on the frontier of the explored map is given a softening where it
meets unexplored ground. The mask for it is a **32×16 checkerboard**, drawn when
Civ II's squares were 64×32 and a chequer of alternating pixels read as a shade.

Terrain composes at several times that size now, so the same mask was stretched
until each of its pixels was a block several across — and what was a shade became
a coarse dark patch covering a quarter of the square. On every square bordering
the unknown, which is why the shadows traced the edge of the black. The city
window showed them for the same reason: the squares at the edge of a city's
radius are frontier squares too.

Above classic resolution the softening is left out. It was not doing anything the
eye reads as softening there, and the edge of the known world is a clean
isometric boundary without it.

## The grassland shield is a marker again

It marks a square as yielding an extra shield. At 0.44 of the tile it covered
most of the square; taken to 0.22 it still read as an object lying in the field —
a stone medallion the size of a manhole cover, dropped in the grass and competing
with whatever was standing there. It is an eighth of the tile now, which reads as
a token on the ground, which is what it is.
