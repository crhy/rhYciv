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
