using RhyCiv.Engine.Advances;
using RhyCiv.Tests.Mocks;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;

namespace RhyCiv.Tests.Advances;

/// <summary>
/// The science cost scales with how far a civilisation trails the key civilisation,
/// so getting the key civilisation wrong -- reading a power rank as a list index --
/// silently prices research against an unrelated tribe.
/// </summary>
public class AdvanceFunctionsTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(24, 100, 0)]
    [InlineData(25, 100, 1)]
    [InlineData(50, 100, 2)]
    [InlineData(75, 100, 3)]
    [InlineData(100, 100, 3)]
    [InlineData(50, 0, 0)]
    [InlineData(-10, 100, 0)]
    public void CalculateResearchProgressQuarter_ReturnsCiv2IconBand(
        int progress, int cost, int expected)
    {
        Assert.Equal(expected,
            AdvanceFunctions.CalculateResearchProgressQuarter(progress, cost));
    }

    // Cost = (TechParadigm + one per three advances of lead over the key civilisation)
    // * (advances already held + 1). TechParadigm is the 10 the clean-room rules ship
    // with. Every row stays at or under 20 advances so the map-size term, which only
    // SetupTech fills in, never enters the arithmetic.
    [Theory]
    [InlineData(0, 0, 10)]   // level with the key: the paradigm alone
    [InlineData(0, 3, 9)]    // three behind: one off the paradigm
    [InlineData(0, 9, 7)]    // nine behind: three off it
    [InlineData(3, 0, 44)]   // three ahead, and four times the base
    [InlineData(6, 6, 70)]   // level again, seven times the base
    [InlineData(12, 9, 143)] // three ahead, thirteen times the base
    [InlineData(12, 0, 182)] // four ahead, thirteen times the base
    public void ScienceCost_ScalesAgainstTheStrongestLivingCivilization(
        int ownAdvances, int keyAdvances, int expected)
    {
        var (game, subject) = CostBoard(ownAdvances, keyAdvances);

        Assert.Equal(expected, AdvanceFunctions.CalculateScienceCost(game, subject));
    }

    [Fact]
    public void Key_IsTheStrongestLivingCivilization_NotTheOneAtItsPowerRank()
    {
        // The subject is the strongest, so it ranks 0, but it sits at list index 1
        // and the civilisation stored at index 0 is the barbarians. Indexing by rank
        // measured the subject's cost against a tribe with no cities at all.
        var subject = Civ(1, PlayerType.Local, advances: 3, citySize: 40);
        var weaker = Civ(2, PlayerType.Ai, advances: 1, citySize: 1);
        var barbarians = Civ(0, PlayerType.Barbarians, advances: 0, citySize: 0);
        var game = Game(barbarians, subject, weaker);
        subject.PowerRank = 0;

        Assert.Equal(1, AdvanceFunctions.KeyCivilization(game)!.Id);
    }

    [Fact]
    public void Key_BreaksTiesOnIdTheWayAssignPowerRanksDoes()
    {
        // Equal ratings: AssignPowerRanks orders them by id, so the higher id holds
        // the last rank and is the key. The subject's stale rank points elsewhere.
        var first = Civ(1, PlayerType.Local, advances: 4, citySize: 10);
        var second = Civ(2, PlayerType.Ai, advances: 4, citySize: 10);
        var third = Civ(3, PlayerType.Ai, advances: 4, citySize: 10);
        var game = Game(first, second, third);
        first.PowerRank = 2;

        Assert.Equal(3, AdvanceFunctions.KeyCivilization(game)!.Id);
    }

    [Fact]
    public void Key_SkipsTheBarbariansEvenWhenTheyOutrateEveryone()
    {
        // A wild tribe with a big city would otherwise be the key, and its advance
        // count is what the cost would then be measured against.
        var barbarians = Civ(0, PlayerType.Barbarians, advances: 0, citySize: 200);
        var living = Civ(1, PlayerType.Local, advances: 2, citySize: 5);
        var game = Game(barbarians, living);

        Assert.Equal(1, AdvanceFunctions.KeyCivilization(game)!.Id);
    }

    [Fact]
    public void Key_SkipsAnEliminatedCivilizationThatOutranksTheLivingOnes()
    {
        // Eliminated civilisations keep their cities and advances on the list, so
        // the strongest thing on the board is not necessarily a living one.
        var dead = Civ(0, PlayerType.Ai, advances: 12, citySize: 200);
        dead.Alive = false;
        var living = Civ(1, PlayerType.Local, advances: 2, citySize: 5);
        var weaker = Civ(2, PlayerType.Ai, advances: 1, citySize: 2);
        var game = Game(dead, living, weaker);

        Assert.Equal(1, AdvanceFunctions.KeyCivilization(game)!.Id);
    }

    [Fact]
    public void ScienceCost_ALoneCivilizationIsItsOwnKeySoTheLeadTermIsZero()
    {
        // Nothing else to compare against: the cost is the paradigm times the
        // advances it has already learned, whatever its rank says.
        var alone = Civ(0, PlayerType.Local, advances: 6, citySize: 30);
        alone.PowerRank = 4;
        var game = Game(alone);

        Assert.Equal(0, AdvanceFunctions.KeyCivilization(game)!.Id);
        Assert.Equal(70, AdvanceFunctions.CalculateScienceCost(game, alone));
    }

    [Fact]
    public void ScienceCost_TheKeyCivilizationAndACivilizationWithEqualAdvancesPayTheSame()
    {
        // Both hold six advances, so neither has a lead or a lag: the key civilisation
        // is the second one, and the subject's cost is measured against six as well.
        var subject = Civ(0, PlayerType.Local, advances: 6, citySize: 10);
        var key = Civ(1, PlayerType.Ai, advances: 6, citySize: 40);
        var game = Game(subject, key);
        subject.PowerRank = 1;

        Assert.Equal(1, AdvanceFunctions.KeyCivilization(game)!.Id);
        Assert.Equal(70, AdvanceFunctions.CalculateScienceCost(game, subject));
        Assert.Equal(AdvanceFunctions.CalculateScienceCost(game, key),
            AdvanceFunctions.CalculateScienceCost(game, subject));
    }

    [Fact]
    public void ScienceCost_UsesAZeroLeadWhenNoCivilizationQualifies()
    {
        // Only the barbarians and eliminated civilisations are left, so there is no
        // key civilisation at all. The cost falls back to a lead term of 0 rather
        // than comparing against some unrelated entry in the civilisation list.
        var barbarians = Civ(0, PlayerType.Barbarians, advances: 0, citySize: 0);
        var dead = Civ(1, PlayerType.Ai, advances: 12, citySize: 100);
        dead.Alive = false;
        var subject = Civ(2, PlayerType.Ai, advances: 3, citySize: 1);
        subject.Alive = false;
        var game = Game(barbarians, dead, subject);

        Assert.Null(AdvanceFunctions.KeyCivilization(game));
        Assert.Equal(40, AdvanceFunctions.CalculateScienceCost(game, subject));
    }

    // The subject is at index 1 and its rank deliberately points at index 0, the
    // barbarians, which is where the old code read a key advance count of 0. The
    // key civilisation is the one at index 2, rated above the subject by its city.
    private static (MockGame Game, Civilization Subject) CostBoard(int ownAdvances, int keyAdvances)
    {
        var subject = Civ(1, PlayerType.Local, ownAdvances, citySize: 1);
        subject.PowerRank = 0;

        var key = Civ(2, PlayerType.Ai, keyAdvances, citySize: 50);
        var barbarians = Civ(0, PlayerType.Barbarians, 0, citySize: 0);

        return (Game(barbarians, subject, key), subject);
    }

    private static Civilization Civ(int id, PlayerType playerType, int advances, int citySize)
    {
        var civilization = new Civilization
        {
            Id = id,
            Alive = true,
            PlayerType = playerType,
            // The cost formula bails out at -1, so every civilisation here is
            // shown as being part-way through an advance.
            ReseachingAdvance = 0,
            Advances = Enumerable.Repeat(true, advances).ToArray(),
        };
        civilization.Cities.Add(new City { Size = citySize });
        return civilization;
    }

    private static MockGame Game(params Civilization[] civilizations)
    {
        return new MockGame { Rules = new Rules(), AllCivilizations = civilizations.ToList() };
    }
}
