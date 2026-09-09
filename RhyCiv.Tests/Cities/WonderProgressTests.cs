using RhyCiv.Engine;
using RhyCiv.Engine.Production;
using RhyCiv.Tests.Mocks;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Player;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// The world's view of a wonder being built.
/// <para>
/// Civ II reports a wonder begun, a rival about to finish one, and one finished,
/// and none of that was reported here. Worse, nothing stopped two civilisations
/// -- or two cities of one civilisation -- from each raising the Pyramids, which
/// is the one thing a wonder cannot be.
/// </para>
/// </summary>
public class WonderProgressTests
{
    [Fact]
    public void TheFirstShieldsIntoAWonderAreNewsEverywhere()
    {
        var (game, players, city) = World();
        city.ShieldsProgress = 20;

        WonderProgress.ReportProgress(game, city, shieldsBefore: 0);

        Assert.Single(players[1].WondersBegun);
        Assert.Equal("Pyramids", players[1].WondersBegun[0].Wonder.Name);
    }

    [Fact]
    public void ACivilisationIsNotToldAboutItsOwnScaffolding()
    {
        var (game, players, city) = World();
        city.ShieldsProgress = 20;

        WonderProgress.ReportProgress(game, city, shieldsBefore: 0);

        Assert.Empty(players[0].WondersBegun);
    }

    [Fact]
    public void WorkThatWasAlreadyUnderWayIsNotAnnouncedAgain()
    {
        var (game, players, city) = World();
        city.ShieldsProgress = 40;

        WonderProgress.ReportProgress(game, city, shieldsBefore: 30);

        Assert.Empty(players[1].WondersBegun);
    }

    [Fact]
    public void OnlyTheCivilisationsInTheRaceHearThatItIsNearlyOver()
    {
        var (game, players, city) = World();
        var rival = game.AllCivilizations[1];
        rival.Cities.Add(CityBuilding(rival, "Rome", Pyramids()));
        var bystander = game.AllCivilizations[2];
        bystander.Cities.Add(CityBuilding(bystander, "Thebes", null));

        city.ShieldsProgress = 160;
        WonderProgress.ReportProgress(game, city, shieldsBefore: 100);

        Assert.Single(players[1].WondersNearlyComplete);
        Assert.Empty(players[2].WondersNearlyComplete);
    }

    [Fact]
    public void FinishingAWonderTellsTheWorld()
    {
        var (game, players, city) = World();

        WonderProgress.Completed(game, city, Pyramids());

        Assert.Single(players[1].WondersCompleted);
        Assert.Single(players[2].WondersCompleted);
        Assert.Empty(players[0].WondersCompleted);
    }

    [Fact]
    public void ACivilisationBuildingTheSameWonderIsToldItHasLostTheRace()
    {
        var (game, players, city) = World();
        var rival = game.AllCivilizations[1];
        rival.Cities.Add(CityBuilding(rival, "Rome", Pyramids()));

        WonderProgress.Completed(game, city, Pyramids());

        Assert.Single(players[1].WondersLost);
        Assert.Equal("Rome", players[1].WondersLost[0].City.Name);

        // The race is the news; it is not also announced as somebody else's triumph.
        Assert.Empty(players[1].WondersCompleted);
    }

    [Fact]
    public void AFinishedWonderCanNeverBeBuiltAgain()
    {
        var (game, _, city) = World();
        var orders = ProductionPossibilities.GetAllowedProductionOrders(city);
        Assert.Contains(orders, order => order is BuildingProductionOrder { Improvement.Name: "Pyramids" });

        WonderProgress.Completed(game, city, Pyramids());

        foreach (var civilization in game.AllCivilizations)
        {
            var stillOffered = ProductionPossibilities
                .GetAllowedProductionOrders(civilization.Cities[0])
                .Any(order => order is BuildingProductionOrder { Improvement.Name: "Pyramids" });

            Assert.False(stillOffered, $"The {civilization.TribeName} were still offered the Pyramids.");
        }
    }

    /// <summary>
    /// Three civilisations, the first of them building the Pyramids, and every
    /// civilisation able to build them until somebody does.
    /// </summary>
    private static (MockGame Game, MockPlayer[] Players, City City) World()
    {
        var civilizations = new List<Civilization>
        {
            new() { Id = 0, TribeName = "Americans", Adjective = "American", Alive = true },
            new() { Id = 1, TribeName = "Romans", Adjective = "Roman", Alive = true },
            new() { Id = 2, TribeName = "Egyptians", Adjective = "Egyptian", Alive = true }
        };

        var pyramids = Pyramids();
        foreach (var civilization in civilizations)
        {
            civilization.Cities.Add(CityBuilding(civilization, $"{civilization.Adjective} capital", null));
        }

        var city = CityBuilding(civilizations[0], "Washington", pyramids);
        civilizations[0].Cities.Add(city);

        var players = civilizations.Select(civ => new MockPlayer(civ)).ToArray();
        var game = new MockGame
        {
            Rules = new Rules { Improvements = [pyramids] },
            AllCivilizations = civilizations,
            Players = players.Cast<IPlayer>().ToArray()
        };

        ProductionPossibilities.InitializeProductionLists(civilizations,
            [new BuildingProductionOrder(pyramids, 0, 0)]);

        return (game, players, city);
    }

    private static Improvement Pyramids() =>
        new()
        {
            Name = "Pyramids", Type = 40, Cost = 200, IsWonder = true,
            Prerequisite = AdvancesConstants.Nil, ExpiresAt = AdvancesConstants.Nil
        };

    private static City CityBuilding(Civilization owner, string name, Improvement? wonder) =>
        new()
        {
            Name = name,
            Owner = owner,
            ItemInProduction = wonder == null
                ? new BuildingProductionOrder(new Improvement { Name = "Temple", Type = 1, Cost = 40 }, 1, 0)
                : new BuildingProductionOrder(wonder, 0, 0)
        };
}
