# Civ II Rule Research: Issue #157 “Invisible Enemy”

## Issue

GitHub issue `crhy/rhYciv#157` reports that a player could not move away from an enemy, and the reporter expected that if an enemy was nearby and limiting movement, it should be exposed and visible.

The issue title is “Invisible Enemy,” and the body says:

> Also an error wherein I couldn't move AWAY from the enemy. If there was another enemy nearby limiting my movement, it should be exposed and visible.

## Question being answered

Does original Civilization II automatically reveal an enemy unit that is otherwise invisible when that enemy’s Zone of Control blocks movement?

Specifically, if a submarine or other invisible unit exerts ZOC against a player’s unit, does the act of being blocked by ZOC make the invisible enemy visible?

## Summary

The rule is currently inconclusive.

There is no decisive authoritative Civ II source in the available materials proving that ZOC automatically reveals invisible enemies. The local wiki and code evidence show ZOC behavior and visibility mechanics, but they do not clearly connect ZOC blocking to automatic enemy reveal.

A Reddit search snippet claims that ZOC means “you will always be able to see a Submarine if it is exerting ZoC over one of your units,” but the full Reddit thread could not be fetched reliably, and the claim was not verified against an authoritative Civ II manual or rules reference.

Therefore, the safe conclusion is: do not implement a behavior change without stronger evidence.

## Relevant Civ II mechanics

### Zone of Control

Original Civ II ZOC mechanics:

- ZOC is exerted by land units on adjacent tiles.
- Foreign ground units cannot move between two tiles that are both adjacent to enemy ZOC.
- Cities exert ZOC only when occupied by units.
- ZOC affects movement, not attacks.
- Some units and situations ignore or override ZOC, including Diplomats/Spies, moving into or out of cities, moving onto friendly units, and terrain cases where ZOC cannot apply.

### Visibility and stealth

Civ II has visibility and stealth mechanics, especially for submarines and units that cannot detect submarines.

Relevant known behavior:

- Units have detection/spotting rules.
- Submarines are stealthy unless detected by units or terrain/features that can detect them.
- Fog of war controls whether a tile and its contents are visible to a civilization.

However, the available documentation does not clearly state whether ZOC itself overrides stealth or reveals the unit exerting it.

## Code evidence

### ZOC blocking in the engine

In `Engine/src/UnitActions/MovementFunctions.cs`, movement is blocked when a tile is next to enemy ZOC:

```csharp
if (!unit.IgnoreZonesOfControl &&
    !IsFriendlyTile(tileTo, unit.Owner) &&
    IsNextToEnemy(tileFrom, unit.Owner, unit.Domain) &&
    IsNextToEnemy(tileTo, unit.Owner, unit.Domain))
{
    MoveBlocked(unit, BlockedReason.Zoc);
    return false;
}
```

`IsNextToEnemy` checks neighboring tiles for enemy units in the relevant domain:

```csharp
private bool IsNextToEnemy(Tile tile, int civId, Domain domain)
{
    return tile.GetNeighbors().Any(t => t.Units.Any(u =>
        u.InShip == null &&
        u.Owner != civId &&
        u.Domain == domain));
}
```

This check does not reveal the enemy.

### Visibility animation and sentries

`MovementFunctions.cs` also contains visibility-related animation logic:

```csharp
if (!tileTo.IsCurrentlyVisible(unit.Owner))
{
    tileTo.SetVisible(unit.Owner);
    ...
}

WakeSentriesNear(unit, tileTo);
```

This makes the destination tile visible when the moving unit enters it, and wakes nearby sentry units. It does not reveal an enemy that merely blocks movement via ZOC.

### UI reporting

In `RaylibUI/RunGame/LocalPlayer.cs`, blocked movement is reported with a message such as:

> cannot move directly between two squares that are next to an enemy unit

This confirms the game recognizes the ZOC block, but it does not reveal the enemy.

## Local documentation evidence

### `docs/CIV2-PARITY-AUDIT.md`

Lists “Submarine visibility” as an open parity issue.

Relevant note:

> Submarine stealth - invisible to units that cannot spot it: absent

This suggests submarine stealth is not fully implemented, but it does not answer whether ZOC reveals invisible enemies.

### `docs/CIV2-PARITY-DEEP-AUDIT.md`

Notes that Movement and ZOC are faithful to Civ II, but separately discusses submarine stealth/visibility as an open issue.

It does not provide a conclusive rule that ZOC reveals invisible units.

## External verification attempts

Attempted sources:

- CivFanatics ZOC thread:
  - Could not reliably access the thread body.
  - Fetched page contained mostly verification/navigation JavaScript.
- CivFanatics rules/manual pages:
  - Fetched pages did not produce useful ZOC reveal text.
- Reddit thread:
  - Search result claimed: “Zone of Control is applied to all tiles adjacent to military units, which means you will always be able to see a Submarine if it is exerting ZoC over one of your units.”
  - Full thread body could not be fetched reliably.
- Wayback Machine:
  - Availability API returned HTTP 429.
  - CDX searches returned no useful output.
- General web search:
  - No decisive authoritative Civ II rule text was found.

## Conclusion

The issue is plausible: if an invisible enemy blocks movement through ZOC, players may reasonably expect that enemy to be visible.

But the original Civ II rule is not conclusively documented in the available sources.

Do not implement a reveal-on-ZOC behavior until an authoritative Civ II manual, rules reference, or verified original behavior source confirms it.

## Suggested next step if continuing

Find a reliable original Civ II manual or rules reference that explicitly states whether ZOC reveals invisible units.

If that evidence shows Civ II reveals ZOC-exerting invisible enemies, then implement a minimal fix:

1. Create or update test coverage for ZOC reveal behavior.
2. Modify the ZOC blocking path so the blocking enemy is marked visible before or during the blocked move.
3. Run the test suite.
4. Submit a PR against `fix/157`.
