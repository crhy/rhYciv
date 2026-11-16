using Model.Input;
using RhyCiv.Engine;
using RhyCiv.Engine.Diagnostics;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.SaveLoad;
using Model.Controls;
using JetBrains.Annotations;
using Model.Core;

namespace RaylibUI.RunGame.Commands;

[UsedImplicitly]
public class SaveGame(GameScreen gameScreen) : AlwaysOnCommand(gameScreen, CommandIds.SaveGame,
    [new Shortcut(Key.S, ctrl: true)])
{
    private FileDialog? _saveDialog;

    /// <summary>
    /// Whether the player has already been asked about replacing an existing save.
    /// <para>
    /// Answering that question Ok runs the write, and the write arrives through the
    /// same callback; without this the Ok would raise the question again forever.
    /// Cleared whenever the save dialog is opened or closed, so a later save to a
    /// taken name is asked about in its own right.
    /// </para>
    /// </summary>
    private bool _overwriteAnswered = false;

    public override void Action()
    {
        var suggestedFileName = SaveFileNames.Suggest(GameScreen.Game);
        _saveDialog = new FileDialog(GameScreen.Main, Labels.For(LabelIndex.SaveFiles),
            Settings.SaveGameFolder, IsValidSelectionCallback, OnSelectionCallback, suggestedFileName,
            false);
        _overwriteAnswered = false;
        GameScreen.ShowDialog(_saveDialog, true);
    }

    bool OnSelectionCallback(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            CloseSaveDialog();
            return true;
        }

        // A save already sitting at that name is not quietly thrown away. The player
        // is asked first, and nothing is written until they answer; Cancel leaves
        // the old file untouched and the save dialog open so another name can be
        // chosen.
        if (!_overwriteAnswered && SaveFileNames.Occupied(filePath))
        {
            AskAboutTheExistingSave(filePath);
            return false;
        }

        return WriteTheSave(filePath);
    }

    /// <summary>
    /// Puts the keep-or-replace question in front of the player. Ok writes the save;
    /// anything else does not.
    /// </summary>
    private void AskAboutTheExistingSave(string filePath)
    {
        if (!GameScreen.ShowPopup("SAVEOVERWRITE",
            handleButtonClick: (button, _, _, _) =>
            {
                if (button != Labels.Ok)
                {
                    return;
                }

                _overwriteAnswered = true;
                WriteTheSave(filePath);
            },
            replaceStrings: [Path.GetFileName(filePath)]))
        {
            // No prompt to ask with, so no answer is ever coming. Writing anyway
            // would destroy a save on the assumption that the player meant it, which
            // is exactly the thing this change exists to stop.
            SessionLog.Record($"not overwriting {Path.GetFileName(filePath)}: SAVEOVERWRITE is not in the game's text");
        }
    }

    /// <summary>
    /// The save itself: written through AtomicFile, then confirmed to the player.
    /// </summary>
    private bool WriteTheSave(string filePath)
    {
        var serializer = new GameSerializer();
        var game = GameScreen.Game;

        var viewData = new Dictionary<string, string> {{ "Zoom", GameScreen.Zoom.ToString() }};
        SessionLog.Record($"saving to {Path.GetFileName(filePath)}");
        // Never write straight to the destination: a save that fails part way
        // through must not destroy the save it was replacing. See AtomicFile.
        AtomicFile.Write(filePath,
            saveFile => serializer.Write(saveFile, game, GameScreen.Main.ActiveRuleSet!, viewData));

        GameScreen.ShowPopup("SAVEOK", replaceStrings: new List<string>{ game.ActivePlayer.Civilization.LeaderTitle, game.ActivePlayer.Civilization.LeaderName, game.ActivePlayer.Civilization.TribeName,Path.GetFileName(filePath)}, handleButtonClick: CloseConfirm);
        return true;
    }

    private void CloseConfirm(string arg1, int arg2, IList<bool>? arg3, IDictionary<string, string>? arg4)
    {
        CloseSaveDialog();
    }

    /// <summary>
    /// Takes the save dialog down, whichever way the player left it.
    /// </summary>
    private void CloseSaveDialog()
    {
        if (_saveDialog != null)
        {
            GameScreen.CloseDialog(_saveDialog);
            _saveDialog = null;
        }

        _overwriteAnswered = false;
    }

    private bool IsValidSelectionCallback(string path)
    {
        return path.EndsWith(".sav", StringComparison.InvariantCultureIgnoreCase);
    }
}
