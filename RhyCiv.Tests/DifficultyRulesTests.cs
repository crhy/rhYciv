using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Tests.Mocks;
using Model.Core;
using Model.Core.Cities;

namespace RhyCiv.Tests;

/// <summary>
/// What choosing Prince over Deity actually changes.
/// <para>
/// The difficulty was asked for at the start of every game, written into the
/// save, and read in three places. Prince and Deity played very nearly the same
/// game.
/// </para>
/// </summary>
public class DifficultyRulesTests
{
    [Fact]
    public void ThePlayerAlwaysPaysTheListedPrice()
    {
        var city = CityOf(PlayerType.Local);

        foreach (var level in Enum.GetValues<DifficultyType>())
        {
            Assert.Equal(40, DifficultyRules.ProductionCost(Game(level), city, 40));
        }
    }

    [Fact]
    public void ComputerCivilisationsPayMoreOnTheEasyLevelsAndLessOnTheHardOnes()
    {
        var city = CityOf(PlayerType.Ai);

        var chieftain = DifficultyRules.ProductionCost(Game(DifficultyType.Chieftain), city, 100);
        var king = DifficultyRules.ProductionCost(Game(DifficultyType.King), city, 100);
        var deity = DifficultyRules.ProductionCost(Game(DifficultyType.Deity), city, 100);

        Assert.Equal(160, chieftain);
        Assert.Equal(100, king);
        Assert.Equal(80, deity);
    }

    [Fact]
    public void TheTopLevelsGiveComputerCitiesExtraShields()
    {
        var city = CityOf(PlayerType.Ai);

        Assert.Equal(10, DifficultyRules.ShieldsBanked(Game(DifficultyType.Prince), city, 10));
        Assert.Equal(12, DifficultyRules.ShieldsBanked(Game(DifficultyType.Emperor), city, 10));
        Assert.Equal(14, DifficultyRules.ShieldsBanked(Game(DifficultyType.Deity), city, 10));
    }

    [Fact]
    public void ThePlayersOwnCitiesGetNoHelpAtAnyLevel()
    {
        var city = CityOf(PlayerType.Local);

        Assert.Equal(10, DifficultyRules.ShieldsBanked(Game(DifficultyType.Deity), city, 10));
    }

    [Fact]
    public void BarbariansHitAQuarterAsHardOnChieftainAndHalfAsHardAgainOnDeity()
    {
        var barbarians = new Civilization { Id = 0, PlayerType = PlayerType.Barbarians };

        Assert.Equal(1, DifficultyRules.AttackStrength(Game(DifficultyType.Chieftain), barbarians, 4));
        Assert.Equal(4, DifficultyRules.AttackStrength(Game(DifficultyType.King), barbarians, 4));
        Assert.Equal(6, DifficultyRules.AttackStrength(Game(DifficultyType.Deity), barbarians, 4));
    }

    [Fact]
    public void EverybodyElseFightsAtTheirListedStrength()
    {
        var civ = new Civilization { Id = 1, PlayerType = PlayerType.Ai };

        Assert.Equal(4, DifficultyRules.AttackStrength(Game(DifficultyType.Deity), civ, 4));
    }

    [Fact]
    public void BarbariansComeAshoreAsVeteransFromKingUpwards()
    {
        Assert.False(DifficultyRules.BarbariansAreVeterans(Game(DifficultyType.Prince)));
        Assert.True(DifficultyRules.BarbariansAreVeterans(Game(DifficultyType.King)));
        Assert.True(DifficultyRules.BarbariansAreVeterans(Game(DifficultyType.Deity)));
    }

    private static MockGame Game(DifficultyType level) => new() { Difficulty = (int)level };

    private static City CityOf(PlayerType type) =>
        new() { Owner = new Civilization { Id = 1, PlayerType = type } };
}
