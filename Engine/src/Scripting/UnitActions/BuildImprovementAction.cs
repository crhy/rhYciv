using RhyCiv.Engine.Terrains;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine.Scripting.UnitActions;

/// <summary>
/// A settler or engineer set to work on the square it is standing on.
/// <para>
/// The computer players had no way to improve any terrain at all: their settlers
/// founded cities and then wandered, so their land was never irrigated, mined or
/// roaded and their pollution was never cleaned. This is the same path the
/// player's Build order takes -- progress is pooled with anyone else working the
/// square, and the improvement appears once enough turns of work have gone in.
/// </para>
/// </summary>
public class BuildImprovementAction(Unit baseUnit, TerrainImprovement improvement, Game game)
    : FullTurnAction(baseUnit, "BuildImprovement", game)
{
    protected override void DoAction()
    {
        BaseUnit.TransferConstructionProgress(improvement);
        BaseUnit.Build(improvement);
        game.CheckConstruction(BaseUnit.CurrentLocation, improvement);
    }
}
