using RhyCiv.UI.Classic.Dialogs.NewGame.CustomWorldDialogs;
using RhyCiv.UI.Classic.Rules;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using Model.Controls;
using Model.InterfaceActions;

namespace RhyCiv.UI.Classic.Dialogs.NewGame;

public class SelectRules : BaseDialogHandler
{
    public const string Title = "RULES";

    public SelectRules() : base(Title, -0.085, -0.03)
    {
    }

    public override IInterfaceAction HandleDialogResult(DialogResult result,
        Dictionary<string, ICivDialogHandler> civDialogHandlers, ClassicInterface civ2Interface)
    {
        if (result.SelectedButton == Labels.Cancel)
        {
            return civDialogHandlers[MainMenu.Title].Show(civ2Interface);
        }

        return civDialogHandlers[SelectGender.Title].Show(civ2Interface);
    }
}