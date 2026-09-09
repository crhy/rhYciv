using JetBrains.Annotations;
using Model;
using Model.Controls;
using Model.Images;
using Model.Input;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// The View menu's pair of mode switches.
/// <para>
/// Both entries have been in the menu since it was written with nothing behind
/// them. The modes themselves were only reachable by clicking the side panel or
/// by running a unit out of movement, so a player who wanted to look around the
/// map without giving orders had no way to ask for it.
/// </para>
/// </summary>
[UsedImplicitly]
public class MovePieces(GameScreen gameScreen) : IGameCommand
{
    public string Id => CommandIds.MovePieces;

    public Shortcut[] ActivationKeys { get; set; } = [new(Key.V)];

    public CommandStatus Status { get; private set; }

    public bool Update()
    {
        // Nothing to move during another civilisation's turn, and nothing to
        // switch to when the game is already waiting for orders.
        Status = gameScreen.ActiveMode == gameScreen.Processing || gameScreen.ActiveMode == gameScreen.Moving
            ? CommandStatus.Disabled
            : CommandStatus.Normal;
        return Status != CommandStatus.Disabled;
    }

    public void Action()
    {
        if (gameScreen.Player.ActiveUnit is { Dead: false, TurnEnded: false })
        {
            gameScreen.ActiveMode = gameScreen.Moving;
        }
        else
        {
            gameScreen.Game.ChooseNextUnit();
        }

        gameScreen.ForceRedraw();
    }

    public bool Checked => gameScreen.ActiveMode == gameScreen.Moving;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "Move Pieces";
}

[UsedImplicitly]
public class ViewPieces(GameScreen gameScreen) : IGameCommand
{
    public string Id => CommandIds.ViewPieces;

    // The same key as Move Pieces, as in Civ II, where V swaps between the two.
    // Whichever mode the game is already in disables its own entry, so exactly one
    // of the pair answers the key at any moment.
    public Shortcut[] ActivationKeys { get; set; } = [new(Key.V)];

    public CommandStatus Status { get; private set; }

    public bool Update()
    {
        Status = gameScreen.ActiveMode == gameScreen.Processing || gameScreen.ActiveMode == gameScreen.ViewPiece
            ? CommandStatus.Disabled
            : CommandStatus.Normal;
        return Status != CommandStatus.Disabled;
    }

    public void Action()
    {
        gameScreen.ActiveMode = gameScreen.ViewPiece;
        gameScreen.ForceRedraw();
    }

    public bool Checked => gameScreen.ActiveMode == gameScreen.ViewPiece;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "View Pieces";
}
