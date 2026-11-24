using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Advances;
using Model.Core.GameRules;

namespace RhyCiv.Tests.Advances;

/// <summary>
/// Philosophy: the first civilisation to discover it carries two advances away
/// with it for nothing, and every other civilisation that is beaten to it is
/// told somebody was. Both halves are driven through the real completed-advance
/// path (<c>GameTurn.CitiesTurn</c> -> research resolution), so pulling the
/// first-discovery check out of the grant takes these tests down with it.
/// <para>
/// Philosophy itself is looked up by name in the shipped rules rather than by
/// <c>(int)AdvanceType.Philosophy</c>: the legacy enum numbers it 60, but the
/// rules keep their own order (the same trap as #169's Monarchy).
/// </para>
/// </summary>
public class PhilosophyFreeAdvancesTests
{
    [Fact]
    public void TheFirstCivToDiscoverPhilosophy_GainsTwoMoreAdvances()
    {
        var (game, civ, player, rules, philosophy) = AtTheResearchTable();
        var before = Snapshot(civ);
        var owed = TwoCheapest(rules, civ, philosophy);
        Assert.Equal(2, owed.Count);

        civ.ReseachingAdvance = philosophy;
        civ.Science = ResearchEnough;
        game.CitiesTurn(player);

        Assert.True(AdvanceFunctions.HasTech(civ, philosophy));
        Assert.Equal(new[] { philosophy }.Concat(owed).OrderBy(i => i), Gained(civ, before));
    }

    [Fact]
    public void ACivDiscoveringPhilosophyAfterSomebodyElse_GainsOnlyItself()
    {
        var (game, civ, player, rules, philosophy) = AtTheResearchTable();

        // A rival has the discovery first; it is not barred from it either,
        // which is what the grant's first-discovery guard reads.
        var rival = game.AllCivilizations.First(other =>
            other.Id != civ.Id && other.Alive && other.PlayerType != PlayerType.Barbarians);
        game.GiveAdvance(philosophy, rival);
        Assert.True(AdvanceFunctions.HasTech(rival, philosophy));

        var before = Snapshot(civ);
        civ.ReseachingAdvance = philosophy;
        civ.Science = ResearchEnough;
        game.CitiesTurn(player);

        Assert.True(AdvanceFunctions.HasTech(civ, philosophy));
        Assert.Equal(new[] { philosophy }, Gained(civ, before));
    }

    [Fact]
    public void TheFreeAdvances_AreNeverOnesTheCivIsBarredFrom()
    {
        var (game, civ, player, rules, philosophy) = AtTheResearchTable();

        // Move the cheapest advance the civ would have taken into a group of
        // its own and bar that group: the grant has to step past it to the next
        // cheapest, and every advance it does take must be a legal one.
        var unbarred = TwoCheapest(rules, civ, philosophy);
        var banned = rules.Advances.First(advance =>
            advance.Index != philosophy && !AdvanceFunctions.HasTech(civ, advance.Index));
        rules.Advances[banned.Index].AdvanceGroup = 1;
        civ.AllowedAdvanceGroups = [AdvanceGroupAccess.CanResearch, AdvanceGroupAccess.Prohibited];

        var expected = TwoCheapest(rules, civ, philosophy);
        Assert.DoesNotContain(banned.Index, expected);
        Assert.NotEqual(unbarred, expected);

        var before = Snapshot(civ);
        civ.ReseachingAdvance = philosophy;
        civ.Science = ResearchEnough;
        game.CitiesTurn(player);

        Assert.Equal(new[] { philosophy }.Concat(expected).OrderBy(i => i), Gained(civ, before));
        foreach (var gained in Gained(civ, before))
        {
            Assert.NotEqual(AdvanceGroupAccess.Prohibited,
                GroupAccess(civ, rules.Advances[gained].AdvanceGroup));
        }
    }

    [Fact]
    public void TheNotice_NamesTheDiscoverersOnlyOnceTheyAreMet()
    {
        var (game, discoverer, other) = SmallBoard();

        Assert.Null(game.PhilosophyDiscoveredNotice(other));

        game.GiveAdvance(PhilosophyOnSmallBoard, discoverer);

        Assert.Equal("Philosophy has been discovered in a faraway land.",
            game.PhilosophyDiscoveredNotice(other));

        DiplomacyFunctions.MakeContact(game, other, discoverer);

        Assert.Equal("The Serpents have discovered Philosophy.",
            game.PhilosophyDiscoveredNotice(other));
        Assert.Null(game.PhilosophyDiscoveredNotice(discoverer));
    }

    /// <summary>Science above any cost a civ at the start of a game can pay.</summary>
    private const int ResearchEnough = 100_000;

    /// <summary>
    /// A fresh shipped-ruleset game with the local civilisation at the research
    /// table: a city to pay for research, and Philosophy not yet known.
    /// </summary>
    private static (Game Game, Civilization Civ, MockPlayer Player, Rules Rules, int Philosophy)
        AtTheResearchTable()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var civ = game.GetPlayerCiv;

        Assert.Same(civ, game.GetActiveCiv);

        // Research is paid by cities, and the shipped opening has the local
        // civ on the road rather than in a town: settle one first.
        CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Research Seat");
        Assert.NotEmpty(civ.Cities);

        var philosophy = rules.Advances.First(advance => advance.Name == "Philosophy").Index;
        Assert.False(AdvanceFunctions.HasTech(civ, philosophy));
        Assert.NotEqual(AdvanceGroupAccess.Prohibited,
            GroupAccess(civ, rules.Advances[philosophy].AdvanceGroup));

        return (game, civ, player, rules, philosophy);
    }

    /// <summary>
    /// The two free advances, by the engine's own rule: research is paid per
    /// civilisation rather than per advance, so every advance costs the same
    /// and the cheapest two are the earliest in the rules that the civilisation
    /// does not know and is not barred from.
    /// </summary>
    private static List<int> TwoCheapest(Rules rules, Civilization civ, int philosophy) =>
        rules.Advances
            .Where(advance => advance.Index != philosophy &&
                              !AdvanceFunctions.HasTech(civ, advance.Index) &&
                              GroupAccess(civ, advance.AdvanceGroup) != AdvanceGroupAccess.Prohibited)
            .OrderBy(advance => advance.Index)
            .Take(AdvanceFunctions.PhilosophyFreeAdvances)
            .Select(advance => advance.Index)
            .ToList();

    private static AdvanceGroupAccess GroupAccess(Civilization civ, int group) =>
        group >= 0 && group < civ.AllowedAdvanceGroups.Length
            ? civ.AllowedAdvanceGroups[group]
            : AdvanceGroupAccess.CanResearch;

    private static bool[] Snapshot(Civilization civ) => (bool[])civ.Advances.Clone();

    // The knowledge array can grow while the advances arrive, so anything past
    // the old length counts as not known before.
    private static List<int> Gained(Civilization civ, bool[] before) =>
        Enumerable.Range(0, civ.Advances.Length)
            .Where(index => civ.Advances[index] && (index >= before.Length || !before[index]))
            .ToList();

    private const int PhilosophyOnSmallBoard = 4;

    private static (MockGame Game, Civilization Discoverer, Civilization Other) SmallBoard()
    {
        var advances = new[]
        {
            SmallAdvance("Alphabet", 0),
            SmallAdvance("Writing", 1, 0),
            SmallAdvance("Literacy", 2, 1),
            SmallAdvance("Bronze Working", 3),
            SmallAdvance("Philosophy", PhilosophyOnSmallBoard, 2, 3),
        };

        var discoverer = new Civilization
        {
            Id = 0,
            Alive = true,
            TribeName = "Serpents",
            Advances = new bool[advances.Length],
            AllowedAdvanceGroups = [AdvanceGroupAccess.CanResearch],
        };
        var other = new Civilization
        {
            Id = 1,
            Alive = true,
            TribeName = "Moths",
            Advances = new bool[advances.Length],
            AllowedAdvanceGroups = [AdvanceGroupAccess.CanResearch],
        };

        var game = new MockGame
        {
            // ProductionOrders set empty: granting an advance re-lists what the
            // civilisation can build from it, and this board has nothing to build.
            Rules = new Rules { Advances = advances, ProductionOrders = [] },
            AllCivilizations = [discoverer, other],
            Players = [new MockPlayer(discoverer), new MockPlayer(other)],
        };
        return (game, discoverer, other);
    }

    private static Advance SmallAdvance(string name, int index, int prereq1 = -1, int prereq2 = -1) =>
        new() { Name = name, Index = index, Prereq1 = prereq1, Prereq2 = prereq2, AdvanceGroup = 0 };
}
