using JetBrains.Annotations;
using RhyCiv.Engine;
using RhyCiv.Engine.Diagnostics;
using RhyCiv.Engine.IO;
using Model;
using Model.Controls;
using Model.Input;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// Overthrow the government.
/// <para>
/// The REVOLUTION entry has been in the Kingdom menu since the menus were
/// written, with no command behind it -- so it was drawn, and clicking it did
/// nothing. A civilisation was given Despotism when the game began and stayed
/// under it however far it researched.
/// </para>
/// </summary>
[UsedImplicitly]
public class Revolution(GameScreen gameScreen) : IGameCommand
{
    public string Id => CommandIds.Revolution;

    public Shortcut[] ActivationKeys { get; set; } = [new(Key.R, shift: true)];

    public CommandStatus Status { get; private set; }

    public bool Update()
    {
        // Greyed out rather than hidden when there is nothing to change to, so the
        // entry is where it will be once something has been researched.
        Status = GovernmentFunctions.CanRevolt(gameScreen.Player.Civilization)
            ? CommandStatus.Normal
            : CommandStatus.Disabled;
        return Status != CommandStatus.Disabled;
    }

    public void Action()
    {
        var civilization = gameScreen.Player.Civilization;
        if (!GovernmentFunctions.CanRevolt(civilization))
        {
            return;
        }

        gameScreen.ShowPopup("REVOLUTION", handleButtonClick: (button, _, _, _) =>
        {
            if (button != Labels.Ok)
            {
                return;
            }

            GovernmentFunctions.BeginRevolution(gameScreen.Game, civilization);
            SessionLog.Record("revolution declared");
            gameScreen.StatusPanel.Update();
            gameScreen.ForceRedraw();
        }, replaceStrings: [civilization.TribeName],
           replaceNumbers: [GovernmentFunctions.AnarchyTurns]);
    }

    public bool Checked => false;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "Revolution";
}
