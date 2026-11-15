---
name: rhyciv-superhighways-tile-trade
description: "Super Highways raise trade on a city's roaded worked tiles by 50%; the rule belongs in the tile-yield path, not in the city's totals, and ordinary Civ II play gets no Advanced Settings toggle."
metadata:
  node_type: memory
  pinned: false
  originSessionId: 01a1ddc2-ca6f-7562-8438-f6c296658280
  modified: 2026-11-15T05:51:32.000Z
---

# Super Highways trade is a tile effect, and it is ordinary Civ II play

Issue #28 listed buildings that are declared but do nothing. The data-only rows
were closed in `c3d3976e`; the owner's comment named what was left: "**Super
Highways** (+50% trade on road tiles) — there is no tile-trade effect in the
`Effects` enum; `TaxMultiplier`/`LuxMultiplier` are city-level. Needs a new effect
plus a hook in the tile-yield path."

The effect that was needed already existed and is not city-level:
`Model/Constants/CityTerrainEffect.cs` (`Terrain`, `Resource`, `Improvement`,
`Action` defaulting to `Add`, `Value`, `Level` defaulting to 0, with `Add`,
`AddExtra`, `Multiply`), attached to an improvement by
`CityImprovement.addTerrainEffect`, and read back by `GetFood`. What was missing
was the trade half of the read: `TileResourceExtensions.GetTrade` consulted the
terrain, the river's `RoadBonus`, the tile's own `EffectsList` and the
organisation rules, and never asked the working city anything. So the fix is one
scripted row plus the same pass `GetFood` already makes, shared as one helper
rather than copied — and `GetShields` stays city-effect-free, because Civ II gives
it no such rule.

Two things worth remembering about where the rule goes.

**Put tile rules in the yield path, not in the city's arithmetic.** Every
consumer of a square's trade reads `GetTrade` — `CityExtensions.CalculateOutput`
summing it into `city.TileTrade`, the city window's tile map, the auto-start's
figures. A rule that lived in the city's totals instead would make the worked
window and the totals disagree, which is exactly the class of bug #28 is full of.

**Super Highways gets no Advanced Settings checkbox.** The opt-in rule
([[rhyciv-advanced-settings-optin]]) covers rules beyond plain Civ II play.
Roaded trade is Civ II: the bundled Civilopedia already promises it — "Super
Highways increase trade from worked land tiles connected by roads or railroads,
with the intended classic effect providing 50 percent more trade from those
developed squares". A player who turns the game into Civ II should not have to
ask for it.

The number is a compatibility contract in two places: the improvement stays index
25 (`Engine/src/Enums/ImprovementType.cs`), and the value is written as 150 because
`Multiply` divides by 100 in the yield path — 150 is ×1.5, not +150%. Scripts name
the actions now (`civ.core.TerrainEffectActions.Multiply`) rather than writing a
bare `2`, so the row reads as words.

Open question left in the code for rhY's in-game check: the row asks for the
road at `Level = 1`, which in this game's level scheme is the railroad grade, while
the Civilopedia sentence above says roads *or* railroads. Whether a plain level-0
road should also raise trade is a measurement in the real game, not something to
guess at.
