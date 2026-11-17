using System.Collections.Generic;
using System.Linq;
using Model.Core;
using RhyCiv.Engine.Advances;

namespace RhyCiv.Engine.Diplomacy;

/// <summary>
/// One line of the Foreign Advisor's table: what this civilisation knows about
/// another.
/// <para>
/// Civ II's advisor screens put the whole of diplomacy in front of the player at
/// once rather than one question at a time, and every number on this line was
/// already carried in a save. Until now the only place any of it was ever shown
/// was the one line the Foreign Ministry writes when you sit down at a table with
/// somebody (docs/CIV2-COMPARISON.md, #174).
/// </para>
/// </summary>
public class ForeignAdvisorRow
{
    /// <summary>The people, as the ministry names them.</summary>
    public string CivName { get; set; } = string.Empty;

    /// <summary>Who sits at their end of the table.</summary>
    public string LeaderName { get; set; } = string.Empty;

    /// <summary>
    /// How their court regards you, in Civ II's own nine ranks.
    /// <para>
    /// Their opinion of you, not yours of them: it is the number that decides
    /// whether their side of the table will accept anything you put on it
    /// (<see cref="DiplomacyFunctions.WouldAccept"/>), which is what a player
    /// reading this line wants to know.
    /// </para>
    /// </summary>
    public string Attitude { get; set; } = string.Empty;

    /// <summary>
    /// The treaty in force, named as the relation carries it.
    /// <para>
    /// Read in the order the relation is written: a war, then a vendetta left
    /// by a broken treaty, then a cease-fire, then a peace, then an alliance.
    /// An alliance is signed on top of a peace, so a pair holding one is named
    /// by the peace beneath it -- the same thing the ministry's own line says.
    /// </para>
    /// </summary>
    public string Treaty { get; set; } = "None";

    /// <summary>Whether their affairs can be seen from here at all.</summary>
    public bool Embassy { get; set; }

    /// <summary>Whether the two have ever met.</summary>
    public bool Met { get; set; }

    /// <summary>Advances they hold that you do not.</summary>
    public int HoldsThatWeLack { get; set; }

    /// <summary>Advances you hold that they do not.</summary>
    public int WeHoldThatTheyLack { get; set; }

    /// <summary>
    /// What it would be worth putting to them, in the words the audience uses
    /// for the same proposals.
    /// </summary>
    public string Offers { get; set; } = string.Empty;

    /// <summary>
    /// Two lines are the same line when every column of them agrees.
    /// <para>
    /// Asked for because the report has to be safe to read twice: the same
    /// question asked of a game that has not changed gives back the same table.
    /// </para>
    /// </summary>
    public override bool Equals(object? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is not ForeignAdvisorRow row)
        {
            return false;
        }

        return CivName == row.CivName && LeaderName == row.LeaderName && Attitude == row.Attitude &&
               Treaty == row.Treaty && Embassy == row.Embassy && Met == row.Met &&
               HoldsThatWeLack == row.HoldsThatWeLack && WeHoldThatTheyLack == row.WeHoldThatTheyLack &&
               Offers == row.Offers;
    }

    /// <summary>Agrees with <see cref="Equals"/> over the same columns.</summary>
    public override int GetHashCode() =>
        (CivName, LeaderName, Attitude, Treaty, Embassy, Met, HoldsThatWeLack, WeHoldThatTheyLack, Offers)
            .GetHashCode();
}

/// <summary>
/// The Foreign Advisor's table: one line per civilisation this one might deal
/// with.
/// <para>
/// Every column of it was already in the save format and read by nothing but Lua.
/// This is the report half of the screen the owner calls the most conspicuous gap
/// in the game (#174); the trigger for the unused tribute and threat dialogs, and
/// any notion of territory, are still open.
/// </para>
/// </summary>
public static class ForeignAdvisorReport
{
    /// <summary>
    /// The table, as one civilisation sees it.
    /// <para>
    /// Read-only on purpose: it goes through
    /// <see cref="DiplomacyFunctions.StandingBetween"/> rather than
    /// <see cref="DiplomacyFunctions.Between"/>, because asking what stands
    /// between two civilisations must not sign something between them, and it
    /// walks the rules' advances once per pair rather than anything per tile.
    /// The one thing it leaves behind is Civ II's own attitude table growing to
    /// fit a civilisation, which changes no answer the table gives.
    /// </para>
    /// </summary>
    public static List<ForeignAdvisorRow> ForeignAdvisorRows(IGame game, Civilization viewer)
    {
        var rows = new List<ForeignAdvisorRow>();

        foreach (var other in game.AllCivilizations.OrderBy(civ => civ.Id))
        {
            if (other == viewer || !other.Alive)
            {
                continue;
            }

            // The horde is not a court. It signs nothing, is never met and holds
            // no opinion, which is why the Foreign Ministry's list leaves it out
            // too; a line for it would be a line of nothing.
            if (other.PlayerType == PlayerType.Barbarians)
            {
                continue;
            }

            rows.Add(Row(game, viewer, other));
        }

        return rows;
    }

    private static ForeignAdvisorRow Row(IGame game, Civilization viewer, Civilization other)
    {
        // Their side of the relation: what they think of you, and what you have
        // signed with them. The embassy is one-way, so it is read from the viewer
        // looking out, exactly as HasEmbassyWith reads it.
        var relation = DiplomacyFunctions.StandingBetween(viewer, other);

        var holds = 0;
        var weHold = 0;
        foreach (var advance in game.Rules.Advances)
        {
            var theirs = AdvanceFunctions.HasTech(other, advance.Index);
            var ours = AdvanceFunctions.HasTech(viewer, advance.Index);
            if (theirs && !ours)
            {
                holds++;
            }
            else if (ours && !theirs)
            {
                weHold++;
            }
        }

        return new ForeignAdvisorRow
        {
            CivName = other.TribeName,
            LeaderName = other.LeaderName,
            Attitude = DiplomacyFunctions.AttitudeName(other, viewer),
            Treaty = TreatyName(relation),
            Embassy = relation?.Embassy ?? false,
            Met = relation?.Contact ?? false,
            HoldsThatWeLack = holds,
            WeHoldThatTheyLack = weHold,
            Offers = string.Join(", ", DiplomacyFunctions
                .AvailableProposals(relation ?? new Relation())
                .Select(Word))
        };
    }

    /// <summary>
    /// The treaty in force, in the order the relation is read.
    /// <para>
    /// A war comes first because it is what is actually happening; a vendetta
    /// next because it is the grudge a broken treaty left behind; then the three
    /// agreements, least binding first.
    /// </summary>
    private static string TreatyName(Relation? relation) => relation switch
    {
        null => "None",
        { War: true } => "War",
        { Vendetta: true } => "Vendetta",
        { CeaseFire: true } => "CeaseFire",
        { Peace: true } => "Peace",
        { Alliance: true } => "Alliance",
        _ => "None"
    };

    /// <summary>
    /// A proposal as it reads in a line of the report.
    /// <para>
    /// The words the audience already puts on its buttons for the same proposals,
    /// so the table and the negotiation name the same things.
    /// </para>
    /// </summary>
    private static string Word(DiplomacyFunctions.Proposal proposal) => proposal switch
    {
        DiplomacyFunctions.Proposal.CeaseFire => "cease-fire",
        DiplomacyFunctions.Proposal.Peace => "peace",
        DiplomacyFunctions.Proposal.Alliance => "alliance",
        DiplomacyFunctions.Proposal.DeclareWar => "war",
        DiplomacyFunctions.Proposal.GiveGold => "gold",
        _ => "knowledge"
    };
}
