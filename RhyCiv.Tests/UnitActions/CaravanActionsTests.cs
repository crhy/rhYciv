using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Player;
using Model.Core.Units;

namespace RhyCiv.Tests.UnitActions;

/// <summary>
/// Caravans and the trade routes they open.
/// <para>
/// Everything around trade existed and none of it could happen: cities were
/// given commodities to supply and demand, the city window has a line for trade
/// routes, the map draws them, the save format carries them -- and nothing could
/// create one. A caravan reaching a foreign city was refused as a failed attack.
/// </para>
/// </summary>
public class CaravanActionsTests
{
    [Fact]
    public void ALongRouteToAStrangerIsWorthMoreThanAShortOneToYourself()
    {
        var world = World();
        var far = world.Foreign;
        var near = world.OwnNeighbour;

        Assert.True(CaravanActions.RouteValue(world.Home, far) >
                    CaravanActions.RouteValue(world.Home, near),
            "A route across the world to another civilisation should beat one next door to yourself.");
    }

    [Fact]
    public void ACityCannotTradeWithItself()
    {
        var world = World();

        Assert.NotNull(CaravanActions.RouteRefusal(world.Home, world.Home));
    }

    [Fact]
    public void TwoOfYourOwnCitiesSideBySideAreNotWorthARoute()
    {
        var world = World();

        Assert.NotNull(CaravanActions.RouteRefusal(world.Home, world.OwnNeighbour));
    }

    [Fact]
    public void ACityWithFourRoutesWillTakeNoMore()
    {
        var world = World();
        world.Home.TradeRoutes = Enumerable.Range(0, CaravanActions.MaximumRoutesPerCity)
            .Select(i => new TradeRoute { Destination = 90 + i })
            .ToArray();

        Assert.NotNull(CaravanActions.RouteRefusal(world.Home, world.Foreign));
    }

    [Fact]
    public void ADeliveryOpensTheRouteAtBothEnds()
    {
        var world = World();

        CaravanActions.EstablishTradeRoute(world.Game, world.Caravan, world.Foreign);

        Assert.Single(world.Home.TradeRoutes);
        Assert.Single(world.Foreign.TradeRoutes);
        Assert.Equal(world.Game.AllCities.IndexOf(world.Foreign), world.Home.TradeRoutes[0].Destination);
    }

    [Fact]
    public void ADeliveryPaysInGoldAndResearchAndSpendsTheCaravan()
    {
        var world = World();
        var before = world.Home.Owner.Money;

        var delivery = CaravanActions.EstablishTradeRoute(world.Game, world.Caravan, world.Foreign);

        Assert.True(delivery.Gold > 0);
        Assert.Equal(before + delivery.Gold, world.Home.Owner.Money);
        Assert.Equal(delivery.Science, world.Home.Owner.Science);
        Assert.True(world.Caravan.Dead);
    }

    [Fact]
    public void ACityThatWantsWhatTheCaravanCarriesPaysTwice()
    {
        var world = World();
        var indifferent = CaravanActions.DeliveryBonus(world.Caravan, world.Home, world.Foreign);

        world.Foreign.CommodityDemanded = [new Commodity { Id = world.Caravan.CaravanCommodity }];
        var wanted = CaravanActions.DeliveryBonus(world.Caravan, world.Home, world.Foreign);

        Assert.Equal(indifferent * 2, wanted);
    }

    [Fact]
    public void ADeliveryPaysMoreWhenTheCitiesAreFartherApart()
    {
        // The one-off delivery payment is the only place distance still enters,
        // and it is still Civilization I's shape: (distance + 10) * (T1 + T2) /
        // 24, then times DeliveryMultiplier = 8. Pinned here with the two cities
        // producing 12 and 14 trade, foreign to one another and on different
        // islands so nothing halves it, so rhY can paste the figures beside Civ
        // II's screenshots of the same delivery:
        //   distance 1: (1 + 10) * 26 / 24 = 11 -> 11 * 8 = 88 gold
        //   distance 8: (8 + 10) * 26 / 24 = 19 -> 19 * 8 = 152 gold
        var near = Delivery(1, homeTrade: 12, destinationTrade: 14);
        var far = Delivery(8, homeTrade: 12, destinationTrade: 14);

        var nearPayment = CaravanActions.DeliveryBonus(near.Caravan, near.Home, near.Destination);
        var farPayment = CaravanActions.DeliveryBonus(far.Caravan, far.Home, far.Destination);

        Assert.Equal(88, nearPayment);
        Assert.Equal(152, farPayment);
        Assert.True(farPayment > nearPayment,
            "Distance genuinely belongs in a delivery payment: the long journey " +
            "should be the worth one, whatever it is worth.");
    }

    [Fact]
    public void ADeliveryBetweenOwnCitiesPaysHalf()
    {
        // The same delivery into a city of your own civilisation is halved -- and
        // halved before the multiplier, so the remainder is dropped first and the
        // halves only line up when the figure is even. Trade 12 and 14, distance
        // 14, different islands:
        //   to a stranger: (14 + 10) * 26 / 24 = 26 -> 26 * 8 = 208 gold
        //   to your own:   26 / 2 = 13          -> 13 * 8 = 104 gold
        var stranger = Delivery(14, homeTrade: 12, destinationTrade: 14);
        var own = Delivery(14, homeTrade: 12, destinationTrade: 14, foreignDestination: false);

        Assert.Equal(208, CaravanActions.DeliveryBonus(stranger.Caravan, stranger.Home, stranger.Destination));
        Assert.Equal(104, CaravanActions.DeliveryBonus(own.Caravan, own.Home, own.Destination));
    }

    [Fact]
    public void ADeliveryToADemandingCityPaysDouble()
    {
        // A destination that wants the commodity the caravan carries pays twice
        // over, on top of everything above. Trade 12 and 14, distance 8, foreign
        // city on its own island:
        //   not wanted: (8 + 10) * 26 / 24 = 19 -> 19 * 8 = 152 gold
        //   wanted:     152 * 2 = 304 gold
        var world = Delivery(8, homeTrade: 12, destinationTrade: 14);
        var unwanted = CaravanActions.DeliveryBonus(world.Caravan, world.Home, world.Destination);

        world.Destination.CommodityDemanded = [new Commodity { Id = world.Caravan.CaravanCommodity }];
        var wanted = CaravanActions.DeliveryBonus(world.Caravan, world.Home, world.Destination);

        Assert.Equal(152, unwanted);
        Assert.Equal(304, wanted);
    }

    [Fact]
    public void AnOpenRouteBringsItsCityTradeEveryTurn()
    {
        var world = World();
        Assert.Equal(0, CaravanActions.TradeFromRoutes(world.Game, world.Home));

        CaravanActions.EstablishTradeRoute(world.Game, world.Caravan, world.Foreign);

        Assert.True(CaravanActions.TradeFromRoutes(world.Game, world.Home) > 0);
    }

    [Fact]
    public void DistanceIsMeasuredTheShortWayRoundAWorldThatWraps()
    {
        var map = new Map(false, 0) { Tile = new Tile[40, 20], XDim = 40, YDim = 20 };
        Fill(map);

        var west = map.Tile[1, 10];
        var east = map.Tile[38, 10];

        // Two columns apart round the back of the world, not thirty-seven across it.
        Assert.Equal(3, CaravanActions.MapDistance(west, east));
    }

    private record TestWorld(MockGame Game, City Home, City OwnNeighbour, City Foreign, Unit Caravan);
    private record DeliveryWorld(City Home, City Destination, Unit Caravan);

    /// <summary>
    /// A home city and a destination exactly <c>distance</c> squares apart on the
    /// same row, with the trade each of them produces and nothing else in play:
    /// different islands, so the payment is not halved for proximity, and a foreign
    /// destination unless asked otherwise. The delivery payment tests need the
    /// distance and both trade figures known exactly, which the world below cannot
    /// give them -- its cities sit where the route tests need them.
    /// </summary>
    private static DeliveryWorld Delivery(int distance, int homeTrade, int destinationTrade,
        bool foreignDestination = true)
    {
        var map = new Map(true, 0) { Tile = new Tile[40, 20], XDim = 40, YDim = 20 };
        Fill(map);

        var us = new Civilization { Id = 0, TribeName = "Americans", Adjective = "American", Alive = true };
        var them = new Civilization { Id = 1, TribeName = "Romans", Adjective = "Roman", Alive = true };

        // Both cities on row 4, an even row, where the doubled columns make one
        // square of distance exactly one column of x: the offset is the distance.
        var home = CityAt(map, us, "Washington", 2, 4, island: 1, trade: homeTrade);
        var destination = CityAt(map, foreignDestination ? them : us, "Rome", 2 + distance, 4,
            island: 2, trade: destinationTrade);

        var caravan = new Unit
        {
            Owner = us,
            HomeCity = home,
            CaravanCommodity = 3,
            TypeDefinition = new UnitDefinition
            {
                Name = "Caravan", Cost = 50, AIrole = AiRoleType.Trade,
                Flags = Enumerable.Repeat(false, 13).ToArray()
            }
        };
        us.Units.Add(caravan);
        caravan.CurrentLocation = destination.Location;

        return new DeliveryWorld(home, destination, caravan);
    }

    /// <summary>
    /// Two civilisations on two islands: the player's capital with a neighbour two
    /// squares away, and a foreign city across the water, with a caravan standing
    /// in it.
    /// </summary>
    private static TestWorld World()
    {
        var map = new Map(true, 0) { Tile = new Tile[40, 20], XDim = 40, YDim = 20 };
        Fill(map);

        var us = new Civilization { Id = 0, TribeName = "Americans", Adjective = "American", Alive = true };
        var them = new Civilization { Id = 1, TribeName = "Romans", Adjective = "Roman", Alive = true };

        var home = CityAt(map, us, "Washington", 2, 4, island: 1, trade: 12);
        var neighbour = CityAt(map, us, "New York", 4, 4, island: 1, trade: 8);
        var foreign = CityAt(map, them, "Rome", 30, 12, island: 2, trade: 14);

        var caravan = new Unit
        {
            Owner = us,
            HomeCity = home,
            CaravanCommodity = 3,
            TypeDefinition = new UnitDefinition
            {
                Name = "Caravan", Cost = 50, AIrole = AiRoleType.Trade,
                Flags = Enumerable.Repeat(false, 13).ToArray()
            }
        };
        us.Units.Add(caravan);
        caravan.CurrentLocation = foreign.Location;

        var players = new IPlayer[] { new MockPlayer(us), new MockPlayer(them) };
        var game = new MockGame
        {
            Maps = [map],
            Rules = new Rules { CaravanCommoditie = [new Commodity { Id = 0, Name = "Silk" }] },
            AllCivilizations = [us, them],
            Players = players
        };

        return new TestWorld(game, home, neighbour, foreign, caravan);
    }

    private static void Fill(Map map)
    {
        var grass = new Terrain { Type = TerrainType.Grassland, Specials = [] };
        for (var y = 0; y < map.YDim; y++)
        {
            for (var x = 0; x < map.XDim; x++)
            {
                map.Tile[x, y] = new Tile(x, y, grass, 1, map, x, new bool[4]);
            }
        }
    }

    private static City CityAt(Map map, Civilization owner, string name, int x, int y, int island, int trade)
    {
        var tile = map.Tile[x, y];
        tile.Island = island;
        var city = new City
        {
            Name = name, Owner = owner, Location = tile, Trade = trade, TileTrade = trade
        };
        tile.CityHere = city;
        owner.Cities.Add(city);
        return city;
    }
}
