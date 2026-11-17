using System.Linq;
using RhyCiv.Engine.Diplomacy;
using JetBrains.Annotations;
using Model;
using Model.Controls;
using Model.Input;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// The Foreign Advisor: the whole table of relations at once, rather than one
/// question at a time across a throne room.
/// <para>
/// Civ II answers the two advisor questions on separate screens, and the foreign
/// one is a report: every civilisation still on the board, what it thinks of you,
/// what stands between you, whether your embassy sees into it, and what each side
/// knows that the other does not. Every one of those numbers was already carried
/// in a save and shown nowhere but the one line the Foreign Ministry writes when
/// you sit down with somebody, which is why the owner calls this screen the most
/// conspicuous gap in the game (#174).
/// </para>
/// <para>
/// It is a dialog rather than a window because that is what this client has: there
/// is no advisor windowing anywhere in it, and the report is the engine's own
/// <see cref="ForeignAdvisorReport"/>.
/// </para>
/// </summary>
[UsedImplicitly]
public class ForeignAdvisor(GameScreen gameScreen) : IGameCommand
{
    public string Id => CommandIds.ForeignAdvisor;

    public Shortcut[] ActivationKeys { get; set; } = [];
    public CommandStatus Status { get; private set; }

    public bool Update()
    {
        Status = CommandStatus.Normal;
        return true;
    }

    public void Action() => gameScreen.ShowPopup("FOREIGNADVISOR", replaceStrings: [Table()]);

    /// <summary>
    /// The table, one line per civilisation still on the board.
    /// <para>
    /// One line each, deliberately: the board holds twenty-one civilisations and a
    /// dialog that spends three lines on each of them is taller than the screen.
    /// The parenthetical is the Foreign Ministry's own -- what they think of you,
    /// the treaty in force, whether your embassy sees into them -- so the two
    /// screens read as the same account at different lengths.
    /// </para>
    /// </summary>
    private string Table()
    {
        var rows = ForeignAdvisorReport.ForeignAdvisorRows(gameScreen.Game, gameScreen.Player.Civilization);
        if (rows.Count == 0)
        {
            return "No other court stands on the board.";
        }

        return string.Join("\n", rows.Select(Line));
    }

    private static string Line(ForeignAdvisorRow row)
    {
        var embassy = row.Embassy ? "Embassy" : "No Embassy";
        var met = row.Met ? "Met" : "Never met";
        var knowledge = row.HoldsThatWeLack == 0 && row.WeHoldThatTheyLack == 0
            ? "both sides know the same"
            : $"they hold {row.HoldsThatWeLack} you lack, you hold {row.WeHoldThatTheyLack} they lack";
        var offers = string.IsNullOrEmpty(row.Offers) ? "nothing" : row.Offers;

        return $"{row.LeaderName} of the {row.CivName} ({row.Attitude}, {row.Treaty}, {embassy}, {met}) " +
               $"-- {knowledge}; open to {offers}.";
    }

    public bool Checked => false;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "Foreign Advisor";
}
