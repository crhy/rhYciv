using RhyCiv.UI.Classic.Rules;
using RhyCiv.Engine.NewGame;
using Model.Controls;
using Model.ImageSets;
using Model.InterfaceActions;
using RaylibUtils;

namespace RhyCiv.UI.Classic.Dialogs.NewGame;

public class Init : BaseDialogHandler
{
    public const string Title = "INIT";
    public Init() : base(Title)
    {
    }

    public override IInterfaceAction Show(ClassicInterface activeInterface)
    {
        // Compact maps backgroundImageSmall1 to the full 1280x520 panel wallpaper;
        // CompatAlternate maps it to a 64x64 Civ2 tile. Only the tile belongs beside
        // the intro text -- the wallpaper made the dialog ~1900px wide and crushed
        // the prose into a single column (#152).
        var introArt = activeInterface.PicSources["backgroundImageSmall1"][0];
        Dialog.Image = Images.GetImageWidth(introArt, activeInterface) <= 256
            ? new(introArt)
            : null;

        var config = Initialization.ConfigObject;
        if (config.PlayerCiv.Id >= Initialization.ConfigObject.Civilizations.Count)
        {
            var correctColour = activeInterface.PlayerColours[config.PlayerCiv.Id];
            var correctIndex = Initialization.ConfigObject.Civilizations.Count - 1;
            activeInterface.PlayerColours[config.PlayerCiv.Id] = activeInterface.PlayerColours[correctIndex];
            activeInterface.PlayerColours[correctIndex] = correctColour;
            config.PlayerCiv.Id = correctIndex;
        }
        var maps = config.MapTask.Result;
        Initialization.GameInstance = NewGameInitialisation.StartNewGame(config, maps, config.Civilizations.OrderBy(c=>c.Id).ToList(), activeInterface.MainApp.ActiveRuleSet.Paths);
        
        var playerCiv = Initialization.GameInstance.GetPlayerCiv;
        Dialog.ReplaceStrings = new List<string>
        {   playerCiv.LeaderName,
            playerCiv.TribeName, ""
        };
        return base.Show(activeInterface);
    }

    public override IInterfaceAction HandleDialogResult(DialogResult result, Dictionary<string, ICivDialogHandler> civDialogHandlers, ClassicInterface civ2Interface)
    {
        return new StartGame(Initialization.GameInstance, Initialization.ViewData);
    }
}