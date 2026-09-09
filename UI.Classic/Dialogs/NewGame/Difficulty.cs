using RhyCiv.UI.Classic.Dialogs.FileDialogs;
using RhyCiv.UI.Classic.Dialogs.Scenario;
using RhyCiv.UI.Classic.Rules;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using Model.Controls;
using Model.Interface;
using Model.InterfaceActions;

namespace RhyCiv.UI.Classic.Dialogs.NewGame;

public class DifficultyHandler : BaseDialogHandler
{
    public const string Title = "DIFFICULTY";
    
    public DifficultyHandler() : base(Title, 0.085, -0.03)
    {
    }

    public override IInterfaceAction Show(ClassicInterface activeInterface)
    {
        var config = Initialization.ConfigObject;

        if (config.IsScenario)
        {
            Dialog.Options.SelectedId = config.DifficultyLevel;
        }
        else if (Settings.NewGameChoice(Title) is { } remembered &&
                 Dialog.Options is { Texts.Count: > 0 } options)
        {
            // Whatever was played last time. A scenario says what it wants and is
            // left alone.
            options.SelectedId = Math.Clamp(remembered, 0, options.Texts.Count - 1);
        }

        return base.Show(activeInterface);
    }

    public override IInterfaceAction HandleDialogResult(DialogResult result,
        Dictionary<string, ICivDialogHandler> civDialogHandlers, ClassicInterface civ2Interface)
    {
        var config = Initialization.ConfigObject;

        if (result.SelectedButton == Labels.Cancel)
        {
            return config.IsScenario ?
                civDialogHandlers[LoadScenario.DialogTitle].Show(civ2Interface) :
                civDialogHandlers[MainMenu.Title].Show(civ2Interface);
        }

        config.DifficultyLevel = result.SelectedIndex;
        if (!config.IsScenario)
        {
            Settings.RememberNewGameChoice(Title, result.SelectedIndex);
        }

        return config.IsScenario ?
            civDialogHandlers[SelectGender.Title].Show(civ2Interface) :
            civDialogHandlers[NoOfCivs.Title].Show(civ2Interface);
    }
}