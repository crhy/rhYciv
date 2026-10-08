using System.Diagnostics;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.UnitActions;
using JetBrains.Annotations;
using Model;
using Model.Constants;
using Model.Core;
using Model.Controls;
using Model.Input;

namespace RaylibUI.RunGame.Commands.Orders;

[UsedImplicitly]
public class FortifyOrder(GameScreen gameScreen) : Order(gameScreen, new Shortcut(Key.F), CommandIds.FortifyOrder)
{
    private readonly IGame _game = gameScreen.Game;
    private readonly LocalPlayer _player = gameScreen.Player;

    public override bool Update()
    {
        var activeUnit = _player.ActiveUnit;
        if (activeUnit == null || activeUnit.AiRole == AiRoleType.Settle)
        {
            return SetCommandState(CommandStatus.Invalid);
        }

        var canFortifyHere = UnitFunctions.CanFortifyHere(activeUnit, _player.ActiveTile);
        return SetCommandState(canFortifyHere ? CommandStatus.Normal : CommandStatus.Disabled);
    }

    public override void Action()
    {
        var activeUnit = _player.ActiveUnit;
        if (activeUnit == null)
        {
            return;
        }

        activeUnit.Order = (int)OrderType.Fortify;
        activeUnit.MovePointsLost = activeUnit.MaxMovePoints;
        _game.ChooseNextUnit();
    }
}