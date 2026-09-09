using RhyCiv.Engine;
using RhyCiv.Engine.Terrains;
using Model;
using Model.Constants;
using Model.Core;
using Model.Controls;
using Model.Core.Mapping;

namespace RaylibUI.RunGame.Commands.Orders;

public class ImprovementOrder(TerrainImprovement improvement, GameScreen gameScreen, IGame game)
    : Order(gameScreen, Shortcut.Parse(improvement.Shortcut), GetCommandName(improvement), MenuName(improvement))
{
    private readonly LocalPlayer _player = gameScreen.Player;

    public override bool Update()
    {
        if (_player.ActiveUnit == null)
        {
            return SetCommandState(CommandStatus.Invalid);
        }

        if (_player.ActiveUnit.AiRole != AiRoleType.Settle)
        {
            return SetCommandState(CommandStatus.Invalid, errorPopupKeyword: "ONLYSETTLERS");
        }

        var canBeBuilt = TerrainImprovementFunctions.CanImprovementBeBuiltHere(_player.ActiveTile, improvement, _player.ActiveUnit.Owner);

        return SetCommandState(canBeBuilt.Enabled ? CommandStatus.Normal : CommandStatus.Disabled, canBeBuilt.CommandTitle, canBeBuilt.ErrorPopup);
    }

    public override void Action()
    {
        _player.ActiveUnit?.TransferConstructionProgress(improvement);
        _player.ActiveUnit?.Build(improvement);
        game.CheckConstruction(_player.ActiveTile, improvement);
        game.ChooseNextUnit();
    }

    /// <summary>
    /// What the Orders menu calls this improvement before a settler is selected and
    /// <see cref="Update"/> has had a chance to say which level comes next.
    /// <para>
    /// The improvement's own name is not a menu entry: it would read "Build Mining"
    /// and, worse, "Build Pollution". The first level's build label is what Civ II
    /// puts on the menu -- "Build Mines", "Clear Pollution" -- so the entry says the
    /// same thing whether or not there is a unit to say it about.
    /// </para>
    /// </summary>
    private static string MenuName(TerrainImprovement improvement)
    {
        return improvement.Levels.Count > 0
            ? TerrainImprovementFunctions.LabelFrom(improvement.Levels[0])
            : improvement.Name;
    }

    private static string GetCommandName(TerrainImprovement improvement)
    {
        var baseId = CommandIds.BuildImprovementOrderNormal;
        if (improvement.Negative)
        {
            baseId = CommandIds.RemoveNegativeImprovementOrder;
        }else if (improvement.Foreground)
        {
            baseId = CommandIds.BuildImprovementOrderForeground;
        }
        return baseId + "_" + improvement.Name.ToUpperInvariant();
    }
}
