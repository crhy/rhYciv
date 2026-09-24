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
