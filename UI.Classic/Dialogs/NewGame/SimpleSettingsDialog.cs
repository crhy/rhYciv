using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using Model.Controls;
using Model.InterfaceActions;

namespace RhyCiv.UI.Classic.Dialogs.NewGame;

/// <summary>
/// A new-game question that is a plain list of options.
/// <para>
/// Each one remembers the answer it was given last time and opens on it. Starting
/// a game asks a dozen of these and every one of them used to open on its own
/// default, so a player who always wants the same thing had to say so every game.
/// </para>
/// </summary>
public abstract class SimpleSettingsDialog : BaseDialogHandler
{
    protected SimpleSettingsDialog(string name, double x = 0, double y = 0) : base(name, x, y)
    {
    }

    public override IInterfaceAction Show(ClassicInterface activeInterface)
    {
        if (Dialog.Options is { Texts.Count: > 0 } options &&
            Settings.NewGameChoice(Dialog.Name ?? string.Empty) is { } remembered)
        {
            options.SelectedId = Math.Clamp(remembered, 0, options.Texts.Count - 1);
        }

        return base.Show(activeInterface);
    }

    public override IInterfaceAction HandleDialogResult(DialogResult result,
        Dictionary<string, ICivDialogHandler> civDialogHandlers, ClassicInterface civ2Interface)
    {
        if (result.SelectedButton == Labels.Cancel)
        {
            return civDialogHandlers[MainMenu.Title].Show(civ2Interface);
        }

        Settings.RememberNewGameChoice(Dialog.Name ?? string.Empty, result.SelectedIndex);

        //var popupBox = civDialogHandlers[Dialog.Name];
        var next = SetConfigValue(result, Dialog);

        return civDialogHandlers[next].Show(civ2Interface);
    }

    protected abstract string SetConfigValue(DialogResult result, DialogElements? popupBox);

}
