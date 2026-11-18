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
/// The standing trade route's value, pinned from both ends.
/// <para>
/// Civ II's own arithmetic is <c>(T1 + T2 + 4) / 8</c> for a route to another
/// civilisation and <c>k(T1 + T2 + 4) / 16</c> between two of your own, where T1
/// and T2 are the two cities' base trade and every division drops its remainder.
/// The formula is symmetric in T1 and T2, and the source states plainly that both
/// cities receive the same amount -- so the value a route carries cannot depend
/// on which end asks.
/// </para>
/// <para>
/// These tests pin the figure rather than change it. What Civ II pays the two ends
/// of the Cardiff-Carmarthen route (+1 one way, +2 the other) is still unexplained
/// and is recorded in docs/CIV2-COMPARISON.md; nothing here settles it, because
/// nothing here can measure it. See issue #130.
/// </para>
/// </summary>
public class TradeRouteValueTests
{
    [Fact]
    public void StandingRouteValueIsTheSameFromBothEnds()
    {
        var world = World();

        // (12 + 14 + 4) / 8 = 3, whichever city is asked.
        AssertArrows(3, CaravanActions.RouteValue(world.Home, world.Foreign),
            "A standing route to another civilisation is (T1 + T2 + 4)/8 with the remainder dropped.");
        AssertArrows(3, CaravanActions.RouteValue(world.Foreign, world.Home),
            "The far end of a foreign route pays the same arrows as the near end: the sourced formula is symmetric in T1 and T2 and says both cities receive the same amount.");

        // (12 + 8 + 4) / 16 = 1, again whichever city is asked.
        AssertArrows(1, CaravanActions.RouteValue(world.Home, world.OwnNeighbour),
            "A standing route between two cities of the same civilisation is k(T1 + T2 + 4)/16 with k treated as 1.");
        AssertArrows(1, CaravanActions.RouteValue(world.OwnNeighbour, world.Home),
            "Neither end of an own-empire route is the source: the value cannot depend on which city asks.");
    }

    [Fact]
    public void TheIssuePairOfCitiesPaysOneArrowBetweenOwnCities()
    {
        var world = World();

        // The pair from issue #130: base trade 11 and 14, two of your own cities.
        var first = CityAt(world.Map, world.Us, "Cardiff", 6, 14, island: 1, trade: 11);
        var second = CityAt(world.Map, world.Us, "Carmarthen", 20, 14, island: 1, trade: 14);

        // (11 + 14 + 4) / 16 = 1, which is what Cardiff's screen shows.
        AssertArrows(1, CaravanActions.RouteValue(first, second),
            "Between two of your own cities the route is (T1 + T2 + 4)/16, so the issue's 11 and 14 pay one arrow.");
        AssertArrows(1, CaravanActions.RouteValue(second, first),
            "Civ II's formula is symmetric, so Carmarthen is pinned to the same one arrow; the +2 on its screen is the open item, not the rule.");

        // The same pair of trade figures across civilisations: (11 + 14 + 4) / 8 = 3.
        var stranger = CityAt(world.Map, world.Them, "Cadiz", 34, 4, island: 2, trade: 14);
        AssertArrows(3, CaravanActions.RouteValue(first, stranger),
            "The same two cities pay (T1 + T2 + 4)/8 once the far end belongs to another civilisation.");
    }

    [Fact]
    public void RemaindersDropTheWayCivIIsDoes()
    {
        var world = World();

        // 12 + 14 + 4 = 30: one division, and the remainder is dropped, not carried.
        AssertArrows(3, CaravanActions.RouteValue(world.Home, world.Foreign),
            "Civ II divides the standing route once by eight for a foreign route and drops the remainder.");
        AssertArrows(1, CaravanActions.RouteValue(world.Home, world.OwnTradePartner),
            "Civ II divides the standing route once by sixteen between your own cities and drops the remainder, rather than halving a halved figure.");
    }

    [Fact]
    public void DistanceDoesNotEnterTheStandingRoute()
    {
        var world = World();

        // Two foreign pairs with the same base trade, at clearly different distances.
        var near = CityAt(world.Map, world.Them, "Tyre", 5, 4, island: 2, trade: 14);
        var far = CityAt(world.Map, world.Them, "Carthage", 35, 16, island: 3, trade: 14);

        AssertArrows(3, CaravanActions.RouteValue(world.Home, near),
            "A short route to a stranger is still (T1 + T2 + 4)/8: distance does not enter the standing figure.");
        AssertArrows(3, CaravanActions.RouteValue(world.Home, far),
            "A long route to a stranger pays exactly what the short one does: distance does not enter the standing figure.");
        Assert.True(CaravanActions.MapDistance(world.Home.Location, near.Location) <
                    CaravanActions.MapDistance(world.Home.Location, far.Location),
            "The two pairs really are at different distances, so this pins distance out of the formula rather than trading figures.");
    }

    [Fact]
    public void OwnRoutesCostLessThanForeignOnesAtTheSameTrade()
    {
        var world = World();

        // The same 12 and 14, own cities against a stranger.
        AssertArrows(1, CaravanActions.RouteValue(world.Home, world.OwnTradePartner),
            "Two of your own cities pay k(T1 + T2 + 4)/16, which is why the profitable routes are the ones to strangers.");
        AssertArrows(3, CaravanActions.RouteValue(world.Home, world.Foreign),
            "A route to another civilisation pays (T1 + T2 + 4)/8, double the own-empire figure at the same trade.");
    }

    [Fact]
    public void TradeFromRoutesAddsEachRouteOnceAndNothingWhenThereAreNone()
    {
        var world = World();

        AssertArrows(0, CaravanActions.TradeFromRoutes(world.Game, world.Home),
            "A city with no trade routes earns nothing from routes, whatever slots its save carries.");

        world.Home.TradeRoutes =
        [
            new TradeRoute { Destination = world.Game.AllCities.IndexOf(world.Foreign) },
            new TradeRoute { Destination = world.Game.AllCities.IndexOf(world.OwnTradePartner) }
        ];

        // 3 from the foreign route plus 1 from the own-empire one.
        AssertArrows(4, CaravanActions.TradeFromRoutes(world.Game, world.Home),
            "A city's trade from routes is the sum of its standing routes, each counted once at its own value.");
    }

    [Fact]
    public void AZeroRouteSlotIsNotARoute()
    {
        Assert.False(CaravanActions.IsRecordedRoute(0, 0),
            "A slot zero in both commodity and destination is an unused slot, not a route to the first city in the list.");
        Assert.True(CaravanActions.IsRecordedRoute(0, 5),
            "Commodity zero names a real commodity, so a slot with a destination is still a route.");
        Assert.True(CaravanActions.IsRecordedRoute(3, 0),
            "A commodity with no destination is still a recorded route; only the zero pair marks a slot empty.");
    }

    /// <summary>
    /// Asserts an exact arrow count and names the Civ II rule it pins.
    /// <para>
    /// xunit 2.9.2 has no message overload of <c>Assert.Equal</c> for integers --
    /// its three-argument form takes a comparison delegate -- so the rule sentence
    /// travels here. A future change to the formula then fails with the Civ II
    /// sentence rather than a bare number.
    /// </para>
    /// </summary>
    private static void AssertArrows(int expected, int actual, string rule)
    {
        Assert.True(actual == expected, $"{rule} (expected {expected} arrow(s), got {actual})");
    }

    private record TestWorld(MockGame Game, Map Map, Civilization Us, Civilization Them,
        City Home, City OwnNeighbour, City Foreign, City OwnTradePartner);

    /// <summary>
    /// Two civilisations on three islands: the player's capital with a neighbour two
    /// squares away and a trade partner far down the same island, and a foreign city
    /// across the water.
    /// <para>
    /// Base trade is set on both <c>Trade</c> and <c>TileTrade</c>, because the
    /// standing route is valued from the squares' figure and not from the city's
    /// total, which already contains its own routes.
    /// </para>
    /// </summary>
    private static TestWorld World()
    {
        var map = new Map(true, 0) { Tile = new Tile[40, 20], XDim = 40, YDim = 20 };
        Fill(map);

        var us = new Civilization { Id = 0, TribeName = "Americans", Adjective = "American", Alive = true };
        var them = new Civilization { Id = 1, TribeName = "Romans", Adjective = "Roman", Alive = true };

        var home = CityAt(map, us, "Washington", 2, 4, island: 1, trade: 12);
        var neighbour = CityAt(map, us, "New York", 4, 4, island: 1, trade: 8);
        var ownPartner = CityAt(map, us, "Richmond", 2, 16, island: 1, trade: 14);
        var foreign = CityAt(map, them, "Rome", 30, 12, island: 2, trade: 14);

        var players = new IPlayer[] { new MockPlayer(us), new MockPlayer(them) };
        var game = new MockGame
        {
            Maps = [map],
            Rules = new Rules { CaravanCommoditie = [new Commodity { Id = 0, Name = "Silk" }] },
            AllCivilizations = [us, them],
            Players = players
        };

        return new TestWorld(game, map, us, them, home, neighbour, foreign, ownPartner);
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
