# Issue #75 research findings

## Issue

GitHub issue #75: `Barbarians are a bit too tough still in Deity.`

Body: `One horse went along and killed TWO fortified warriors in cities. Without stopping to heal first.`

There are no comments on the issue in the GitHub view or the local archive (`docs/issues-archive/issues.json`).

## Verified rule: barbarian attack strength scales by difficulty

Local Civ II wiki: `.civ2-wiki/Difficulty_level_(Civ2).wiki`

Barbarian Attack Strength by difficulty:

| Difficulty | Barbarian attack strength |
|---|---:|
| Chieftain | 25% |
| Warlord | 50% |
| Prince | 75% |
| King | 100% |
| Emperor | 125% |
| Deity | 150% |

Independent cross-check: CivFanatics thread "Play as Barbarians - how to do it without hex editor" via `r.jina.ai` says:

> Note that Barbarian attack strength is modified by the difficulty level (Chieftain = x0.25, Warlord = x0.50, etc., increasing by 0.25 per level up to Deity = x1.50)

The current implementation already matches this in `Engine/src/DifficultyRules.cs`:

```csharp
private static readonly int[] BarbarianAttack = [25, 50, 75, 100, 125, 150];
```

Tests in `RhyCiv.Tests/DifficultyRulesTests.cs` already cover this.

## Candidate rule: `BarbariansAreVeterans` at King/Emperor/Deity

`Engine/src/DifficultyRules.cs` contains:

```csharp
public static bool BarbariansAreVeterans(IGame game) => Level(game) >= (int)DifficultyType.King;
```

This is used when spawning barbarians:

```csharp
DifficultyRules.BarbariansAreVeterans(game)
```

Tests currently assert that barbarians are veterans at King and Deity:

```csharp
Assert.False(DifficultyRules.BarbariansAreVeterans(Game(DifficultyType.Prince)));
Assert.True(DifficultyRules.BarbariansAreVeterans(Game(DifficultyType.King)));
Assert.True(DifficultyRules.BarbariansAreVeterans(Game(DifficultyType.Deity)));
```

## Evidence status

I could not find a reliable Civ II source supporting "barbarians become veterans at King/Emperor/Deity".

Sources checked:

- `.civ2-wiki/Difficulty_level_(Civ2).wiki`
- `.civ2-wiki/Barbarian_(Civ2).wiki`
- `.civ2-wiki/Combat_(Civ2).wiki`
- `.civ2-wiki/Village_(Civ2).wiki`
- grep for `veteran` across local wiki, engine, and tests
- CivFanatics Civ2 forum search via DuckDuckGo
- Original Civ2 difficulty thread via `r.jina.ai`

The original thread via `r.jina.ai` discusses difficulty bonuses such as:

```text
Barbarian Attack Bonus (Emperor=25%, Deity=50%)
```

It does not mention barbarians becoming veterans.

The DuckDuckGo snippet from another CivFanatics thread confirms attack-strength scaling, but not veteran status.

## Conclusion

The barbarian difficulty attack strength is verified and already implemented.

The `BarbariansAreVeterans` rule appears to be an unverified assumption. Because there is no conclusive Civilization II source confirming barbarians become veterans at King/Emperor/Deity, the correct action is to avoid changing game code based on this candidate rule.

`RESULT #75: SKIPPED - no verified Civ II rule supports barbarians becoming veterans at King/Emperor/Deity; difficulty-based barbarian attack strength is already implemented and cross-checked.`
