using RhyCiv.Engine;
using RhyCiv.Engine.Production;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Production;

namespace RhyCiv.Tests.Production;

/// <summary>
/// A city that finishes a building moves on to something it can build, and does
/// not announce the next turn that it "cannot build" anything (#185).
/// <para>
/// A finished building used to stay in production. The next turn found it
/// invalid, and the replacement it looked for was anything whose prerequisite
/// matched the building's expiry advance -- for a building that never expires,
/// the no-advance marker, which matched everything needing no advance. The
/// cheapest of those was Barracks: the player was told the city could not build
/// Barracks, and then it built them.
/// </para>
/// </summary>
public class FinishedBuildingProductionTests
{
    [Fact]
    public void FinishingABuilding_MovesProductionOnAtOnce()
    {
        var (game, city, player) = CityBuilding(out var building);
        city.ShieldsProgress = 999;

        game.CitiesTurn(player);

        Assert.True(city.ImprovementExists(building.Improvement.Type),
            "the building was not completed");
        Assert.NotSame(building, city.ItemInProduction);
        Assert.True(ProductionPossibilities.ProductionValid(city),
            $"production moved on to {city.ItemInProduction?.Title}, which the city cannot build");
    }

    [Fact]
    public void TheTurnAfter_SaysNothingAboutWhatItCannotBuild()
    {
        var (game, city, player) = CityBuilding(out _);
        city.ShieldsProgress = 999;

        game.CitiesTurn(player);
        game.CitiesTurn(player);

        Assert.Equal(0, player.CantProduceCalls);
    }

    [Fact]
    public void AnInvalidBuildingThatNeverExpires_IsNotReplacedByBarracks()
    {
        var (_, city, _) = CityBuilding(out var building);
        city.AddImprovement(building.Improvement);

        var next = ProductionPossibilities.AutoNext(city);

        Assert.NotNull(next);
        Assert.IsType<UnitProductionOrder>(next);
    }

    private static (Game Game, City City, MockPlayer Player) CityBuilding(out BuildingProductionOrder building)
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var civ = game.GetPlayerCiv;
        var city = CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Alesia");
        city.FoodInStorage = 0;
        // Enough to pay upkeep: a civilisation that cannot sells the building on
        // the spot, which is a different rule from the one under test.
        civ.Money = 500;

        // Something the city can build now that never expires: the kind of
        // building (Barracks' own case aside) that exposed the fault.
        building = ProductionPossibilities.GetAllowedProductionOrders(city)
            .OfType<BuildingProductionOrder>()
            .First(order => !order.Improvement.IsWonder && order.ExpiresTech < 0);
        city.ItemInProduction = building;
        return (game, city, player);
    }
}
