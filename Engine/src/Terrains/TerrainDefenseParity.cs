using Model.Core.Mapping;

namespace RhyCiv.Engine.Terrains;

/// <summary>
/// The defensive multiplier Civ II gives a unit standing on each terrain: the
/// expected value the parity tests compare the engine against, kept in one place so
/// the terrain-defence collapse is caught without a display.
/// <para>
/// A rules.txt terrain defence value counts half-steps rather than whole ones, so
/// Forest, Jungle and Swamp carry 3 (x1.5), Hills carry 4 (x2) and Mountains carry
/// 6 (x3), while every other land square a unit can stand and fight on carries 2
/// (x1). A river adds half a step to that terrain adjustment rather than a further
/// quarter on top of it.
/// </para>
/// <para>
/// The engine once divided the rules value by 2 as an <c>int</c>, which flattened
/// Forest, Jungle and Swamp from x1.5 to x1 and erased the half-steps -- finding C2
/// in <c>docs/CIV2-PARITY-DEEP-AUDIT.md</c>, the defensive half of tracker TR4
/// (issue #28). <see cref="Model.Core.Mapping.Tile.Defense"/> now keeps the
/// half-steps; this class states the Civ II figure they are checked against.
/// </para>
/// </summary>
public static class TerrainDefenseParity
{
    /// <summary>
    /// The rules.txt terrain defence value (in half-steps) Civ II carries for a
    /// terrain, matching the <c>@TERRAIN</c> block of RULES.txt.
    /// </summary>
    public static int RulesValue(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => 3,
        TerrainType.Jungle => 3,
        TerrainType.Swamp => 3,
        TerrainType.Hills => 4,
        TerrainType.Mountains => 6,
        _ => 2,
    };

    /// <summary>
    /// The terrain-only defensive multiplier Civ II applies to a unit standing on a
    /// square: the rules value counted in half-steps. Forest, Jungle and Swamp are
    /// x1.5, not the x1 an integer division flattened them to.
    /// </summary>
    public static decimal Multiplier(TerrainType terrain) => RulesValue(terrain) / 2m;

    /// <summary>
    /// The defensive multiplier including a river on the square. Civ II adds the
    /// river's half step to the terrain adjustment before that total multiplies with
    /// anything else, not as a further x1.25 on top of it.
    /// </summary>
    public static decimal MultiplierWithRiver(TerrainType terrain, bool river) =>
        Multiplier(terrain) + (river ? 0.5m : 0m);
}
