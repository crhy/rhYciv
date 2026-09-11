**The hard crashes, and the first release checked against the original.**

Sessions had been ending in a crash since 0.1.6 — always with no managed
exception and nothing on standard error, which is the signature of something
outside .NET's reach. This release ends them, and it also opens Civilization II's
own saved games and compares the result against the game it is a re-implementation
of.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.1.10-win-x64.zip` — **extract the folder first**, then run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.1.10-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.1.10-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.1.10-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.1.10-x86_64.flatpak` |

**Extract before running.** A tester reported `Could not load file or assembly 'System.Runtime'`, which is what Windows produces when the `.exe` is double-clicked while it is still inside the zip: it unpacks that one file to a temporary folder and runs it with none of the nine hundred beside it. Nothing is wrong with the download. The archives now carry a note saying so.

The builds are unsigned. **macOS**: `xattr -dr com.apple.quarantine /Applications/rhYciv.app`. **Windows**: SmartScreen → *More info* → *Run anyway*.

## The hard crashes

The output capture added in 0.1.8 finally caught one, and it was not a fault —
it was arithmetic. raylib reports every texture it loads and unloads, so a
crashed session can simply be counted:

```
4,170 textures loaded
  179 unloaded
3,991 still held  ≈ 546 MB of video memory
```

Every one of them belonging to a window that had been closed. A raylib texture is
a GPU allocation with nothing to free it when the managed object is collected, so
anything that paints its own background has to be told when it is finished with.
Nothing told them. Eventually the driver refuses, and a driver refusing kills the
process outright.

Four places were painting without ever giving back: dialog panels (repainted on
every layout and dropped on close), button faces, the scrollbars every list
builds, and the city window's composed resource map.

| | windows opened | loaded | unloaded | **held** |
|---|---|---|---|---|
| before | 169 | 3,256 | 170 | **3,086** |
| after | 3,298 | 29,737 | 29,688 | **49** |

The 49 are shared art loaded once, and they do not grow.

## Checked against Civilization II

Civ II's saved games load here, which means the same position can be opened in
both games and the numbers set beside each other. That found three faults nothing
else would have.

**Every city in a loaded game had three trade routes it never had.** A city keeps
three route slots whether or not it uses any, and an unused one is zero in both
fields — which both readers turned into a route to whichever city came first in
the list. Cardiff's squares produce 3 trade; the phantom routes were worth 120
more.

**Every civilisation was under the wrong flag.** A save stores each civilisation's
tribe as a position in Civ II's own leaders table, which runs Romans, Babylonians,
Germans and on in no order but its own; this game's table is alphabetical. A
Celtic game reported its own player as Persian, its German rivals as Babylonian
and the English as Japanese. The cities kept their real names, which is what gave
it away.

**A city taken from you never changed colour on your map** — because losing it is
exactly what stops you being able to see it, and the map is only refreshed for
civilisations that can see a square. The one civilisation certain to have it on
their map was the one certain never to be told.

With those fixed, **Cardiff at 2500 BC and again at A.D. 1240** agrees with the
original on size, food produced and eaten, surplus, shields, support, production,
trade, corruption, tax and science — every number the city screen states.

## Two rules corrected

- **Bribing a unit does not cost the Diplomat its life.** Civ II's mission table
  gives Bribe Unit as "Mission Success" for a Diplomat and a Spy alike; every
  other mission kills a Diplomat. It was being spent as though it had incited a
  revolt, so turning one warrior cost the agent as well as the gold. A message
  says what happened now, which nothing did before.
- **Settlers and Engineers cannot fortify** — "the only units incapable of
  fortifying". They were, and took the same half-again defence as a Phalanx on
  top of the twenty hit points a Settlers unit already has. That is what let one
  settler in a city kill two attacking horsemen. They keep the fortress, which
  they are the ones who build.

## The game stops interrupting you

Civ II announces buildings, and announces units that cannot fight — a Settlers or
a Diplomat, the ones that want orders the moment they appear. It says nothing at
all about a warrior or a horseman. This had it the other way round: combat units
were announced whatever the options said, and the quiet ones could be switched
off. A city in production interrupted the game every few turns.

## The city screen

Each line of the City Resources panel has a band of its own — green for food,
amber for trade, a deeper orange for what the rates take, blue for shields — lit
from the top and closed with a darker line, so the block reads as four separate
accounts rather than four rows of small pictures on grey. The food store and the
shield box are ramps rather than flat colour; at that size a flat fill reads as a
hole cut in the window.
