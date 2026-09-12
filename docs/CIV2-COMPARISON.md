# Comparing rhYciv against Civilization II directly

This is the record of checking this game against the one it re-implements, by
opening the same saved position in both and setting the numbers beside each
other. It is kept current as the work goes on: what was compared, what agreed,
what did not, and what has been fixed as a result.

It exists because reasoning about the rules was not enough. Two of the faults
below had been in the game for months and were invisible to a test suite of four
hundred tests, because every test agreed with the code about what the answer
should be.

---

## How to do it

Civ II's own `.SAV` files load here — `Engine/src/LegacySaves/Read.ClassicSav.cs`
has read them since before the defork — so the same position can be opened in
both games.

**Get this game's numbers.** From the directory the binaries are in (this
matters — the Lua scripts that give improvements their effects are resolved
relative to the working directory, and running from the repository root silently
loads none of them, so the Palace has no `Capital` effect and no city is ever the
capital):

```
cd RaylibUI/bin/Debug/net9.0
RHYCIV_AUTOSTART=1 RHYCIV_REPORT=1 \
RHYCIV_AUTOSTART_LOAD=/path/to/civ2.sav \
dotnet RaylibUI.dll
```

That prints a tab-separated row per city in the world — size, food produced and
eaten, surplus, stored, box size, shields, support, waste, production, trade,
corruption, tax, science, worked squares, specialists, tile trade, route trade,
the happiness split and the improvements — and quits.

`RHYCIV_REPORT_CITY=NAME[,NAME...]` adds a listing of each named city's worked
squares with the terrain, the special, what each yields, and — where two of the
listed cities have both claimed a square — which other city has it. A
disagreement about a city's food or trade always comes down to which squares it
is working and what they are worth, and a city working land that looks worse than
what lies beside it is usually a square a neighbour has already taken.

**Get Civ II's numbers.** Open the same save and screenshot the city screen. Civ
II states its own figures there, which is what makes this work at all.

**Read Civ II's city screen correctly.** `Food: 14` on the City Resources panel
is what the city *eats*, not what it grows. What it grows is on the Citizens
header above the resource map, as `17🌾 4⚒ 6🔶`. Mistaking one for the other
produces a disagreement that is not there — it did here, and cost a pass.

**Ask Civ II what a single square is worth.** The Citizens header carries a
*second* triple, at its right-hand end. That one is the yield of the square the
mouse is resting on, and it changes as the cursor moves over the resource map. It
is the most useful thing on the screen for this work: it settles what one square
produces without any arithmetic on totals, and a screenshot of it is worth more
than a screenshot of the city. Clicking the square takes its citizen off, and the
totals on the left fall by exactly that triple — which is also how to confirm you
are reading the right number.

**Count the icons on the resource map.** Every worked square has its yield drawn
on it in wheat, shields and arrows, including the city's own square. Those icons
sum to the totals in the header, so the whole arrangement can be read off one
screenshot: which squares, and what each is worth. The citizen faces above say
how many of the rest are entertainers — the entertainer's face is plainly
different from a worker's.

**Photograph this game's nested dialogs.** `RHYCIV_AUTOCLICK="Button,Button"`
presses a named button on whatever window is in front, once per timed screenshot,
so the later pages of a dialog can be captured headlessly:

```
cd RaylibUI/bin/Debug/net9.0
RHYCIV_AUTOSTART=1 RHYCIV_TEST_DIPLOMACY=Persians \
RHYCIV_SHOT_DIR=/tmp/shots RHYCIV_SHOT_INTERVAL=6 \
RHYCIV_AUTOCLICK="We Have a Proposal,Never Mind,We Offer a Gift" \
xvfb-run -a -s "-screen 0 1600x920x24" dotnet RaylibUI.dll
```

---

## What agrees

Two positions from one Celtic game, checked city by city.

**2500 BC, turn 31, Despotism.** Cardiff (size 2) and Carmarthen (size 1) agree
on size, food produced and eaten, surplus, shields, support, production, trade,
corruption and the food box.

**A.D. 1240, turn 163, Monarchy.** Cardiff, size 7, Palace and Temple:

| | Civ II | rhYciv |
|---|---|---|
| Food produced / eaten / surplus | 17 / 14 / 3 | 17 / 14 / 3 |
| Shields / support / production | 4 / 0 / 4 | 4 / 0 / 4 |
| Trade / corruption | 6 / 0 | 6 / 0 |
| Tax / science | 5 / 4 | 5 / 4 |

Every number the city screen states.

---

## What the comparison found

### Phantom trade routes — fixed

A city keeps three trade-route slots whether or not it uses any, and an unused
one is zero in both the commodity and the partner fields. Both readers — the Civ
II importer and this game's own save format — built a route out of every slot,
so every city in a loaded game came out with three routes to whichever city
happened to be first in the list. Cardiff's squares produce 3 trade; the phantom
routes were worth 120 more.

Commodity zero alone does not mark a slot empty, because zero names a real
commodity; the pair being zero does.

### Every civilisation under the wrong flag — fixed

A save stores each civilisation's tribe as a position in Civ II's own `@LEADERS`
table, which runs Romans, Babylonians, Germans, Egyptians and on in no order but
its own. This game's table is alphabetical. Reading one number and looking it up
in the other put every civilisation in a loaded game under the wrong flag,
consistently and silently.

| Civ II index | Civ II tribe | our table at that index | we reported |
|---|---|---|---|
| 14 | Celts | Persians | Persians |
| 2 | Germans | Babylonians | Babylonians |
| 12 | English | Japanese | Japanese |
| 6 | Indians | Egyptians | Egyptians |
| 17 | Spanish | Sioux | Sioux |

The cities kept their real names, which is what gave it away: Berlin and Leipzig
belonging to the Babylonians. Translated by name now, so a ruleset listing its
tribes in yet another order still lands on the right one.

### A city taken from you never changed colour — fixed

What a player sees drawn is their remembered record of a square, and that record
is refreshed only for civilisations that can currently see it. A city stops
being visible to its owner at the moment they stop owning it. So the one
civilisation certain to have that square on its map was the one certain never to
be told it had changed.

---

## What still disagrees

### Kells under a Republic — traced to the end

The same city in both games, from `Cu_Auto.SAV`: Kells, A.D. 1670, turn 193,
size 8, Republic, Temple only, five units supported.

| | Civ II | rhYciv |
|---|---|---|
| Size | 8 | 8 |
| Squares worked / entertainers | 6 / 3 | 7 / 2 |
| **Food produced** | **18** | **19** |
| **Shields** | **7** | **9** |
| **Trade** | **10** | **10** |

The city screen was photographed with the cursor resting on one square after
another, which is what made this readable. Civ II's Citizens box shows the city's
totals on the left and **the yield of the square under the cursor** on the right,
and clicking a square takes its citizen off: one shot has the totals fall from
18/7/10 to 15/5/10 exactly as a square reading 3/2/0 stops being worked. So the
right-hand triple is a per-square readout, and the totals are plain sums over the
worked squares including the city's own.

Counting the icons drawn on Civ II's resource map then gives its whole
arrangement: the city square and four grassland squares at 3 food / 1 shield /
2 trade each, plus a pheasant forest at 3 / 2 / 0. That is 18 / 7 / 10, the
displayed totals, from six squares. The citizen row confirms it — of the eight
faces, the last three are entertainers.

This game's arrangement is the same six squares plus a seventh, a plain forest at
1 / 2 / 0, worked by the citizen Civ II keeps as a third entertainer.

Two things were wrong, and neither was corruption or Republic:

1. **The city square produced no shields.** Civ II's Civilopedia, under Game
   Concepts / City Squares: "if the city is built on Terrain that normally
   produces no Shields, one Shield is automatically added to the other resources
   generated in the city square." Kells sits on plain grassland, so Civ II's
   centre square gives 3 / 1 / 2 where this game gave 3 / 0 / 2. Fixed, with
   tests in `RhyCiv.Tests/Terrains/CitySquareYieldTests.cs`.

2. **The eighth citizen is put to work rather than kept content.** Drop the plain
   forest from the arrangement above and the totals become 18 / 7 / 10 — Civ II's
   figures exactly, on every count. The entire remaining gap is that Civ II's
   Kells needs three entertainers to stay out of disorder and this game thinks two
   are enough. That is the happiness model, not the terrain, the government, or
   corruption, and it is the next thing to measure.

The earlier reading of this comparison — food 20 against 18, trade 12 against 10,
corruption 3 against 0 — was taken at turn 188 and attributed to Republic
corruption and to a disagreement about *which* squares were worked. Both were
wrong. Trade agrees exactly, corruption agrees at 0, and the square lists differ
by one square, not by the composition of seven.

A false lead worth recording: the worked-square listing shows an entry at
`dx = -3`, which looks impossible for a city radius. It is not. Civ II stores X
doubled, so odd deltas are legitimate.

### The city square is improved to its maximum — measured, and already right

The same Civilopedia entry says more than the shield rule: "The city square
automatically contains a road, which is upgraded to a railroad when the Railroad
Advance is discovered. The city square is also automatically irrigated or mined,
depending on the type of terrain."

**An earlier pass of this document predicted that this game got that wrong, and
that a new city on grassland would yield 2 food against Civ II's 3. That
prediction was false and is corrected here.** This game has carried the rule all
along, in the ruleset rather than in code: `TerrainImprovementFunctions` marks
both Irrigation and Road `AllCitys`, and `GameExtensions.SetImprovementsForCity`
lays them on the city's square when the city is founded. Mining is deliberately
not marked, and a test now founds cities and checks the result rather than
leaving it to be assumed again
(`RhyCiv.Tests/Cities/NewCitySquareTests.cs`).

The Civilopedia does not say which of irrigation and mining wins on terrain that
permits both, and hills is the case that decides it. **Civ II was asked.**
Maesteg, a size-one Celtic city on hills, A.D. 1700: the city square reads 2 food,
1 shield, 0 trade. Hills are 1 food and no shields, irrigation adds a food and a
mine adds three shields — so the city square is **irrigated, not mined**, and the
single shield is the minimum, not a mine's three. That is what this game does,
and it is now written down with the measurement behind it.

That reading came from the Citizens header and the resource map together: the
header says 5 food, 1 shield, 3 trade for the whole city, the two icon groups on
the map are 2🌾1🛡 on the centre and 3🌾3☘ on the one worked square, and they
sum to the header. The worked square at 3 / 0 / 3 is an ocean fish square with
the Republic's extra arrow.

### Every wonder in every imported game was missing — fixed

A Civ II save does not record wonders on its cities. It keeps a table near the
front saying which city holds each of the twenty-eight, in a fixed order starting
with the Pyramids. `Read.ClassicSav.cs` parsed that table into three local arrays
and then never looked at them again, so **no city in an imported game held any
wonder at all**.

It was invisible until two city screens were put side by side. Civ II's Cardiff
lists Palace, Granary, Temple and the **Great Library**; this game listed the
first three. Civ II's Kells lists Temple and **Michelangelo's Chapel**; this game
listed the Temple.

And it had a consequence nobody would have traced back to a save reader. Kells
came out of the save in **civil disorder** — with Michelangelo's Chapel, which
counts as a Cathedral in every one of its owner's cities, it is not, and Civ II's
own Happiness Analysis shows six content citizens and two specialists with
nothing red left on the third row. What looked like a happiness bug was a missing
wonder.

Getting the wonder table's position wrong by one is silent in the same way, and
it happened on the first attempt: Cardiff was handed the Oracle instead of the
Great Library and Kells Copernicus' Observatory instead of Michelangelo's Chapel.
Nothing threw, nothing looked wrong, the cities simply held the neighbouring
wonder. `RhyCiv.Tests/IO/WonderTableTests.cs` now fixes the first wonder's index
at 39 and checks what sits at either end of the table.

### Trade routes were being paid Civilization I's formula — fixed

Civ II's ongoing trade route bonus, from two independent write-ups of the
formula, is

```
foreign route:  (T1 + T2 + 4) / 8
own cities:    k(T1 + T2 + 4) / 16
```

where T1 and T2 are the two cities' **base trade** — what their own squares
produce, before routes and before corruption — every division drops its
remainder, and k is a transport modifier (1.5 for a road along the route, 2 for a
railroad) that this game does not work out yet and treats as 1. Distance does not
enter it.

What this game had was `(distance + 10) * (Ta + Tb) / 24`, halved for the same
continent and again for the same civilisation. That is **Civilization I's**
formula, and not even that game's continuing-route formula — it is Civ I's
one-off payment for the delivery, charged again every turn. Measured against Civ
II on the same save it paid four arrows a turn where Civ II paid one.

The delivery bonus keeps that arithmetic, because a delivery is a different thing
and distance genuinely belongs in it, but it now owns the calculation rather than
borrowing the route's. **The delivery figure has not been checked against Civ II
and remains unverified.**

Base trade is now recorded on the city as `TileTrade`, because a route cannot be
valued from the city's `Trade`: the route's own arrows are part of that, so a
route worked out from it would pay itself more every turn.

### Trade cities: Cardiff agrees exactly

A.D. 1700, turn 196, Republic, from `tradesave.sav` — a save taken deliberately at
the moment the screenshots were taken, which is what made this comparable at all.

| | Civ II | rhYciv | |
|---|---|---|---|
| **Cardiff** — food / eaten / surplus | 17 / 14 / 3 | 17 / 14 / 3 | ✓ |
| shields / support / production | 10 / 2 / 8 | 10 / 2 / 8 | ✓ |
| base trade / route / corruption / net | 11 / +1 / 0 / 12 | 11 / +1 / 0 / 12 | ✓ |
| improvements | Palace, Granary, Temple, Great Library | same | ✓ |
| citizens / specialists / disorder | 7 / 1 / no | 7 / 1 / no | ✓ |
| **Kells** — food / eaten / surplus | 19 / 16 / 3 | 19 / 16 / 3 | ✓ |
| shields / support / production | 9 / 2 / 7 | 9 / 2 / 7 | ✓ |
| base trade | 10 | 10 | ✓ |
| improvements | Temple, Michelangelo's Chapel | same | ✓ |
| happiness | 6 content, 2 specialists, no disorder | same | ✓ |
| corruption | 0 | 2 | ✗ |
| **Carmarthen** — food / eaten / surplus | 18 / 14 / 4 | 18 / 14 / 4 | ✓ |
| shields / support / production | 8 / 2 / 6 | 8 / 2 / 6 | ✓ |
| base trade | 14 | 14 | ✓ |
| route / corruption | +2 / 1 | +1 / 4 | ✗ |

Cardiff now agrees with Civ II on **every figure its city screen shows**. That is
the first city to do so, and it is worth saying plainly because it means the
terrain reading, the city square rules, the government, the trade route formula,
the improvement list and the happiness model are all right together for at least
one real city in a real position.

### What is left, in order

1. **Corruption.** Kells 2 against 0, Carmarthen 3 against 1, Cardiff 0 against 0.
   Kells and Carmarthen are the *same* distance from the capital — both four
   columns and six rows away — so the two games can be compared directly at one
   distance. Civ II gives Kells, with 10 trade, no corruption at all, and
   Carmarthen, with 16, exactly one. That brackets Civ II's rate at this distance
   between 1/16 and 1/10 of trade. This game's rate is about one fifth. Working
   back through the documented formula, `trade × min(32, distance) × 15/(4+gov) /
   100`, Civ II is behaving as though the distance were between 3.75 and 6 where
   this game computes something between 12 and 17.

   The suspect is `CityExtensions.ComputeDistanceFactor`, which measures a
   straight-line distance on the *stored* column numbers and then multiplies by
   `Map.ScaleFactor` (`XDim * YDim / 4000`). No source consulted so far has a map
   scale term in it at all. **Do not change this without a source**: the formula
   already carries an Apolyton citation and the last person to reason about
   corruption from first principles here got it wrong.

2. **The asymmetric trade route.** Cardiff's route pays +1 and Carmarthen's pays
   +2, from the same pair of cities. The sourced formula is symmetric in T1 and
   T2 and the source states plainly that both cities receive the same amount, so
   something not in it is at work. Carmarthen's line carries a trailing `+` that
   Cardiff's does not — `Cardiff Gems: +2☘+` against `Carmarthen Silk: +1☘` — and
   Cardiff demands Gems while Carmarthen does not demand Silk. That suggests the
   demanded commodity raises the standing route and not only the delivery, but the
   source consulted says the opposite, and one observation is not enough to
   overrule it. **Unresolved; needs either a second source or a second pair of
   cities to measure.**

3. **The delivery bonus**, which remains Civ I's and unmeasured.

### Whales were worth a shield and an arrow too little

Carmarthen was one shield and one trade short of Civ II with the same squares
worked. Hovering each of its squares in turn named the culprit in one shot: Civ
II prices its whales square at 2 food, 2 shields, 4 trade under a Republic, and
this game priced it 2 / 1 / 3.

The standalone ruleset had `Whales, 1, 2, 2, 1, 2` where Civ II's figures are
2 food, 2 shields, 3 trade — the fourth arrow on the screen being the Republic's
own, added to any square already producing trade. Corrected, and Carmarthen's
food, shields, support, production and base trade all agree.

Two confirmations, as a terrain number deserves: the measurement above, and the
published Civ II terrain tables, which give Whales as raising ocean from 1/0/2 to
2/2/3.

**The rest of the specials table has not been checked this way**, and one wrong
number in it was invisible for as long as nobody put a city beside its original.
Coal, Musk Ox/Game and Spice are the three this pass could not confirm from a
source and should not be changed on recollection. Hovering one square of each in
Civ II settles each of them in a single screenshot.

### Take the save at the same moment as the screenshot

Civ II's `Cu_Auto.SAV` is written at the start of each turn, so anything done
during the turn — a caravan delivered, a city founded, squares rearranged — is in
the screenshot and not in the save. Two comparisons here ran into it: Maesteg,
founded mid-turn, is not in the save at all, and the trade routes above appear in
neither city's record.

So for anything mid-turn, save deliberately (Game → Save Game) immediately before
or after taking the picture, and name the file after what it is for.

---

## Rules researched against sources

### Diplomat and Spy missions

From Civ II's own mission table (the in-depth guide, Table 1.1):

| Mission | Diplomat | Spy |
|---|---|---|
| Establish embassy | Killed | Killed |
| Investigate city | Killed | May survive, moved to nearest friendly city |
| Steal technology | Killed | May survive |
| Industrial sabotage | Killed | May survive |
| Incite a revolt | Killed | May survive |
| **Bribe a unit** | **Mission success** | **Mission success** |

Bribing a unit is the one mission either walks away from. This game was spending
the Diplomat on it as though it had incited a revolt — fixed; it costs a move.

Not yet implemented: a Spy's survival is currently certain rather than a chance,
and Civ II moves a surviving Spy to the nearest friendly city rather than leaving
it where it stands.

### Defence strength

From the CivFanatics combat guide and testing reported at CivFanatics:

- Fortified land units: ×1.5.
- **City walls, a fortress and being fortified do not stack.** The fortification
  bonus is "superceded by fortress improvement and city walls", and the game
  takes the walls or the fortress *even where fortifying would give the better
  number* — an order of precedence, not the best of three. This project's own
  deep audit asserted the opposite and a fix was written to match it; both were
  wrong, and `docs/CIV2-PARITY-DEEP-AUDIT.md` has been corrected.
- City walls answer **land attacks only**.
- A river adds **half a step to the terrain adjustment** rather than multiplying
  it — "a hill square with a river gives a ×2.5 bonus, a (2 + 0.5) multiplier".
  Applied as a ×1.25 the two agree on hills by coincidence and nowhere else.
- **There is no defence bonus for standing in a city that has no walls, and city
  size does not affect defence.** Checked because it is the natural next
  assumption; a search result claiming otherwise turned out to be about
  Civilization IV.
- **Settlers and Engineers cannot fortify** — "the only units incapable of
  fortifying". They keep the fortress bonus, which Civ II gives a land unit
  occupying one "whether given the order to fortify or not".
- A Settlers unit has **20 hit points**, double every other unit of its age, so
  an early attacker genuinely loses to one about half the time. That is Civ II
  working, not a fault, and the shipped ruleset already carries the number.

### Messages when a city finishes something

Civ II's City Report Options carry a switch for "Show City Improvements Built"
and one for "Show **Non-Combat** Units Built", and none for combat units — which
there would have to be if combat units were ever announced. So Civ II announces
buildings and units that cannot fight, and says nothing about a warrior or a
horseman. This game had it the other way round.

---

## Diplomacy: what the original does

Taken from screenshots of a live game, for the work still to come.

**The Foreign Minister** lists every civilisation met as a single line carrying
three facts:

> Consul Ishmael of the Babylonians (**Worshipful**, Allied, No Embassy)
> Empress Isabella of the Spanish (**Enraged**, War, No Embassy)
> Consul Hippolyta of the Greeks (**Uncooperative**, Peace, No Embassy)

— an attitude word, the treaty in force, and whether an embassy exists. Two
buttons: *Check Intelligence* and *Send Emissary*. Attitude words seen so far:
**Worshipful**, **Enraged**, **Uncooperative**. This is the vocabulary this
game's numeric `DiplomacyFunctions.Attitude` has to map onto, and the screen it
has to fill.

**A negotiation** is a conversation held in a throne room with the other
leader's portrait, not a flat menu. The emissary is addressed by attitude —
"Worshipful Babylonian Emissary" — and the player picks from a short list that
changes with the state of the relationship:

- Consider this disclosure complete
- Request a gift of gold or goods
- Cancel this worthless alliance
- Have a proposal to make
- Wish to offer you a gift

"Have a proposal to make" opens a second list: *never mind*, *ask to exchange
knowledge*, *ask to declare war against …*, *ask to share world maps*. Declaring
war against somebody opens a third list of the civilisations known to both. Each
is answered in the leader's own voice, and a refusal is a sentence with a reason
in it — "We have no interest in exchanging knowledge at this time", "We have no
quarrel with the Spanish", "Very well, we shall share the knowledge of geography
with you".

This game's parley is a flat menu of options with no portrait, no attitude
vocabulary, no counter-proposals and no reasons given. The structure above is
what it needs to grow into.

**Diplomat missions in the field** are chosen from a menu headed by the arriving
unit — "Celtic Diplomat arrives in Cadiz" — offering Establish Embassy,
Investigate City, Steal Technology, Industrial Sabotage and Incite a Revolt.
This game offers the same five. Inciting quotes a price first: "Cadiz will
revolt for 776 gold".

---

## Still unverified

- **Happiness.** The happy/content/unhappy split has never been compared against
  anything. It is the most rule-dense part of the city model — martial law, the
  Temple, luxuries and the difficulty level all feed it. The `Happy` button on
  Civ II's city screen is the oracle.
- **A real trade route.** The route reader was changed on the strength of
  phantom routes being wrong; no position with a genuine caravan route has been
  compared.
- **Democracy**, and Republic's Senate, which is not implemented at all.
- **The tax, luxury and science split** matches on the two positions checked, but
  the figures Civ II displays sum to more than the city's trade, which is not yet
  understood.

---

## Sources

- The Complete Civilization II Combat Guide — <https://civfanatics.com/civ2/strategy/combatguide/>
- Civilization 2 defence modifier calculations (tested findings) — <https://forums.civfanatics.com/threads/civilization-2-defense-modifier-calculations.683500/>
- Civilization II In-Depth Guide, Table 1.1 (Diplomat and Spy mission outcomes) — <https://www.supercheats.com/pc/walkthroughs/civilization2-walkthrough01.txt>
