using RhyCiv.Engine.Enums;
using RhyCiv.Engine.UnitActions;
using JetBrains.Annotations;
using Model;
using Model.Controls;
using Model.Input;
using RaylibUI.RunGame.GameModes;

namespace RaylibUI.RunGame.Commands.Orders;

/// <summary>
/// Arms an airlift. The order itself only checks that the active unit is an air
/// unit sitting on its own airbase; the destination is the next city the player
/// clicks, which <see cref="MovingPieces"/> hands to the engine.
/// </summary>
[UsedImplicitly]
public class AirliftOrder(GameScreen gameScreen)
    : Order(gameScreen, new Shortcut(Key.L), CommandIds.AirliftOrder)
{
    public override bool Update()
    {
        var unit = GameScreen.Player.ActiveUnit;

        // The menu entry is omitted entirely for units that are not aircraft, so
        // it does not sit greyed out above every Settler in the game.
        if (unit is null || unit.Domain != UnitGas.Air)
        {
            return SetCommandState(CommandStatus.Invalid);
        }

        return SetCommandState(AirliftFunctions.CanAirlift(unit)
            ? CommandStatus.Normal
            : CommandStatus.Disabled);
    }

    public override void Action()
    {
        var unit = GameScreen.Player.ActiveUnit;
        if (unit is null || !AirliftFunctions.CanAirlift(unit))
        {
            return;
        }

        if (GameScreen.Moving is MovingPieces moving)
        {
            moving.AwaitingAirlift = unit;
        }
    }
}
