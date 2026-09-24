# Civ II Rules Research

## Issue #134: Barbarians as veterans by difficulty

**Claim in the code before the fix:**
`DifficultyRules.BarbariansAreVeterans(game)` returned true from King upward,
making barbarians spawn as veteran units on King, Emperor, and Deity.

**Civ II rule being tested:**
Does difficulty make barbarians veteran units?

**Sources checked:**

- `.civ2-wiki/Difficulty_level_(Civ2).wiki`
  - The difficulty table lists three effects: citizen unhappiness, AI production
    costs, barbarian attack strength, and the AI "production box squeeze" at
    Prince/Deity. It does not say barbarians become veterans.
- `.civ2-wiki/Barbarian_(Civ2).wiki`
  - Barbarians are described as wandering units that appear in unexplored land
    and goody huts. No difficulty-based veteran barbarian rule is mentioned.
- `The Barbarian Paper`
  (https://sleague.civfanatics.com/articles/barbarians/)
  - Says: "The difficulty level determines the strength of barbarians: Chieftain
    25%, Warrior 50%, Prince 75%, King 100%, Emperor 125%, Deity 150%."
  - No statement that barbarians become veteran units at higher difficulty.
- CivFanatics "Civ2 Difficulty Levels" discussion
  (https://forums.civfanatics.com/threads/civ2-difficulty-levels.527324/)
  - Difficulty effects mentioned are city production, citizen unhappiness, AI
    bonuses, and barbarian strength. No barbarian veteran effect is listed.
- `.civ2-wiki/Combat_(Civ2).wiki`
  - Veterans get a general 1.5x attack and defense multiplier, and units can
    become veterans after surviving combat. That is a combat outcome, not a
    difficulty-based barbarian spawn rule.

**Agreement or disagreement:**
The sources agree that higher difficulty makes barbarians stronger attackers.
They do not support barbarians spawning as veterans from King upward.

**Unverified:**
I could not access a page that explicitly says barbarians are never veterans on
lower difficulties; however, no reliable Civ II source lists a difficulty-based
barbarian veteran mechanic. The existing claim appears to be a misreading of
barbarian attack strength.

**Owner ruling (2026-09-24):** the owner, who plays Civ II, confirms that
barbarians are not veterans by default at any difficulty. That, together with no
source listing such a rule, settles it. Difficulty scales barbarian attack
strength (the Barbarian Paper's 25%-150% table), not veteran status.

**Not settled by this change:** #75 (barbarians still too tough on Deity) and
#134 (one barbarian killing two fortified units in a turn) remain open. Both need
separate checks: whether an attack spends the attacker's whole move, whether a
damaged unit keeps attacking, and when a whole stack dies with its defender.

**Conclusion:**
The difficulty-based barbarian veteran behavior is not supported by Civ II rules.
The fix removes `DifficultyRules.BarbariansAreVeterans` and makes barbarians
spawn non-veteran, while keeping the supported difficulty effect on barbarian
attack strength.

## #140 · Meeting a new civilisation opens the negotiation, not just a greeting

**Rule.** Contact is not a silent flag: it is a meeting. Contact between two
civilisations is established automatically when rival ground units enter
adjacency or a unit approaches a rival city, and the computer civ then requests
an audience — a herald introducing the leader — which is the frame in which
all negotiation happens. A Diplomat opening an embassy is likewise a contact
event (walking up to a rival city is itself contact), so it must announce the
meeting like any other introduction rather than quietly flipping a bit.

**Sources.**

- `.civ2-wiki/Diplomacy_(Civ2).wiki` ("Diplomatic relations" / "Negotiation"):
  "Contact is established when rival ground units enter adjacency, or a unit
  approaches a rival city." And: "Computer-controlled civs will normally request
  a meeting the first time their ground units encounter the player's, and may
  seek an audience on subsequent contact. The player may choose to accept or
  refuse... Meetings are framed as the player receiving an emissary from the
  civilization. If enabled in graphic options, this uses a special screen
  featuring a portrait of the civ's leader..."
- Official *Sid Meier's Civilization II* user guide (ManualShelf mirror,
  https://www.manualshelf.com/manual/games-pc/civilization-ii/user-guide-english.html),
  "Conducting Diplomacy with Computer Opponents" (p. 123): "Diplomacy is
  conducted face to face with one rival emissary at a time. An opponent can
  contact you any time after units from each of your civilizations have met, and
  the reverse is also true." The guide's own tutorial enacts the first-contact
  audience (p. 30): "As soon as you enter Sioux territory, their leader, Sitting
  Bull, requests an audience with you. Accept Sitting Bull's invitation by
  clicking OK."
- `.civ2-wiki/Espionage_(Civ2).wiki` ("Establish Embassy"): the embassy action
  is performed by a Diplomat standing at a rival city — the same approach that
  the Diplomacy page says establishes contact.

**Agreement.** Wiki and manual agree on both parts: contact happens on
encounter, and an encounter is a meeting with the leader, not a name quietly
added to a list.

**Unverified.** Whether the player-initiated route ("Send Emissary" from the
Foreign Minister) also lands straight on the leader's screen or on a list of
known civs; the sources describe the AI's request for an audience but not the
exact player-side menu shape. Herald animation and portrait are a graphics
setting (absent in *Test of Time*), so they are presentation, not rules.

**Code consequence (already landed in Release 0.2.2, verified here).**
`LocalPlayer.ContactMade` announces the herald (GREETINGS) and, once it is
dismissed, seats the player opposite the leader just met
(`GameScreen.MeetByParley` → `Diplomacy.ParleyWith`) instead of leaving the
Foreign Ministry list as the only door to diplomacy;
`DiplomatActions.EstablishEmbassy` routes through
`DiplomacyFunctions.MakeContact`, so a Diplomat's embassy is a real
introduction for both sides. Covered by
`RhyCiv.Tests/Diplomacy/EmbassyMakesContactTests.cs`. Same root cause as #149,
whose rule was verified the same way against the wiki and the manual.

## #149 · First contact opens the negotiation

**Rule.** Contact between two civilisations is established automatically when
rival ground units enter adjacency or a unit approaches a rival city; the
computer civ then requests a meeting, which is framed as receiving an emissary
(a herald introducing the leader), and negotiations proceed from there. After
contact, either side may open an audience at will ("Send Emissary" from the
Foreign Minister). A Diplomat establishing an embassy is likewise a contact
event, not a silent flag write.

**Sources.**

- `.civ2-wiki/Diplomacy_(Civ2).wiki` (Negotiation section): "Contact is
  established when rival ground units enter adjacency, or a unit approaches a
  rival city. Known civilizations are listed in the Foreign Minister window;
  whereas the original *Civilization* required the human player dispatch a
  diplomat to initiate a meeting, in *Civ II* rivals can be contacted at any
  time, subject to leaders' patience." And: "Computer-controlled civs will
  normally request a meeting the first time their ground units encounter the
  player's, and may seek an audience on subsequent contact... Meetings are
  framed as the player receiving an emissary from the civilization."
- Official *Sid Meier's Civilization II* user guide (ManualShelf mirror,
  https://www.manualshelf.com/manual/games-pc/civilization-ii/user-guide-english.html),
  "Conducting Diplomacy with Computer Opponents" (p. 123): "Diplomacy is
  conducted face to face with one rival emissary at a time. An opponent can
  contact you any time after units from each of your civilizations have met,
  and the reverse is also true. You can contact an opponent any time after your
  units have been adjacent to his or hers. Just select the SEND EMISSARY option
  from the FOREIGN MINISTER's report in the ADVISORS menu." The manual's own
  tutorial enacts the first-contact meeting: "As soon as you enter Sioux
  territory, their leader, Sitting Bull, requests an audience with you. Accept
  Sitting Bull's invitation by clicking OK."

**Agreement.** The wiki and the manual agree on both parts: adjacency or
approaching a city makes contact automatically, and the meeting (the diplomacy
screen with the leader/herald) follows, rather than the player merely getting a
name added to a menu.

**Unverified.** The exact timing of the AI's request versus the greeting popup
(wiki says the AI "normally requests" a meeting on first encounter and the
player "may choose to accept or refuse"; rhYciv presents the herald and then
opens the parley directly, with no refuse-a-meeting option). Herald animation
and portrait details are a graphics setting and absent in *Test of Time*, so
they are not rules the engine must enforce.

**Code consequence (already landed in Release 0.2.2, verified here).**
`LocalPlayer.ContactMade` announces the herald (GREETINGS) and then opens the
audience with the leader just met (`GameScreen.MeetByParley` →
`Diplomacy.ParleyWith`) instead of leaving the player to find them in the
Foreign Ministry list; `DiplomatActions.EstablishEmbassy` routes through
`DiplomacyFunctions.MakeContact` so a diplomat's embassy is a proper
introduction (herald, negotiation, record) rather than a silent flag write.
Covered by `RhyCiv.Tests/Diplomacy/EmbassyMakesContactTests.cs`. Also applies
to #140.

## Issue #174 - How is Diplomacy going?

### Verified rule

A cease-fire in Civilization II is temporary. The manual says it lasts approximately 16 turns (rhYciv uses 5; see the owner ruling below).
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

### Owner ruling (2026-09-24): five turns

The owner, who plays the original game, is confident cease-fires last well short
of 16 turns and ruled that rhYciv uses **5 turns**, with tribute restarting the
count. This is a deliberate departure from the manual's "approximately 16",
based on the owner's play and consistent with the player reports above of
cease-fires ending early. It is unmeasured: the test described above should
still be run, and `DiplomacyFunctions.CeaseFireDuration` changed if it shows
otherwise.

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
