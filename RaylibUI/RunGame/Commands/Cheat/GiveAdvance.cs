using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.IO;
using Model.Controls;
using Model.Core;
using Model.Input;
using Model.Interface;
using RaylibUI.Dialogs;

namespace RaylibUI.RunGame.Commands.Cheat;

/// <summary>
/// Hands the player an advance without researching it.
/// <para>
/// The Cheat menu has offered this since the menus were written and nothing was
/// behind it. Only what the civilisation could research next is on the list, so a
/// granted advance always has its prerequisites behind it and the tree stays
/// consistent -- the same rule the research chooser follows.
/// </para>
/// </summary>
public class GiveAdvance(GameScreen gameScreen)
    : AlwaysOnCommand(gameScreen, CommandIds.CheatTechnologyAdvance, [new Shortcut(Key.F6, shift: true)])
{
    private CivDialog? _dialog;

    public override void Action()
    {
        var civilization = GameScreen.Player.Civilization;
        var options = AdvanceFunctions.CalculateAvailableResearch(GameScreen.Game, civilization);

        _dialog = new CivDialog(GameScreen.Main, new DialogElements(new PopupBox
        {
            Title = "Technology Advance",
            Text = options.Count > 0
                ? ["Choose an advance to grant."]
                : ["There is nothing left to research."],
            LineStyles = [TextStyles.LeftOwnLine],
            Options = options.Select(advance => advance.Name).ToArray(),
            Button = options.Count > 0 ? [Labels.Ok, Labels.Cancel] : [Labels.Ok]
        }), (button, selection, _, _) =>
        {
            GameScreen.CloseDialog(_dialog);

            if (button != Labels.Ok || selection < 0 || selection >= options.Count)
            {
                return;
            }

            GameScreen.Game.GiveAdvance(options[selection].Index, civilization);
            GameScreen.Player.NotifyAdvanceResearched(options[selection].Index);
            GameScreen.StatusPanel.Update();
        });

        GameScreen.ShowDialog(_dialog);
    }
}
