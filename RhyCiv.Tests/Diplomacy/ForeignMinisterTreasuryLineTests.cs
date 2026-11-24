using System;
using System.Globalization;
using Model.Core;
using RhyCiv.Engine.Diplomacy;

namespace RhyCiv.Tests.Diplomacy;

/// <summary>
/// Each Foreign Minister line carries the treasury alongside the three facts it
/// already reported, formatted with thousands separators so it reads like Civ II.
/// </summary>
public class ForeignMinisterTreasuryLineTests
{
    [Fact]
    public void TheLineCarriesAttitudeStandingEmbassyAndTreasury()
    {
        var line = Run(out var us, out var them);

        // The leader and tribe text is unchanged from before the treasury was added.
        Assert.Contains("Consul Ishmael of the Romans", line);
        Assert.Contains("Neutral", line);
        Assert.Contains("No Treaty", line);
        Assert.Contains("Embassy", line);
        Assert.Contains(them.Money.ToString("N0"), line);
    }

    [Fact]
    public void ATreasuryRendersWithThousandsSeparators()
    {
        var (_, them) = Expect();
        Assert.Equal(1500, them.Money);
        Assert.Equal("1,500", them.Money.ToString("N0", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TheSecondCivilisationCarriesItsOwnFacts()
    {
        var (us, first) = Expect();
        var second = new Civilization
        {
            Id = 2, Alive = true, TribeName = "Greeks", Adjective = "Greek",
            LeaderTitle = "Basileus", LeaderName = "Theresa", Money = 2000
        };

        // A court that has soured on us, with no embassy and no treaty.
        DiplomacyFunctions.AdjustAttitude(second, us, -100);

        var line = DiplomacyFunctions.ForeignMinisterLine(second, us);

        Assert.Contains("Basileus Theresa of the Greeks", line);
        Assert.Contains("Enraged", line);
        Assert.Contains("No Treaty", line);
        Assert.Contains("No Embassy", line);
        Assert.Contains("2,000", line);
    }

    private static string Run(out Civilization us, out Civilization them)
    {
        (us, them) = Expect();
        DiplomacyFunctions.EstablishEmbassy(us, them);
        return DiplomacyFunctions.ForeignMinisterLine(them, us);
    }

    private static (Civilization us, Civilization them) Expect()
    {
        var us = new Civilization
        {
            Id = 0, Alive = true, TribeName = "Americans", Adjective = "American"
        };
        var them = new Civilization
        {
            Id = 1, Alive = true, TribeName = "Romans", Adjective = "Roman",
            LeaderTitle = "Consul", LeaderName = "Ishmael", Money = 1500
        };
        return (us, them);
    }
}
