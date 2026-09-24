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