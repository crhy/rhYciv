**Twenty-one leaders across the table, and the rules checked against Civilization
II square by square.**

Diplomacy is no longer a menu with nobody behind it: every civilisation has a
face, and talking to one keeps them in view from the first word to the last. And
the comparison against the original went deep enough this time that one city now
agrees with Civ II on **every figure its city screen shows** — which meant finding
out that imported games had been losing every wonder in the world.

## Install

| Platform | Download |
|---|---|
| **Windows** (x64) | `rhYciv-0.2.0-win-x64.zip` — **extract the folder first**, then run `RaylibUI.exe` |
| **macOS** (Apple silicon) | `rhYciv-0.2.0-osx-arm64.zip` — unzip, drag `rhYciv.app` to Applications |
| **macOS** (Intel) | `rhYciv-0.2.0-osx-x64.zip` — same |
| **Linux** (x64) | `rhYciv-0.2.0-linux-x64.tar.gz` — extract, run `./RaylibUI` |
| **Linux** (Flatpak) | `rhYciv-0.2.0-x86_64.flatpak` |

**Extract before running.** Windows produces `Could not load file or assembly
'System.Runtime'` when the `.exe` is double-clicked while still inside the zip: it
unpacks that one file to a temporary folder and runs it with none of the nine
hundred beside it. Nothing is wrong with the download.

The builds are unsigned. **macOS**: `xattr -dr com.apple.quarantine
/Applications/rhYciv.app`. **Windows**: SmartScreen → *More info* → *Run anyway*.

## An audience, not a menu

Talking to another civilisation used to be one dialog that asked everything at
once. It is an audience now, and you stay in it: *What Will You Discuss?* leads to
a proposal or a gift, each of those to the actual terms, and backing out of a
matter returns you to the audience rather than ending it. You leave when you say
farewell, which is how Civ II's own negotiations behave.

The other leader is there the whole time. **Forty-two portraits**, one for each of
the twenty-one civilisations in each gender — Montezuma and the Aztec Empress,
Cunobelinus and Boudica, Tomyris, Gunnhild, Washington, Gandhi — each with their
own banner, emblem and words.

Their court's attitude and your own reputation are stated in Civ II's own
vocabulary rather than words of this game's invention, so somebody who knows the
original can read a relationship at a glance: nine ranks from Worshipful down to
Enraged.

## Every wonder in an imported game was missing

A Civ II save does not record wonders on its cities. It keeps a table near the
front of the file saying which city holds each of the twenty-eight, and this game
had always read that table and then dropped it on the floor. **No city in any
imported game held any wonder at all.** Cardiff lost the Great Library, Kells lost
Michelangelo's Chapel, and nothing anywhere said so.

It had a consequence nobody would have traced back to a save reader. Kells came
out of the save in **civil disorder**. It is not in disorder in Civ II, whose own
Happiness Analysis shows six content citizens with nothing red left; the
difference was Michelangelo's Chapel, which counts as a Cathedral in every city
its owner holds. What presented as a happiness bug was a missing wonder.

## Trade routes were being paid Civilization I's formula

Civ II's standing trade route brings `(T1 + T2 + 4) / 8` arrows a turn, halved
between two of your own cities, and distance does not enter it at all.

What this game had was `(distance + 10) × (Ta + Tb) / 24` — which is
Civilization **I**'s one-off payment for the caravan's delivery, charged again
every turn. Measured against Civ II on the same saved position, it paid four
arrows a turn where the original paid one.

The two games are constantly confused in forum threads, including in threads
whose titles say Civ2. That is exactly where this came from.

## Rules corrected against the original

- **A city square always produces at least one shield.** Civ II's Civilopedia:
  "if the city is built on Terrain that normally produces no Shields, one Shield
  is automatically added". A city founded on plain grassland produced nothing at
  all here until a citizen was put somewhere that did.
- **Whales are worth two shields and three trade**, not one and two. It was the
  only wrong figure in the whole terrain specials table, which has now been
  checked entry by entry against the Civilopedia's own pages.
- **The city square is irrigated, not mined**, on ground that permits either.
  The Civilopedia says "irrigated or mined, depending on the type of terrain"
  without saying which wins; a size-one city on hills in Civ II answers it.

## One city that agrees completely

Cardiff, A.D. 1700, seven citizens under a Republic: food produced, eaten and
surplus, shields, support, production, base trade, its trade route, corruption,
its list of improvements and wonders, its citizens and its lack of disorder — every
figure identical to Civ II's.

That is the first city to match on everything, and it matters because it means the
terrain reading, the city square rules, the government, the trade route formula,
the improvements and the happiness model are all correct *together*, on a real
position in a real game.

**What still disagrees, honestly:** corruption. Two cities the same distance from
the capital bracket Civ II's rate between a tenth and a sixteenth of trade, where
this game takes about a fifth. The distance term is the suspect and it will not be
changed without a source. The caravan delivery payment and waste have never been
compared at all.

All of it — what was measured, what agreed, what did not, every source and what
each was used to decide — is in `docs/CIV2-COMPARISON.md`.

## Also in this release

- Settlers can no longer be announced as though they were a completed warrior, and
  the quiet units stay quiet.
- The city screen's resource panel reads as four separate accounts rather than
  four rows of small pictures on grey, with the food store and shield box drawn as
  ramps.
- Fonts carry Latin-1 and Latin Extended-A, so accented leader and city names
  render instead of dropping to boxes.
- A headless screenshot harness can now walk a nested dialog and photograph each
  page, which is how the audience above was checked without a mouse.
