# Civilization II parity — deep audit

A formula-level read of the engine and the bundled `RULES.txt` against
Civilization II Multiplayer Gold, system by system. It complements the ranked
backlog in [CIV2-PARITY-AUDIT.md](CIV2-PARITY-AUDIT.md), which stays the source of
truth for prioritisation; this file is the evidence behind it plus new findings.

Read for this pass: `Civ2engine`, `CityExtensions`, `CityHappiness`,
`CityResourcesExtensions`, `TileResourceExtensions`, `GameTurn`,
`MovementFunctions` (movement and combat), `UnitExtensions` (attack/defence
factors), `AdvanceFunctions`, `RulesParser`, `Tile.cs`, plus
`FOSSart/Standalone/RULES.txt`, `improvements.lua`, `advances.lua`.

Not covered: playtested balance, AI strength, rendering, save/load fidelity.
Anything that only shows up over many turns will have been missed. Line numbers
are approximate to `work/great-library-stone-ui` and will drift.

Severity: **major** materially changes play · **minor** wrong value or edge
case · **missing** Civ II mechanic absent · **faithful** matches Civ II.

## Summary

| System | Core resolution | Major | Minor | Missing |
|---|---|--:|--:|--:|
| Combat | diverges | 3 | 3 | 1 |
| Movement & ZOC | faithful | 0 | 2 | 0 |
| City economy | diverges | 2 | 3 | 0 |
| City growth | diverges | 1 | 1 | 0 |
| Happiness | faithful | 0 | 3 | 0 |
| Government | diverges | 1 | 2 | 3 |
| Technology | diverges | 3 | 1 | 0 |
| Terrain & buildings | diverges | 1 | 3 | 0 |
| Production | diverges | 2 | 1 | 0 |
| Wonders | faithful | 0 | 0 | 6 |
| Diplomacy | absent | 0 | 0 | 1 |
| Units | faithful | 0 | 1 | 0 |
| Victory & loss | diverges | 1 | 0 | 0 |

### Fixed since this audit was written

C1 through C4 were all corrected in the 2026-09-04 Civilopedia-parity pass, and
each carries a regression test in `RhyCiv.Tests/Units/UnitExtensionsTests.cs`. That
pass also found one divergence this audit missed: **City Walls added their effect
value rather than multiplying by it**, so the shipped 200 gave a garrison a flat
+2 defence instead of ×3. It is fixed in the same place as C3. The summary table
above has not been re-scored.

### Highest-impact findings not already in the ranked backlog

1. **City growth is uncapped** (CG1) — no Aqueduct/Sewer limit; cities pass
   size 8 and 12 freely. Feeds the "too happy / too big" reports.
2. **Terrain defence loses its half-steps** (C2) — integer division drops
   Forest/Jungle/Swamp from ×1.5 to ×1 and erases the river defence bonus.
3. **Per-round combat odds are not `A/(A+D)`** (C1) — the substitute curve
   over-rewards the stronger unit.
4. ~~**Walls and fortification do not stack** (C3)~~ — withdrawn 2026-09-11: they
   do not stack in Civ II either, and the "fix" was the fault. See C3.
5. **No production-change penalty and no rush-buy** (P1, P2) — two absent core
   loops.
6. **All advances share AI value 4** (T3) — AI tech choice is arbitrary within
   an epoch; compounds the key-civ index bug (T1).
7. **Fundamentalism pays no corruption** (CE2) — modelled like Democracy;
   Civ II Fundamentalism has Monarchy-level corruption.

---

## 1. Combat

Every named modifier from the reference is present and mostly correct. What is
off is the arithmetic that combines them.

### C1 · ~~major~~ fixed 2026-09-04 · per-round hit probability is not `A / (A + D)`

`MovementFunctions.cs:400`. The round is decided by
`probAttackerWins = (A*8 - 1) / (2*D*8)` (mirrored when the attacker is
stronger). At A=D it is ≈ 0.5, but at A2 vs D1 it returns ≈ 0.72.

Civ II rolls `rand(A + D)` each round; the attacker wins the round with
probability exactly `A/(A+D)` — 0.67 for A2 vs D1. The substitute biases every
unequal fight toward the favourite.

### C2 · ~~major~~ fixed 2026-09-04 · terrain defence is integer-divided

`Tile.cs:153` — `Defense => (River ? EffectiveTerrain.Defense + 1 :
EffectiveTerrain.Defense) / 2`, an `int`. Forest / Jungle / Swamp carry
rules-value 3, so `3 / 2 = 1`. A river on grassland is `(2 + 1) / 2 = 1`.
`DefenseFactor` multiplies by this truncated integer.

Civ II: value 3 is a ×1.5 terrain bonus and a river adds +25% defence. Both
vanish here, so units in forest or on a river defend as if in the open.

Corrected again 2026-09-11: the river was being applied as a further ×1.25 on
top of the terrain multiplier. Civ II adds half a step to the terrain adjustment
before that total multiplies with anything else — "a hill square with a river
gives a x2.5 bonus, a (2 + 0.5) multiplier" — so the two agreed on hills, by
coincidence, and nowhere else. And the whole result was being rounded down to a
whole number at the end, which cost Warriors their fortification bonus outright.

### C3 · ~~major~~ withdrawn 2026-09-11 · Fortress / Fortified / City Walls do not stack, and never did

This entry was wrong, and acting on it made the game wrong. It read:

> Civ II: these stack multiplicatively — City Walls ×3 *and* fortified ×1.5 both
> apply.

They do not. Civ II's combat guide states that the fortification bonus is
"superceded by fortress improvement and city walls", and testing on the original
reported at CivFanatics found the game takes the walls or the fortress *even
where fortifying would have given the better number* — so it is an order of
precedence, not the best of the three. The original `max()` was closer to right
than what replaced it; a fortified garrison behind walls was defending at ×4.5
where Civ II gives ×3.

Corrected 2026-09-11: walls first, else a fortress, else fortified. Walls also
answer land attacks only, which was not being checked at all.

Sources: <https://civfanatics.com/civ2/strategy/combatguide/>,
<https://forums.civfanatics.com/threads/civilization-2-defense-modifier-calculations.683500/>

**There is no defence bonus for merely standing in a city, and city size does
not affect defence.** What a city gives a defender in Civ II is City Walls.
Checked at the same time because it is the natural next assumption, and it is
not a rule in this game.

### C4 · ~~minor~~ fixed 2026-09-04 · Pikemen bonus is ×1.5 and misses Dragoons / Cavalry

`UnitExtensions.cs:59-62, 192` — the "mounted attacker" test is
`2 moves AND 10 HP AND 1 firepower`. Dragoons and Cavalry have 20 HP, so Pikemen
get no bonus against them. The multiplier is hard-set to ×1.5; most references
say Pikemen double (×2) their defence against every mounted unit.

### C5 · minor · combat RNG is `new Random()` per attack

`MovementFunctions.cs:403` — a fresh wall-clock-seeded generator is created
inside `Attack()` and also drives veteran promotion. No run reproduces;
`ResolveShipsLostAtSea` uses a different generator. Blocks the deterministic
combat goal in issue #7.

### C6 · minor · walled cities skip the capture population loss

`MovementFunctions.cs:448-453` — `ShrinkCity` runs on the defender's death only
`if (!Walled)`. Civ II: every captured city drops one population (a size-1 city
is razed); City Walls change the defence multiplier, not the capture outcome.

### C7 · missing · nuclear strike, partisan spawn, barbarian ransom

A Nuclear Missile resolves as ordinary combat — no area destruction, no fallout
(parity item 14). Capturing a city spawns no Partisans though the `Partisan`
unit effect is parsed. Killing a barbarian leader pays no ransom.

### Faithful

Shore bombardment (both firepowers → 1), ship caught in port (attacker firepower
×2, ship → 1), helicopter vs fighter, reduced-strength attack below one move
point, killstack with the city / fortress / airbase exception, HP-proportional
post-combat movement loss with the sea-unit floor, 50% veteran promotion for the
survivor, AEGIS ×3 vs air / ×5 vs missiles, Cruise and Nuclear missiles expended
by their own attack, Great Wall ×2 vs barbarians, ignore-walls flag for
Cannon / Artillery / Howitzer / Bomber.

---

## 2. Movement & Zone of Control

Solid. Terrain costs, road / railroad networks and the ZOC rule all match.

### M1 · minor · river movement discount is diagonal-only

`MovementFunctions.cs:979-984` — `RiverMovement` applies only when
`|dx| == 1 AND |dy| == 1`. Civ II gives road-like movement between any two
adjacent river tiles. Depending on the coordinate convention this may be cutting
the orthogonal river steps.

### M2 · minor · "always move one tile" rule is gated on 1-move units

`MovementFunctions.cs:566-569` — the "spend everything to enter" allowance is
conditioned on `MaxMovePoints <= MovementMultiplier`. Civ II lets any unit with
at least one movement atom enter any adjacent tile, forfeiting the remainder.
*Verify — the turn-end check may already cover it.*

### Faithful

Base terrain move costs 1 / 2 / 3, road = 1/3 and railroad = free via paired
tile effects, Alpine Troops treating all terrain as road, ZOC (adjacent-enemy →
adjacent-enemy blocked unless the destination holds a friendly unit, a city, or
the target), same-domain-only ZOC with sea and air exempt.

---

## 3. City economy

Tile-level output is faithful. The city-level layer on top — corruption, waste,
rate caps — is a set of reconstructions.

### CE1 · major · corruption / waste use a forum-derived formula

`CityExtensions.cs:67-88` — `corruption = trade * min(32, distance) *
15/(4+gov) / 100`, waste analogous with `min(16, ...)` and `4 + gov*4`. Comment
cites an Apolyton archive thread. `//TODO: Trade route to capital`. Civ II ties
corruption to distance from the palace with per-government behaviour, halved by a
Courthouse, removed under Democracy, reduced by a trade route to the capital.
Issue #6.

### CE2 · major · Fundamentalism has zero corruption and waste

`RulesParser.cs:302-314` — `DefaultDistanceFromIndex` returns 0 for both
Democracy and Fundamentalism, and distance 0 short-circuits
`ComputeDistanceFactor` to no corruption / no waste. Civ II Fundamentalism has
corruption on the order of Monarchy. (Communism at distance 0 is defensible —
Civ II Communism does eliminate corruption.)

### CE3 · minor · celebration lifts the Despotism tile penalty

`CityExtensions.cs:396-400` — `GetOrganizationLevel` adds 1 during
We-Love-the-King, and level >= 1 removes the `lowOrganisation` -1 on tiles
yielding >= 3. Civ II's celebration bonus (rapture growth, +1 trade) is
Republic / Democracy only and never removes the Despotism / Monarchy tile
penalty.

### CE4 · minor · city-centre tile floors food only

`TileResourceExtensions.cs` — `if (tile.CityHere != null && food < 2) food += 1`;
no equivalent for shields or trade. Civ II guarantees at least 1/1/1 from the
centre tile.

### CE5 · minor · tax / luxury / science rate caps are mostly wrong

`RulesParser.cs:282-300` — Monarchy 80% (Civ II 70), Communism 70% (Civ II 80),
Fundamentalism 80% / 50% science (Civ II 60), Democracy 100% (Civ II 90).
Despotism 60 and Republic 80 match.

### Faithful

Despotism / Anarchy -1 on any tile yielding >= 3, Republic / Democracy +1 trade
on every trade-producing tile, river +1 trade, grassland shield-tile rule,
specialist yields (3 tax / 3 science / 2 luxury), summed-percent multiplier
stacking for Marketplace + Bank + Stock Exchange and Library + University +
Research Lab, settlers eating 2 food from Communism onward.

---

## 4. City growth

The food box and granary are right. The size ceiling is not there.

### CG1 · major · no Aqueduct / Sewer System size cap

`CityExtensions.cs:260` / `GameTurn.cs:62-66` — `GrowCity` is `Size += 1` with
no guard. `ToExceedCitySizeAqueductNeeded` (8) and `SewerNeeded` (12) are parsed
from `@COSMIC` and exposed to Lua (`CosmicScripts.cs:216`) but read by no C#
code. Civ II stalls a city at size 8 without an Aqueduct and size 12 without a
Sewer System. Issue #11; likely a contributor to issue #13.

### CG2 · minor · starvation warning isn't on every path

`GameTurn.cs:45-56` — a size-1 city that runs its food box negative is shrunk to
0 and removed; the `FoodShortage` notification only fires on the "will go
negative next turn" branch.

### Faithful

Food box `(size + 1) * RowsFoodBox`, granary keeps 50% on growth, Pyramids
acting as a granary in every city, worker auto-distribution on growth and shrink.

---

## 5. Happiness

The most complete system in the engine. Five ordered passes, correct martial
law, every Civ II happiness wonder wired. What remains is magnitude tuning.

### H1 · minor · Hanging Gardens grants +3 / +1

`CityHappiness.cs:210-216` — +3 happy in the wonder's own city, +1 in every
other. Civ II: +1 content in every city, plus 1 happy in the city that holds it.

### H2 · minor · Colosseum step keyed to Electronics

`CityHappiness.cs:150-153` — Colosseum makes 3 content, 4 with Electronics.
Civ II: the 3 → 4 step is Electricity.

### H3 · minor · empire-size unhappiness is a reconstruction

`CityHappiness.cs:126-138` — a `governmentFactor * riotFactor` expression,
Communism exempt, large-map term added. Magnitude unverified against Civ II's
table. If it under-shoots it plus CG1 explains "way too happy" in issue #13. The
base "content citizens by difficulty" term does check out:
`CitySizeUnhappyChieftain = 7` yields {6, 5, 4, 3, 2, 1} across Chieftain →
Deity, matching Civ II.

### Faithful

Five-pass order (base mood → luxuries → improvements → government → wonders),
martial law (<= 3 units, ×2 under Communism), Republic / Democracy field-unit
unhappiness (1 / 2 per unit, -1 with Police Station or Women's Suffrage),
Fundamentalism never unhappy, Temple 1 (+1 Mysticism, ×2 Oracle), Cathedral 3
(-1 Communism, +1 Theology), Michelangelo as a cathedral everywhere, Democracy
Courthouse / Palace → +1 happy, Shakespeare's Theatre, J.S. Bach's -2 same
continent, Cure for Cancer +1 everywhere, disorder when unhappy > happy,
We-Love-the-King conditions.

---

## 6. Government

The seven governments exist with the right output effects, but you cannot change
to them, and several per-government constants are off.

### G1 · major · no revolution or anarchy

Nothing in the engine or client changes a civ's government — no
`ChangeGovernment` exists. A civ keeps its start government forever; the Kingdom
→ Revolution menu entry has no command. Parity item 15. Statue of Liberty (skip
anarchy) is inert as a consequence.

### G2 · minor · Republic gets zero free unit support

`RulesParser.cs:248-253, 272` — level-2 governments get
`NumberOfFreeUnitsPerCity = 0`. Civ II: Republic supports a small number of
units free per city; only Democracy is truly zero.

### G3 · missing · Senate, tithe income, Communism spy bonus

No Senate constraint on Republic / Democracy aggression (needs diplomacy).
Fundamentalism's tithe income isn't modelled. Communism's diplomat / spy veteran
bonus isn't applied.

### Faithful

All seven governments present, level-0 (Anarchy / Despotism) tile penalty,
level-2 (Republic / Democracy) trade bonus, free-support constants for
Monarchy / Communism / Fundamentalism (3 / 3 / 10), the settler-food step at
Communism, the Fundamentalism science cap and loss.

---

## 7. Technology

The prerequisite tree is faithful. The economics around it — research cost, the
key civilisation, AI valuation — are not.

### T1 · major · key-civ science cost indexes by power rank

`AdvanceFunctions.cs:190` — `TotalAdvances(game, civ.PowerRank)` passes a 1..N
power *rank* straight into `AllCivilizations[...]` as if it were a civ id.
Issue #9.

### T2 · major · no difficulty-based research culling

`AdvanceFunctions.cs:230` — `//TODO: cull list based on difficulty`; the full
set of legal advances is always offered.

### T3 · major · every advance has AI value 4

`RULES.txt @CIVILIZE` — all 88 advances are written `4, 0`. The AI
"pick the highest-value tech" routine has nothing to sort on. Civ II varies AI
research value 2-6 per advance.

### T4 · minor · cost formula is self-described as unverified

`AdvanceFunctions.cs:185-200` — doc-comment: "I'm not sure if this formula is
correct I've just grab[b]ed it from [a forum thread]." `TechParadigm` (10)
matches; the tech-lead and map-size terms are approximations.

### Faithful

The prerequisite graph (spot-checked: Iron Working = Bronze Working + Warrior
Code, Feudalism = Warrior Code + Monarchy, Gunpowder = Invention + Iron Working),
epoch tagging, `GiveAdvance` effect application and production-list refresh, and
— with the current PR — the Great Library and Darwin's Voyage granting advances.

---

## 8. Terrain & buildings

Terrain yields, move costs and improvement costs match Civ II MGE closely. A
handful of buildings are declared but have no effect.

### TR1 · major · several buildings do nothing

`improvements.lua` — no effect attached to **Recycling Center** (Civ II:
industrial pollution / 3), **Police Station** (field / war unhappiness relief),
**Super Highways** (+50% trade on roaded tiles). Hydro and Nuclear Plant lack
their pollution reduction (Civ II halves industrial pollution). **Supermarket**
is modelled as "+1 food on irrigated tiles" rather than "+50% on farmland", and
farmland isn't gated on it.

### TR2 · minor · power-plant percentages and stacking

`improvements.lua` — Manufacturing Plant is +25% (Civ II +50%);
Power / Hydro / Nuclear / Solar Plant are +25% each and **stack**, with no
Factory prerequisite. Parity audit already notes the stacking.

### TR3 · minor · pollution-tech modifiers are off

`advances.lua` — Industrialization adds +2 to the population-pollution modifier
(Civ II: +1). Sanitation is given -1 population pollution, which is not a Civ II
effect.

### TR4 · minor · mountain mining and forest defence

`RULES.txt @TERRAIN` — Mountains mining is +1 shield (Civ II: +2 — verify).
Forest / Jungle / Swamp defence collapses to ×1 via C2.

### Faithful

Terrain food / shield / trade and move costs, irrigation and mining yields and
turn counts from `@TERRAIN`, special-resource tiles, road trade bonus via tile
effects, Harbor (+1 food per ocean tile), Offshore Platform (+1 shield per ocean
tile), improvement costs and upkeep (match Civ II MGE line for line), wonder
costs, and every `@ENDWONDER` obsolescence advance.

---

## 9. Production

Accumulation and completion are correct. Two Civ II loops around them are absent.

### P1 · major · no production-change penalty

`ShieldPenaltyTypeChange` (50) is parsed from `@COSMIC` and used nowhere.
`city.ItemInProduction = ...` is assigned directly in `GameTurn.cs` and
`AiPlayer.cs` with no adjustment to `ShieldsProgress`. Civ II wipes about half
the accumulated shields (once) when switching between the unit, building and
wonder categories.

### P2 · major · no gold rush-buy

There is no "hurry production" path anywhere in the engine or the Raylib client.
Civ II completes production with gold at an incremental cost (roughly `2x` or
`4x` the remaining shields plus a quadratic term).

### P3 · minor · disband-for-shields rounding to verify

`CityActions.cs` — the disband order credits half the unit's shield cost to its
standing / home city. Confirm the rounding and the standing-tile vs home-city
precedence against Civ II.

### Faithful

Shield box `Cost * RowsShieldBox`, building completion (a `Unique` improvement
sells prior copies), unit build, Sun Tzu and Barracks producing veterans, the
disband order and its city credit, one-wonder-per-world through the shared
improvement list.

---

## 10. Wonders

Covered in depth by [CIV2-PARITY-AUDIT.md](CIV2-PARITY-AUDIT.md). About seventeen
wonders have real effects; the current PR adds the Great Library and Darwin's
Voyage.

### W1 · missing · still cosmetic

**Leonardo's Workshop** — no unit-upgrade pass (parity item 4). **Marco Polo's
Embassy**, **United Nations**, **Eiffel Tower** — grant embassies / reputation,
need diplomacy. **Statue of Liberty** — removes anarchy, needs revolution.
**Manhattan Project** — gates nuclear weapons, which aren't implemented.
**Apollo Program** — spaceship, out of scope by design.

### Faithful

Pyramids, Hanging Gardens, Colossus, Lighthouse, Great Wall, Sun Tzu's War
Academy, King Richard's Crusade, Copernicus' Observatory, Magellan's Expedition,
Isaac Newton's College, A. Smith's Trading Co., SETI Program, Michelangelo's
Chapel, Shakespeare's Theatre, J.S. Bach's Cathedral, Oracle, Women's Suffrage,
Cure for Cancer, Hoover Dam — plus obsolescence timing for all of them.

---

## 11. Diplomacy

Not implemented at all.

### D1 · missing · no contact, treaties, war state or reputation

Treaty state (contact, cease-fire, peace, alliance, vendetta, embassy, war) is
read from classic saves and never used. There is no first contact, no
negotiation screen, no declaration of war, no reputation, no attitude tracking
that affects anything. All civilisations are permanently at war. Parity item 16
— and the blocker for four wonders (W1) and the Senate (G3).

---

## 12. Units

The stat block is faithful to Civ II MGE. A few unit *actions* are stubs.

### U1 · minor · paradrop, airlift, amphibious assault, submarine visibility

Paradrop and Airlift have menu entries and parsed flags but no command (parity
items 10-11). Amphibious assault — the marine-only restriction on attacking from
a ship — isn't enforced in movement. Submarines are never hidden from other
players. Diplomat / Spy actions against a city are also entirely absent
(issue #20, parity item 5).

### Faithful

Unit stat block — attack, defence, hit points, firepower, cost, moves, transport
capacity — spot-checked across ~15 units (Warriors, Phalanx, Legion, Musketeers,
Riflemen, Armor, Battleship, Nuclear, Howitzer), all matching Civ II MGE; the
14-bit flag field; fortify / sleep / go-to / disband orders; carrier air units;
trireme loss at sea with Seafaring / Navigation / Lighthouse mitigation; air
fuel range enforced at the start of each turn.

---

## 13. Victory & loss

The conquest-only, permanent-elimination scope is a deliberate product decision.
Within it, one thing is broken.

### V1 · major · the human is never eliminated

`Game.Actions.cs:88` — `ChooseNextCivilizationOnce` just `continue`s past a civ
whose `Alive` is false, and the restart branch below it is an empty comment.
Nothing sets `Alive = false` when a civ loses its last city and unit, and no
game-over fires for the local player. Losing everything silently hands the turn
to the AIs. Issue #16.

### By design, not gaps

Spaceship and spaceship victory, the throne room, animated advisors / high
council, wonder movies, and global warming are intentional omissions per
[CIV2-PARITY-AUDIT.md](CIV2-PARITY-AUDIT.md) and the product scope.
