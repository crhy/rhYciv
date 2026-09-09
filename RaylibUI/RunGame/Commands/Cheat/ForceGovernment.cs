using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using Model.Controls;
using Model.Core;
using Model.Input;
using Model.Interface;
using RaylibUI.Dialogs;

namespace RaylibUI.RunGame.Commands.Cheat;

/// <summary>
/// Puts the civilisation under a government without the revolution.
/// <para>
/// Useful for seeing what a government actually does to a city's output without
/// playing the several thousand years it takes to research one. Every government
/// in the ruleset is offered, including ones the civilisation has no right to --
/// that is the point of a cheat.
/// </para>
/// </summary>
public class ForceGovernment(GameScreen gameScreen)
    : AlwaysOnCommand(gameScreen, CommandIds.CheatForceGovernment, [new Shortcut(Key.F7, shift: true)])
{
    private CivDialog? _dialog;

    public override void Action()
    {
        var civilization = GameScreen.Player.Civilization;
        var governments = System.Enum.GetValues<GovernmentType>();

        _dialog = new CivDialog(GameScreen.Main, new DialogElements(new PopupBox
        {
            Title = "Force Government",
            Text = [$"The {civilization.TribeName} are under {GameScreen.Game.Rules.Governments[civilization.Government].Name}."],
            LineStyles = [TextStyles.LeftOwnLine],
            Options = governments.Select(government => GameScreen.Game.Rules.Governments[(int)government].Name).ToArray(),
            Button = [Labels.Ok, Labels.Cancel]
        }), (button, selection, _, _) =>
        {
            GameScreen.CloseDialog(_dialog);

            if (button != Labels.Ok || selection < 0 || selection >= governments.Length)
            {
                return;
            }

            GovernmentFunctions.AdoptGovernment(GameScreen.Game, civilization, governments[selection]);
            GameScreen.StatusPanel.Update();
            GameScreen.ForceRedraw();
        });

        GameScreen.ShowDialog(_dialog);
    }
}
