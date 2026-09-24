# Civ II Rule Research

This file records verified Civilization II rules before a change is made. A rule is
verified here only when it is supported by the original manual and, when available,
the local Civ II wiki.

## Issue #174 - How is Diplomacy going?

### Verified rule

A cease-fire in Civilization II is temporary. It lasts approximately 16 turns.
When tribute is paid by either side, the cease-fire is automatically extended by
approximately 16 more turns. When it expires, the two civilizations return to a
neutral relationship unless a permanent peace or alliance has been made.

### Manual evidence

Original manual source:
`https://ia800801.us.archive.org/27/items/sid-meiers-civilization-ii-manual/Sid%20Meier%27s%20Civilization%20II%20Manual_djvu.txt`

The manual says:

> "In game terms, cease fires expire after approximately 16 turns, and they are
> automatically extended when tribute is paid by either side. You are informed when
> a cease fire has expired."

It also says:

> "Once a cease-fire is signed, your former enemy ceases attacking your units and
> cities for approximately 16 turns... Once the cease-fire expires, your
> civilizations remain in a state of neutrality... A cease-fire is automatically
> extended for an additional 16 turns or so whenever tribute is paid by either
> side."

### Local wiki evidence

`.civ2-wiki/Diplomacy_(Civ2).wiki` says:

> "Cease-Fire: A temporary suspension of war lasting 16 turns... If the cease-fire
> expires without a permanent peace, relations default to `none`."

This agrees with the manual.

### Second check (2026-09-24): the sources disagree on how long a cease-fire really lasts

The owner, who plays Civ II, reports that cease-fires end sooner than 16 turns.
A wider search found that only the official texts give 16; players report
shorter and variable durations. Nobody has published a measurement or a
save-file field for a cease-fire counter.

| Source | Kind | What it says |
|---|---|---|
| Civ II manual (archive.org text, above) | official | "approximately 16 turns", extended "16 turns or so" by tribute |
| Official Strategy Guide, quoted by Ace in [CivFanatics: how long does a cease fire last?](https://forums.civfanatics.com/threads/how-long-does-a-cease-fire-last.55033/) | official | "a cease-fire lasts only 16 turns. At the end of the cease-fire, hostilities resume." "The worse your reputation, the more likely it is that your opponents might break the cease-fire agreement." |
| Civilization fandom wiki, Diplomacy (Civ2) | wiki | "A temporary suspension of war lasting 16 turns" |
| Same CivFanatics thread | recollection | "16 or 20 turns for the human player" (TimTheEnchanter); "12 to 15 turns" (Duke of Marlbrough) |
| [Apolyton: How long does a cease fire last?](https://codehappy.net/apolyton/threads/73834-1.htm) | recollection / anecdote | "a random 8-16 turns" (DaveV); a cease-fire that "expired in less than one complete turn" after being accepted (rjmatsleepers); "a 'one-turn' cease-fire" seen "on a rare occasion" (Gatekeeper) |
| [Apolyton, same thread p.2](https://apolyton.net/forum/miscellaneous/archives/civ2-general-help-archive-aa/86231-how-long-does-a-cease-fire-last/page2) | recollection / anecdote | "14 turns if your rep has no black marks against it" (War4ever); moving a unit next to the enemy about three turns in got a contact and the cease-fire "expired" (Reinhard-Baer) |
| [CivFanatics: Cease fire](https://forums.civfanatics.com/threads/cease-fire.327000/) | recollection | "supposed to run for 16 turns (IIRC), but it depends on your reputation" (Ace); cease-fires "expire if you subvert a city" (Prof. Garfield); expiry leaves "no treaty", not war (Peaster) |
| [Apolyton: Cease Fire](https://apolyton.net/forum/miscellaneous/archives/civ2-strategy-archive-aa/3432-cease-fire) | anecdote | in the other direction: a cease-fire that held for 1000+ years in a multiplayer game (Finbar) |
| [Civ II SAV/SCN format (Apolyton)](https://apolyton.net/forum/civilization-series/civilization-i-and-civilization-ii/130935-civilization-ii-sav-scn-file-format), [ToT save format (CivFanatics)](https://forums.civfanatics.com/threads/save-file-format.669406/) | reverse engineering | no cease-fire counter or expiry field documented |

**Where this leaves the rule.** 16 turns is the documented upper figure. Several
players independently report cease-fires ending sooner, sometimes within a turn,
with reputation and units near the other civilisation named as causes. It is not
settled whether the shorter endings are a shorter or random timer (8-16 is
claimed once, unsourced) or the AI ending the cease-fire early. After expiry the
relation is "no treaty", not war. Subverting a city ends a cease-fire.

**Unverified; needs a measurement in the original game:** sign a cease-fire, pay
no tribute, keep units away from the other civilisation, and note the turn of the
"cease-fire has expired" message; repeat with a poor reputation and with units
near their cities.

### What was changed

1. `Relation` now stores `CeaseFireTurn`, the turn when the cease-fire expires.
2. `AgreeCeaseFire` records the expiry turn.
3. `RenewCeaseFire` extends an existing cease-fire when tribute is paid.
4. `ExpireCeaseFires` clears expired cease-fires once per turn.
5. AI and local player tribute handling call `RenewCeaseFire`.

### Not verified enough to implement

The issue mentions other diplomacy questions, such as AI tribute and threat
triggers, exact reputation labels, withdraw-troops behavior, and the Foreign
Advisor screen. Those parts were not conclusively verified against the manual and
local wiki, so they were left unchanged.
