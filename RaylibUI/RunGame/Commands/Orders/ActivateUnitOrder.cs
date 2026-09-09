using JetBrains.Annotations;
using Model;
using Model.Controls;
using Model.Input;

namespace RaylibUI.RunGame.Commands.Orders;

/// <summary>
/// Gives orders to whatever is standing on the square the cursor is on.
/// <para>
/// Civ II's counterpart to clicking a unit, and the only way to pick up a unit
/// that has already been told to sleep or fortify without hunting for it with the
/// mouse. The menu entry existed with no command behind it.
/// </para>
/// </summary>
[UsedImplicitly]
public class ActivateUnitOrder(GameScreen gameScreen)
    : Order(gameScreen, new Shortcut(Key.A), CommandIds.ActivateUnitOrder, "Activate Unit")
{
    public override bool Update()
    {
        var tile = GameScreen.Game.ActivePlayer.ActiveTile;
        var anythingToActivate = tile.UnitsHere.Any(unit =>
            !unit.Dead && unit.Owner == GameScreen.Player.Civilization);

        return SetCommandState(anythingToActivate ? CommandStatus.Normal : CommandStatus.Invalid);
    }

    public override void Action()
    {
        GameScreen.ActivateUnits(GameScreen.Game.ActivePlayer.ActiveTile);
    }
}
