using Model.Core.Mapping;

namespace RhyCiv.Engine.Terrains;

/// <summary>
/// Civ II's Mountains mining shield bonus.
/// <para>
/// CIV2-PARITY-DEEP-AUDIT.md TR4: the ruleset's Mountains row gave mining +1
/// shield, but Civ II mines Mountains for +2 (RULES.txt @TERRAIN, "Mountains
/// mining is +1 shield (Civ II: +2)"). A worked Mountain is a citizen mining a
/// shieldless mountain square, so it yields its (zero) base shields plus the
/// mining bonus -- Civ II's +2, not the old +1.
/// </para>
/// </summary>
public static class MountainMiningParity
{
    /// <summary>The shield bonus Civ II grants for mining a Mountain.</summary>
    public const int Civ2MiningShieldBonus = 2;

    /// <summary>
    /// Shields a worked Mountain makes: the terrain's base shield yield plus the
    /// mining shield bonus its ruleset row carries.
    /// </summary>
    public static int ExpectedWorkedShieldYield(Terrain mountain)
        => mountain.Shields + mountain.MiningBonus;
}
