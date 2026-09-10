using JetBrains.Annotations;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using Model;
using Model.Controls;
using Model.Images;
using Model.Input;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// The settings that are not part of ordinary play: a rule the player opts into,
/// and the two development menus.
/// <para>
/// All three are off until they are asked for, and all three are remembered
/// between sessions, so turning the cheat menu on once is enough. Global warming
/// is the one that changes the game itself -- pollution left on the map long
/// enough will start changing terrain -- so it is not something to have happen to
/// somebody who has not chosen it.
/// </para>
/// </summary>
[UsedImplicitly]
public class AdvancedSettings(GameScreen gameScreen) : IGameCommand
{
    public string Id => CommandIds.AdvancedSettings;

    public Shortcut[] ActivationKeys { get; set; } = [new(Key.A, ctrl: true)];

    public CommandStatus Status => CommandStatus.Normal;

    public bool Update()
    {
        return true;
    }

    public void Action()
    {
        gameScreen.ShowPopup("ADVANCEDSETTINGS", DialogClick,
            checkboxStates: (List<bool>)
            [
                Settings.GlobalWarmingEnabled, Settings.CheatMenuEnabled, Settings.EditorMenuEnabled,
                false
            ]);
    }

    private void DialogClick(string button, int _, IList<bool>? checkboxes, IDictionary<string, string>? _2)
    {
        if (button != Labels.Ok || checkboxes is not { Count: >= 4 })
        {
            return;
        }

        // Not a setting so much as a button that looks like one: ticking it puts
        // the tutorial back for somebody who wants to see it again, and it is
        // unticked the next time this dialog opens because it has already happened.
        if (checkboxes[3])
        {
            Settings.ForgetTutorials();
        }

        var menusChanged = Settings.CheatMenuEnabled != checkboxes[1] ||
                           Settings.EditorMenuEnabled != checkboxes[2];

        Settings.SetAdvancedSettings(checkboxes[0], checkboxes[1], checkboxes[2]);

        if (menusChanged)
        {
            gameScreen.RebuildMenus();
        }
    }

    public bool Checked => false;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "Advanced Settings";
}
