using Moq;
using RhyCiv.Engine;
using RhyCiv.Engine.Production;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Production;
using RaylibUI.RunGame.Commands;

namespace RhyCiv.Tests.Production;

/// <summary>
/// The City Status advisor changes what a city is building through the engine's
/// own change of project, and only to something that city may build.
/// <para>
/// The advisor (matrix row 31 of docs/CIV2-UI-ADDITIONS.md) offers the alternatives
/// itself: what <see cref="ProductionPossibilities.GetAllowedProductionOrders"/>
/// lists for that city. A project outside that list is refused, so the city keeps
/// making what it was making instead of taking on something it cannot finish.
/// </para>
/// </summary>
public class CityStatusAdvisorProductionEditTests
{
    [Fact]
    public void AnOfferedAlternative_BecomesTheCitysProject()
    {
        var (rules, city) = ACityAtWork();
        var allowed = ProductionPossibilities.GetAllowedProductionOrders(city);
        var next = allowed.First(order => !ReferenceEquals(order, city.ItemInProduction));

        Assert.True(FindCity.TryChangeProduction(city, allowed, next, rules));
        Assert.Same(next, city.ItemInProduction);
        Assert.True(ProductionPossibilities.ProductionValid(city),
            $"the city was left producing {next.Title}, which it cannot build");
    }

    [Fact]
    public void TheQueuedProjectFollowsTheChange()
    {
        var (rules, city) = ACityAtWork();
        var allowed = ProductionPossibilities.GetAllowedProductionOrders(city);
        var next = allowed.First(order => !ReferenceEquals(order, city.ItemInProduction));
        city.ShieldsProgress = 0;

        Assert.True(FindCity.TryChangeProduction(city, allowed, next, rules));
        Assert.NotNull(city.ConstructionQueue.Current);
        Assert.Same(next, city.ConstructionQueue.Current!.Order);
        Assert.Equal(next.Cost, city.ConstructionQueue.Current.RemainingCost);
    }

    [Fact]
    public void AnOrderTheAdvisorDidNotOffer_LeavesTheCitysProjectAlone()
    {
        var (rules, city) = ACityAtWork();
        var allowed = ProductionPossibilities.GetAllowedProductionOrders(city);
        var current = city.ItemInProduction;

        // Something this city may not build at all: an order no build list carries.
        var forbidden = new Mock<IProductionOrder>().Object;

        Assert.DoesNotContain(forbidden, allowed);
        Assert.False(FindCity.TryChangeProduction(city, allowed, forbidden, rules));
        Assert.Same(current, city.ItemInProduction);
    }

    [Fact]
    public void TheEngineRefusesAProjectItDidNotList()
    {
        var (_, city) = ACityAtWork();

        // The advisor's second guard: a project that reached the city at all still
        // has to pass the engine's own check before the change is allowed to stand.
        city.ItemInProduction = new Mock<IProductionOrder>().Object;

        Assert.False(ProductionPossibilities.ProductionValid(city));
    }

    private static (Rules Rules, City City) ACityAtWork()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var civ = game.GetPlayerCiv;
        civ.Money = 500;
        return (rules, CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Alesia"));
    }
}
