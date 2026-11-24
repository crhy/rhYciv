---
name: rhyciv-city-exit-zoc
description: "On rhYciv, a unit standing in a city may leave it for any empty tile; zones of control stop arrivals, not departures from a city (#159)."
metadata:
  type: project
---

# Leaving a city is not held by a zone of control

Issue crhy/rhYciv#159: "When leaving a city, a unit should be allowed movement
to ANY empty tile. Regardless of enemy proximity." Civ II's zone rule is a rule
about the square a unit arrives on. A unit that has overwintered in a city, or
sits in one it captured, walks out of it the way it walks out of open ground:
the nearest enemy's zone does not hold it in place.

The rule as implemented, in one predicate —
`MovementFunctions.ExemptFromZoneOfControl(Tile from)` — read as "the square
being left holds a city". Every call site goes through it, so the three places
that repeat the zone rule cannot drift apart again:

- `MovementFunctions.Moveto` — the move the player commits, which used to answer
  with `BlockedReason.Zoc`.
- `MovementFunctions.GetPossibleMoves` — the move list the interface offers, so
  the square is not offered and then refused.
- `Path.GetInitialCandidates` and `Path.CanContinueThroughZoneOfControl` — Go
  To, which repeats the rule for the first step and for keeping a walk going.

What did **not** change, deliberately: the destination side of the rule. A
destination is still let through when it holds a friendly unit, a city, or the
target, and is still otherwise refused between two enemy-adjacent squares. No
fortress, port, road or airbase exemption was invented — Civ II's city rule is
the only one being implemented, and the others are unverified. The domain rule
in `IsNextToEnemy` is untouched: a warship next door has never held a legionnaire
in place, and still does not.

**Why there is no Advanced Settings checkbox** (see [[rhyciv-advanced-settings-optin]]):
that rule covers anything that is not part of ordinary Civ II play. A city exit
*is* ordinary Civ II play, so it is on for everyone, like the ZOC rule itself.

Still open, and not touched here:

- Whether a fortress, port or road tile grants the same exit. Civ II is said to,
  but nothing in `docs/CIV2-RULES-RESEARCH.md` settles it; do not guess.
- Audit items M1 (river movement discount applied diagonally only) and M2 (the
  "always move one tile" allowance gated on 1-move units) in
  `docs/CIV2-PARITY-DEEP-AUDIT.md` remain open.
- The in-game check is rhY's: a unit in a city, an enemy stack next door, and
  the walk out. The tests below cover the rule offline only.
