using System.Linq;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.UnitActions;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine.Scripting.UnitActions;

public class UnloadAction(Unit baseUnit, Tile possibleMove, Game game) : TileAction(baseUnit, possibleMove, "Unload", game)
{
    public override void Execute()
    {
        if (BaseUnit.CarriedUnits.Count == 0) return;
        var unitToMove = BaseUnit.CarriedUnits.First();
        MovementFunctions.ExecuteUnitMove(game, unitToMove, Tile, BaseUnit.CurrentLocation);
    }
}