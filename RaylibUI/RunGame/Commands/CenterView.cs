using JetBrains.Annotations;
using Model;
using Model.Controls;
using Model.Images;
using Model.Input;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// Brings the map back to whatever the game is waiting on -- the active unit, or
/// the cursor square when looking around.
/// <para>
/// Scrolling the map, whether by dragging it or by clicking the minimap, pins the
/// view to the square it was left on. There was no way to unpin it short of
/// selecting another unit, so a player who had gone looking at the far side of
/// the world had to hunt their way back.
/// </para>
/// </summary>
[UsedImplicitly]
public class CenterView(GameScreen gameScreen) : IGameCommand
{
    public string Id => CommandIds.CenterView;

    public Shortcut[] ActivationKeys { get; set; } = [new(Key.C)];

    public CommandStatus Status { get; private set; }

    public bool Update()
    {
        Status = gameScreen.ActiveMode == gameScreen.Processing ? CommandStatus.Disabled : CommandStatus.Normal;
        return Status != CommandStatus.Disabled;
    }

    public void Action()
    {
        gameScreen.SetViewAnchor(null);
    }

    public bool Checked => false;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "Center View";
}
