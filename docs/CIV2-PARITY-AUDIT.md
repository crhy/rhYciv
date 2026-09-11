# Civilization II gameplay parity audit

## Scope

This pass compared the engine's rules against Civilization II Multiplayer Gold as
documented in the published combat and rules references. It covers unit
abilities, combat resolution, the unit lifecycle, wonders, player actions, and
the AI opponents. Rendering and art are covered separately by
[4K rendering](4K-RENDERING.md) and the
[standalone asset audit](STANDALONE-ASSET-AUDIT.md).

Items marked **fixed** were corrected in the same pass that produced this
document. Everything else is an open divergence.

Out of scope by design, and not treated as gaps: the throne room, advisor and
high-council sequences, wonder movies, spaceships and spaceship victory, and
global warming.

## Ranked backlog

One ordered list, highest value-per-effort first. Each row's detail is in the
matching section below. Status: **done** (this pass or earlier), **in
progress** (this pass), **open**.

| # | Item | Area | Status |
|---|---|---|---|
| 1 | City-improvement production bonus (Factory, Mfg. Plant, power plants; unblocks Hoover Dam) | economy | done |
| 2 | Disband unit, disband-in-city for shields | unit action | done |
| 3 | Great Library / Darwin's Voyage turn hook (grant advances) | wonder | done |
| 4 | Leonardo's Workshop unit-upgrade pass | wonder | open |
| 5 | Diplomat/Spy actions: embassy, investigate, sabotage, steal tech, incite, bribe | unit action | open |
| 6 | Caravan/Freight actions: trade route, help build wonder | unit action | open |
| 7 | AI production choice (`City_Production_Complete`) | AI | open |
| 8 | AI expansion strategy: site evaluation, escorting, target count | AI | open |
| 9 | AI terrain improvement | AI | open |
| 10 | Paradrop | unit action | done |
| 11 | Airlift | unit action | open |
| 12 | Amphibious-assault enforcement (marine-only ship attack) | rules | done |
| 13 | Submarine visibility | rules | open |
| 14 | Nuclear strike (area effect, fallout) | rules | open |
| 15 | Revolution and anarchy | government | open |
| 16 | Diplomacy: contact, negotiation, treaty state, reputation | diplomacy | open |
| 17 | Senate constraint under Republic/Democracy | government | open |
| 18 | AI difficulty scaling | AI | open |
| 19 | Marco Polo's Embassy / United Nations / Eiffel Tower (needs diplomacy) | wonder | open, blocked by #16 |
| 20 | Statue of Liberty (needs revolution) | wonder | open, blocked by #15 |
| 21 | Manhattan Project gate (needs nuclear weapons) | wonder | open, blocked by #14 |
| 22 | AI Lua per-order/per-move `print` spam | AI perf | open |

Items 1, 2 and 3 were implemented across the 2026-09-01 passes; see their
sections below for what landed and what's still open within them. Items 10 and
12 landed in the 2026-09-04 Civilopedia-parity pass, together with the combat
arithmetic fixes recorded as C1-C4 in the deep audit. Item 4 (Leonardo's
Workshop unit-upgrade pass) is next.

## Civilopedia as the parity contract

`docs/CIVILOPEDIA-TEXT.md` is now written and shipped, so the game states in
plain words what each unit, building, terrain type and government does. That
makes it the player-facing specification, and any divergence is no longer an
internal inconsistency but a broken promise the player can read. The
2026-09-04 pass worked through it and fixed everything it named that was cheap
to fix; what it names and the engine still does not do is listed here.

| Civilopedia promise | Engine | Where |
|---|---|---|
| City Walls "multiply the defense of units inside it" | fixed 2026-09-04 | `UnitExtensions.DefenseFactor` |
| Terrain defence values shown per terrain entry | fixed 2026-09-04 | `Tile.Defense` |
| Pikemen "enhanced defense against mounted units" | fixed 2026-09-04 | `UnitExtensions.IsMountedAttacker` |
| Paratroopers "can make paradrops" | fixed 2026-09-04 | `ParadropFunctions` |
| Marines "can attack directly from ships" | fixed 2026-09-04 | `MovementFunctions.AttackAtTile` |
| Barracks restore a damaged unit "much more quickly" | fixed 2026-09-04 | `Game.Actions.HealRestingUnit` |
| Fanatics "free support under Fundamentalism" | already faithful | `RulesParser`, `CityExtensions` |
| Trireme "must remain near land" | already faithful | `Game.Actions.ResolveShipsLostAtSea` |
| Despotism and Anarchy lose a point on any 3+ tile | already faithful | `TileResourceExtensions` |
| Fundamentalism suppresses unhappiness, costs half research, supports ten units | already faithful | `CityHappiness`, `RulesParser` |
| Alpine Troops "treat all terrain as roads" | already faithful | `MovementFunctions` |
| Howitzer "attacks ignore City Walls" | already faithful | `UnitExtensions.DefenseFactor` |
| AEGIS "enhanced defense against air units" | already faithful | `UnitExtensions.DefenseFactor` |
| Diplomat and Spy missions: embassy, investigate, sabotage, steal, incite, bribe | **absent** | backlog #5, issue #20 |
| Caravan and Freight: trade routes, help build wonder | **absent** | backlog #6 |
| Submarine stealth - invisible to units that cannot spot it | **absent** | backlog #13 |
| Nuclear "devastate a target area ... leave serious pollution" | **absent** | backlog #14 |
| Leonardo's Workshop upgrades obsolete units | **absent** | backlog #4 |
| Marco Polo's Embassy, United Nations, Eiffel Tower | **absent**, needs diplomacy | backlog #19 |
| Statue of Liberty, and government change generally | **absent**, needs revolution | backlog #15, #20 |
| Airlift between cities with Airports | **absent** | backlog #11 |
| Capitalization converts shields to gold | **absent** | issue #28 |

The Civilopedia text hedges on every one of these - each says the behaviour is
"intended" - so no entry currently claims something the engine contradicts
outright. They are promises not yet kept rather than statements that are false.

## Rules that were already faithful

These were checked against the reference and need no work:

- The 14-bit unit flag field parses correctly, and every flag lands on the unit
  the standard ruleset intends.
- Firepower modifiers: shore bombardment reduces both sides to 1, a ship caught
  on a land square has its firepower reduced to 1 while the attacker's doubles,
  and a helicopter attacked by a fighter is reduced to 1.
- Defence multipliers for veteran (x1.5), fortified (x1.5), fortress (x2), city
  walls (x3), SAM batteries, SDI, coastal fortress, and terrain. Walls, fortress
  and fortified are one of the three in that order of precedence rather than a
  product, walls answer land attacks only, and a river adds half a step to the
  terrain adjustment rather than multiplying it. Nothing is gained by standing in
  a city that has no walls, and city size does not affect defence.
- Killstack, with the correct exceptions for city, fortress and airbase tiles.
- Post-combat movement loss proportional to hit points lost, with the sea-unit
  minimum.
- Zones of control, including the flag and the automatic exemption for sea and
  air units, in both movement and pathfinding.
- Happiness: martial law, Temple/Colosseum/Cathedral, specialists, and the
  happiness wonders — Hanging Gardens, Michelangelo's Chapel, Oracle, Women's
  Suffrage, Cure for Cancer, Shakespeare's Theatre, J.S. Bach's Cathedral.
- Corruption, pollution accumulation, and city growth/starvation.

## Bundled standalone data

Three faults in the bundled ruleset made the game close to unplayable, none of
them in the engine.

**No dialogs.** `FOSSart/Standalone/Game.txt` held only its metadata header.
Every dialog in the game is read from that file, and `ShowPopup` silently does
nothing when a dialog is absent, so pressing B on a Settler opened no naming
prompt and founded no city. The same silence covered research selection, go-to,
find city, pillage, selling buildings, save and quit confirmation, the options
screens, every error message and all nine city notifications. Forty-five
definitions have been written.

**Six terrain overlays were the same placeholder.** `build_terrain1` wrote six
tinted copies of the fortress icon into the sheet column the renderer reads as
irrigation, farmland, mine, pollution, the grassland shield and the goody hut.
A red stone wall therefore appeared on every shielded grassland square. Each now
has its own overlay, drawn in the generator.

**City names parsed as a single list.** `CITY.txt` separated its sections with
`@STOP` and no blank line, but the reader ends a section on a blank line, so the
whole file resolved to one 714-entry `AMERICANS` list. Every other tribe fell
through to an `EXTRA` key that no longer existed, and the lookup indexed the
dictionary directly — so any non-American civilisation, including every AI,
threw `KeyNotFoundException` on founding its first city. Sections are now
separated correctly and the lookup no longer throws.

**Mostly fixed in a later pass (2026-09-01):** painted base diamonds now feed
every TERRAIN1 row, and painted special-resource cutouts render for the desert,
plains, grassland, hills-2, tundra, glacier-2, swamp-1, jungle, and ocean
slots. Still the procedural coloured circle: coal, gold, iron, ivory, and the
swamp-2 slot. Full detail in [TEXTURE-GAP.md](TEXTURE-GAP.md). The orphaned
`FOSSart/Other/*.jpg` reference paintings this section originally pointed at are
still wired to nothing; the new art was generated fresh from `~/rhYcivtextures`
instead. Road and railroad tiles remain the generator-drawn vector rosettes —
a painted road-overlay set was still being authored in `~/rhYcivtextures/roads`
at the time of this pass and was left alone.

## Combat and unit abilities

**Fixed in this pass:**

- Pikemen-style x1.5 defence against mounted attackers. The flag was parsed and
  exposed on the model but never read. Civ II has no "is a horse" flag; a
  mounted attacker is identified as a land unit with two movement points, one
  hit point and one firepower, which selects exactly Horsemen through Cavalry in
  the standard rules and still behaves sensibly for modified rulesets.
- AEGIS-style defence: x3 against aircraft, x5 against missiles. Also parsed but
  never read.
- Cruise Missiles and Nuclear Missiles are expended by their own attack. The
  "destroyed after attacking" flag had no effect, so missiles survived combat.
- Veteran promotion. There was none; a unit surviving a battle now has the
  documented 50% chance of promotion.
- Air fuel range. `MovementFunctions` carried a bare `// TODO: Air unit out of
  fuel check`, so fighters and bombers could stay airborne indefinitely. Air
  units now count consecutive turns away from a city, airbase or carrier and
  crash when they reach their range.
- Ships that must stay near land. Triremes had no risk of loss at sea. The
  documented odds now apply — 1-in-2, improving to 1-in-4 with Seafaring and
  1-in-8 with Navigation, and nil while the Lighthouse stands. Civ II's bug where
  a trireme in a one-tile-island city drowns is deliberately not reproduced.
- The helicopter firepower penalty keyed on the attacker being an air unit while
  the matching defence penalty keyed on the attacker being able to engage air
  units. Both now use the latter.

**Open:**

- Paradrops. The flag is parsed, the menu carries a Paradrop entry, and no
  command implements it, so Paratroopers are ordinary infantry.
- Airlift. Menu entry present, no command, and the Airport improvement grants
  only veteran air units.
- Amphibious assault is referenced by the AI scripting layer but is not enforced
  in movement, so the marine restriction on attacking from a ship is not applied.
- Submarine visibility. Submarines are never hidden from other players; the
  "spot submarines" property is wired to the fighter flag and is read nowhere.
- Nuclear attack. A Nuclear Missile resolves as ordinary combat. There is no
  strike, no area destruction, no fallout.
- **Fixed in a later pass (2026-09-01):** Disband. A `DisbandOrder` (Shift+D) now
  removes the active unit anywhere on the map; the existing city-window disband
  path and the new order share one `CityActions.DisbandUnit`/
  `ApplyDisbandProductionCredit` implementation, crediting half the unit's
  shield cost to the city it is standing in or homed to.

## Non-combat unit actions

None of the following exist in any form:

- **Diplomat and Spy actions** — establish embassy, investigate city, sabotage,
  steal technology, incite revolt, bribe unit. `MinBribe` is parsed from the
  rules and read by nothing. Both unit types are currently unable to do anything
  a Warrior cannot.
- **Caravan and Freight actions** — establish trade route, help build wonder.
  Trade-route fields exist in the save format and in the classic-save reader, but
  no gameplay path creates or uses one.

## Wonders

The happiness wonders listed above were already implemented. Everything else was
cosmetic: built, occupying a city slot, costing shields, and doing nothing.

**Obsolescence, fixed in this pass.** The bundled ruleset's `@ENDWONDER` block was
28 lines of `nil`, so no wonder ever expired — including the happiness wonders
that were otherwise working. The block now carries Civ II's obsoleting advances:
Hanging Gardens/Railroad, Colossus/Flight, Lighthouse/Magnetism, Great
Library/Electricity, Oracle/Theology, Great Wall/Metallurgy, Sun Tzu/Mobile
Warfare, King Richard's/Industrialization, Marco Polo/Communism, and
Leonardo's/Automobile. The rules parser now accepts a trailing `;` comment in that
section, as it already did for sounds, so each line can name its wonder.

**Effects implemented in this pass:**

- Pyramids — a granary in every city its owner holds.
- Colossus — an extra trade arrow on each worked tile of its city that already
  produces trade.
- Lighthouse — ships lost at sea prevented, and one extra movement point for
  every sea unit.
- Great Wall — free city walls in every city its owner holds, and double attack
  strength against barbarians.
- Sun Tzu's War Academy — every ground unit its owner builds is a veteran.
- King Richard's Crusade — an extra shield on each worked tile of its city.
- Copernicus' Observatory — half again the science in its city.
- Magellan's Expedition — two extra movement points for every sea unit.
- Isaac Newton's College — double science in its city.
- A. Smith's Trading Co. — pays the upkeep of every building that costs one gold.
- SETI Program — a research lab in every city.

**Fixed in a later pass (2026-09-01):**

- Great Library — `Game.ResolveGreatLibrary` runs at the start of the owner's
  turn, before research is resolved, and calls `GiveAdvance` for every advance
  that at least two other non-barbarian civilisations already know. It stops when
  Electricity obsoletes the wonder, which `FindActiveWonder` already handles. The
  qualifying-advance set is `WonderFunctions.GreatLibraryAdvances`, unit-tested in
  `WonderFunctionsTests`.
- Darwin's Voyage — `GameTurn.GrantWonderCompletionAdvances` fires once, at the
  point of construction, and grants the owner two advances taken from
  `CalculateAvailableResearch` (highest AI value first, recomputed between the
  two so a freshly unlocked advance is eligible). It is deliberately resolved at
  build time rather than from a per-turn hook because it is a one-off.

**Still cosmetic**, and why:

- Leonardo's Workshop needs a unit-upgrade pass.
- Hoover Dam counts as a hydro plant, but power plants have no effect yet — see
  below.
- Marco Polo's Embassy, United Nations and the Eiffel Tower all grant embassies
  or reputation, which requires diplomacy to exist.
- The Statue of Liberty removes the anarchy between governments, which requires
  revolution to exist.
- The Manhattan Project gates nuclear weapons, which are not implemented.

## City improvements

**Fixed in a later pass (2026-09-01):** no building provided a shield bonus. The
`Effects` enum had multipliers for tax, luxury and science but none for
production, and `CalculateOutput` applied none, so the Factory, Manufacturing
Plant, Power Plant, Hydro Plant, Nuclear Plant and Solar Plant contributed
nothing and were pure loss. A `ProductionMultiplier` effect now exists,
following the exact pattern already used for the other three multipliers
(percentage points summed across improvements and multiplier-style wonders,
applied as `(100 + percent) / 100`): Factory +50%, Mfg. Plant/Power
Plant/Hydro Plant/Nuclear Plant/Solar Plant +25% each, wired in
`Engine/Scripts/improvements.lua`. Hoover Dam now grants every city its owner
holds the Hydro Plant bonus, the same way SETI Program grants a research lab.

Still open: Civ II gates Mfg. Plant and the power plants behind having a
Factory, and treats the four power-plant types as mutually exclusive per city;
neither constraint is enforced here, so they simply stack. The exact Civ II
percentages (and whether the bonus applies before or after corruption/waste)
are reconstructed from general knowledge of the ruleset, not re-verified
against a reference implementation.

## Government and diplomacy

- **No revolution.** Governments affect city output, unit support and tax limits,
  but a civilisation keeps its starting government forever. The Kingdom menu's
  REVOLUTION entry has no command behind it, and there is no anarchy period.
- **No diplomacy.** Treaty state — contact, cease-fire, peace, alliance,
  vendetta, embassy, war — is read from classic saves and never used. There is
  no contact, no negotiation, no declaration of war, and no reputation. Civs are
  permanently hostile to each other.
- The Senate, which constrains aggression under Republic and Democracy, does not
  exist.

## AI opponents

`Engine/Scripts/default.ai.lua` is 153 lines and drives every non-barbarian
opponent. Against Civ II's AI:

- **No production logic.** The script registers no `City_Production_Complete`
  handler, so every AI city falls through to `ProductionPossibilities.AutoNext`.
  The AI does not choose what to build.
- **No expansion strategy.** Settlers check fertility of the current tile and
  otherwise wander. There is no site evaluation, no target selection, no
  escorting, and no sense of how many cities to aim for.
- **No terrain improvement.** The settler branch is a `--TODO terrain
  improvements` comment.
- **Research** picks the highest AI-value technology biased toward earlier
  epochs. There are no technology goals and no reaction to the game state.
- **Military behaviour** is a per-unit priority sort: attack if a move is an
  attack, otherwise drift toward the nearest enemy within 12 tiles. No stacks, no
  operational objectives, no defence of threatened cities beyond a garrison
  count, no naval transport planning.
- **Difficulty does not scale AI behaviour.** `AiPlayer` receives a difficulty
  level and never reads it.
- The script `print`s on every unit order and every unit move. In a late-game
  turn with hundreds of AI units this is a meaningful cost on top of being noise.

The C# fallback in `AiPlayer.GetFallbackAction` is a reasonable safety net —
attack an adjacent enemy, settle, otherwise do nothing — but it is a safety net,
not an opponent.

## Suggested order of work

Superseded by the [ranked backlog](#ranked-backlog) at the top of this document,
which tracks status per item as work lands.
