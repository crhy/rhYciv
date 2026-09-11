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

`RHYCIV_REPORT_CITY=NAME` adds a listing of that city's worked squares with the
terrain, the special and what each yields. A disagreement about a city's food or
trade always comes down to which squares it is working and what they are worth.

**Get Civ II's numbers.** Open the same save and screenshot the city screen. Civ
II states its own figures there, which is what makes this work at all.

**Read Civ II's city screen correctly.** `Food: 14` on the City Resources panel
is what the city *eats*, not what it grows. What it grows is on the Citizens
header above the resource map, as `17🌾 4⚒ 6🔶`. Mistaking one for the other
produces a disagreement that is not there — it did here, and cost a pass.

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

### Republic: food, trade and corruption

Kells, A.D. 1620, turn 188, size 8, Republic, Temple only.

| | Civ II | rhYciv |
|---|---|---|
| Size | 8 | 8 |
| Shields / support / production | 7 / 5 / 2 | 7 / 5 / 2 |
| Worked squares / specialists | 7 / 2 | 7 / 2 |
| **Food produced** | **18** | **20** |
| **Trade** | **10** | **12** |
| **Corruption** | **0** | **3** |

Both work seven squares with two entertainers and shields agree exactly, so the
terrain reading is sound and the difference is *which* seven squares — one
square's worth of food and trade, in a mask read straight out of the save.

Corruption 0 against 3 for a city that is not the capital is the more suspicious
of the two, and may be a separate fault in how Republic distance corruption is
worked out.

Monarchy and Despotism agree completely, so whatever this is, it is specific to
Republic or to this position.

A false lead worth recording: the worked-square listing shows an entry at
`dx = -3`, which looks impossible for a city radius. It is not. Civ II stores X
doubled, so odd deltas are legitimate.

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
