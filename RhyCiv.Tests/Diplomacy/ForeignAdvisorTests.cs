using Model.Core;
using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Tests.TestFiles;

namespace RhyCiv.Tests.Diplomacy;

/// <summary>
/// The Foreign Advisor's table: one line per civilisation still on the board,
/// carrying what the ministry knows about that pair.
/// <para>
/// Every column of it has been carried in a save since the beginning and shown
/// to the player nowhere but the one line the Foreign Ministry writes when you
/// sit down at a table with somebody. This is the report half of the screen the
/// owner calls the most conspicuous gap in the game (#174).
/// </para>
/// <para>
/// The table is read, not written: it must not sign a treaty, open an embassy or
/// change an opinion on its way past, and asking the same question twice has to
/// give the same answer twice.
/// </para>
/// </summary>
public class ForeignAdvisorTests
{
    [Fact]
    public void OneLinePerCivilisationStillOnTheBoard()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var viewer = game.GetPlayerCiv;

        // The clean-room board carries two civilisations. Three more are put on it
        // here in code -- a living neighbour, one whose time has gone, and the
        // horde -- so the report has something to leave out of the board for each
        // of the three reasons it leaves something out.
        var neighbour = new Civilization
        {
            Id = 3, TribeName = "Romans", Adjective = "Roman", LeaderName = "Octavia",
            Alive = true, PlayerType = PlayerType.Ai
        };
        var gone = new Civilization
        {
            Id = 4, TribeName = "Greeks", Adjective = "Greek", LeaderName = "Hippolyta",
            Alive = false, PlayerType = PlayerType.Ai
        };
        var horde = Barbarians.Civilization;
        game.AllCivilizations.AddRange([neighbour, gone, horde]);

        var expected = game.AllCivilizations
            .Where(civ => civ != viewer && civ.Alive && civ.PlayerType != PlayerType.Barbarians)
            .OrderBy(civ => civ.Id)
            .ToList();

        var rows = ForeignAdvisorReport.ForeignAdvisorRows(game, viewer);

        // Two lines, in Id order: the opponent and the neighbour. Not yourself,
        // not the civilisation that has been wiped out, not the horde.
        Assert.Equal(2, rows.Count);
        Assert.Equal(expected.Select(civ => civ.TribeName).ToList(), rows.Select(row => row.CivName).ToList());
        Assert.Equal(expected.Select(civ => civ.LeaderName).ToList(), rows.Select(row => row.LeaderName).ToList());
        Assert.DoesNotContain(rows, row => row.CivName == gone.TribeName);
        Assert.DoesNotContain(rows, row => row.CivName == horde.TribeName);
        Assert.DoesNotContain(rows, row => row.CivName == viewer.TribeName);
    }

    [Fact]
    public void TheTreatyColumnNamesWhatStandsBetweenThem()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var viewer = game.GetPlayerCiv;
        var them = game.AllCivilizations
            .First(civ => civ != viewer && civ.Alive && civ.PlayerType != PlayerType.Barbarians);

        DiplomacyFunctions.DeclareWar(game, viewer, them);
        Assert.Equal("War", RowFor(game, viewer, them).Treaty);

        DiplomacyFunctions.AgreeCeaseFire(game, viewer, them);
        Assert.Equal("CeaseFire", RowFor(game, viewer, them).Treaty);

        DiplomacyFunctions.AgreePeace(viewer, them);
        Assert.Equal("Peace", RowFor(game, viewer, them).Treaty);

        // An alliance is signed on top of a peace, and the column names the treaty
        // that comes first in the order the relation is read -- the same thing the
        // ministry's own line says about the pair.
        DiplomacyFunctions.FormAlliance(viewer, them);
        Assert.True(DiplomacyFunctions.Between(viewer, them).Alliance);
        Assert.Equal("Peace", RowFor(game, viewer, them).Treaty);

        // The grudge a broken treaty leaves behind, once nothing else stands.
        var relation = DiplomacyFunctions.Between(viewer, them);
        relation.War = false;
        relation.CeaseFire = false;
        relation.Peace = false;
        relation.Alliance = false;
        relation.Vendetta = true;
        Assert.Equal("Vendetta", RowFor(game, viewer, them).Treaty);

        relation.UpdateFrom(0);
        Assert.Equal("None", RowFor(game, viewer, them).Treaty);
    }

    [Fact]
    public void TheAttitudeColumnIsTheRankTheirCourtIsAddressedBy()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var viewer = game.GetPlayerCiv;
        var them = game.AllCivilizations
            .First(civ => civ != viewer && civ.Alive && civ.PlayerType != PlayerType.Barbarians);

        Assert.Equal(DiplomacyFunctions.AttitudeName(them, viewer), RowFor(game, viewer, them).Attitude);
        var before = RowFor(game, viewer, them).Attitude;

        DiplomacyFunctions.AdjustAttitude(them, viewer, -40);
        var row = RowFor(game, viewer, them);

        Assert.Equal(DiplomacyFunctions.AttitudeName(them, viewer), row.Attitude);
        Assert.NotEqual(before, row.Attitude);
    }

    [Fact]
    public void AnEmbassyShowsOnTheSideThatOpenedIt()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var viewer = game.GetPlayerCiv;
        var them = game.AllCivilizations
            .First(civ => civ != viewer && civ.Alive && civ.PlayerType != PlayerType.Barbarians);

        Assert.False(RowFor(game, viewer, them).Embassy);

        DiplomacyFunctions.EstablishEmbassy(viewer, them);

        var row = RowFor(game, viewer, them);
        Assert.True(row.Embassy);
        Assert.True(row.Met);

        // One-way, read the way HasEmbassyWith reads it: their report of you does
        // not gain the embassy you opened on them.
        var reverse = ForeignAdvisorReport.ForeignAdvisorRows(game, them);
        Assert.False(reverse.Single(r => r.CivName == viewer.TribeName).Embassy);
    }

    [Fact]
    public void TheKnowledgeCountsMoveOnlyWhenOneSideKnowsMore()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var viewer = game.GetPlayerCiv;

        // Two civilisations holding the same advances: nothing to report either
        // way, whatever the board has already taught them.
        var them = game.AllCivilizations
            .First(civ => civ != viewer && civ.Alive && civ.PlayerType != PlayerType.Barbarians &&
                         civ.Advances.SequenceEqual(viewer.Advances));

        var before = RowFor(game, viewer, them);
        Assert.Equal(0, before.HoldsThatWeLack);
        Assert.Equal(0, before.WeHoldThatTheyLack);

        var advance = rules.Advances.First(a => a.Index >= 0 &&
                                               !AdvanceFunctions.HasTech(viewer, a.Index) &&
                                               !AdvanceFunctions.HasTech(them, a.Index));

        game.GiveAdvance(advance.Index, them);
        Assert.True(AdvanceFunctions.HasTech(them, advance.Index),
            "The grant should have landed; a prohibited advance would leave the count where it was.");

        var after = RowFor(game, viewer, them);
        Assert.True(after.HoldsThatWeLack > 0,
            "A civilisation taught something you do not hold should show up in the report.");
        Assert.Equal(0, after.WeHoldThatTheyLack);
    }

    [Fact]
    public void ReadingTheReportSignsNothingAndSaysTheSameThingTwice()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var viewer = game.GetPlayerCiv;

        var first = ForeignAdvisorReport.ForeignAdvisorRows(game, viewer);
        var second = ForeignAdvisorReport.ForeignAdvisorRows(game, viewer);

        Assert.Equal(first, second);

        // A pair that has never dealt stays a pair that has never dealt: asking
        // what the treaty is must not sign one, which is why the report reads
        // through StandingBetween rather than Between.
        var stranger = game.AllCivilizations
            .First(civ => civ != viewer && civ.Alive && civ.PlayerType != PlayerType.Barbarians);
        Assert.Null(DiplomacyFunctions.StandingBetween(viewer, stranger));
        Assert.Equal("None", RowFor(game, viewer, stranger).Treaty);
        Assert.False(RowFor(game, viewer, stranger).Met);
        Assert.Null(DiplomacyFunctions.StandingBetween(viewer, stranger));
    }

    private static ForeignAdvisorRow RowFor(IGame game, Civilization viewer, Civilization other) =>
        ForeignAdvisorReport.ForeignAdvisorRows(game, viewer)
            .Single(row => row.CivName == other.TribeName);
}
