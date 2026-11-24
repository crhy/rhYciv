using RhyCiv.Engine.UnitActions;
using RhyCiv.Engine.Enums;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Player;
using Model.Core.Units;
using Moq;

namespace RhyCiv.Tests.UnitActions;

public class AirliftFunctionsTests
{
    private const int AirbaseEffect = ImprovementConstants.Airbase;

    private static Map BuildMap()
    {
        var map = new Map(true, 0) { XDim = 20, YDim = 20, Tile = new Tile[20, 20] };
        for (var x = 0; x < 20; x++)
        {
            for (var y = 0; y < 20; y++)
            {
                map.Tile[x, y] = new Tile(x * 2 + y % 2, y,
                    new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = [] },
                    0, map, x, new bool[2]);
            }
        }

        return map;
    }

    private static Mock<IGame> BuildGame(Map map, List<City> cities, int turn = 1)
    {
        var game = new Mock<IGame>();
        var rules = new Rules { Advances = [], Improvements = [], Governments = [] };
        game.Setup(g => g.Rules).Returns(rules);
        game.Setup(g => g.Maps).Returns(new List<Map> { map });
        game.Setup(g => g.AllCities).Returns(cities);
        game.Setup(g => g.TurnNumber).Returns(turn);
        game.Setup(g => g.Players).Returns([new Mock<IPlayer>().Object, new Mock<IPlayer>().Object]);
        return game;
    }

    private static Unit Aircraft(Civilization owner, Tile at, UnitGas domain = UnitGas.Air)
    {
        var unit = new Unit
        {
            Owner = owner,
            TypeDefinition = new UnitDefinition { Move = 3, Range = 4, Flags = new bool[15], Domain = domain }
        };
        unit.CurrentLocation = at;
        unit.X = at.X;
        unit.Y = at.Y;
        return unit;
    }

    /// <summary>
    /// Found a city on the map, optionally carrying the airbase effect, and register
    /// it with the game the way <see cref="IGame"/> does, since that is where the
    /// airlift looks for destinations.
    /// </summary>
    private static City CityTile(Map map, List<City> cities, Civilization owner, int x, int y, bool withAirbase)
    {
        var tile = map.Tile[x, y];
        var city = new City { Owner = owner, Location = tile };
        tile.CityHere = city;
        if (withAirbase)
        {
            tile.EffectsList.Add(new ActiveEffect(
                new TerrainImprovementAction { Target = AirbaseEffect }, ImprovementTypes.Airbase));
        }

        cities.Add(city);
        return city;
    }

    private static void AssertUntouched(Unit unit, Tile origin)
    {
        Assert.Equal(origin, unit.CurrentLocation);
        Assert.Equal(origin.X, unit.X);
        Assert.Equal(origin.Y, unit.Y);
        Assert.Empty(unit.PrevXy);
        Assert.Equal(0, unit.MovePointsLost);
        // A unit that has never been handed an order carries 0; NoOrders (255) is
        // what an order is cleared to once a unit has had one.
        Assert.Equal(0, unit.Order);
    }

    [Fact]
    public void TryAirlift_MovesTheUnitAndSpendsItsTurn()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var originCity = CityTile(map, cities, civ, 2, 2, withAirbase: true);
        var landingCity = CityTile(map, cities, civ, 15, 15, withAirbase: true);
        var origin = originCity.Location;
        var landing = landingCity.Location;
        var unit = Aircraft(civ, origin);
        var game = BuildGame(map, cities, turn: 1);

        Assert.True(AirliftFunctions.TryAirlift(game.Object, unit, landing));
        Assert.Equal(landing, unit.CurrentLocation);
        Assert.Contains(unit, landing.UnitsHere);
        Assert.DoesNotContain(unit, origin.UnitsHere);
        Assert.Equal(new[] { origin.X, origin.Y }, unit.PrevXy);
        Assert.Equal(landing.X, unit.X);
        Assert.Equal(landing.Y, unit.Y);
        Assert.Equal(unit.MaxMovePoints, unit.MovePointsLost);
        Assert.Equal((int)OrderType.NoOrders, unit.Order);
        // The runway that took the lift is full for this turn...
        Assert.Equal(1, landingCity.AirliftTurn);
        // ...and the one it left is still free.
        Assert.Equal(-1, originCity.AirliftTurn);
    }

    [Fact]
    public void TryAirlift_RefusesASecondLiftIntoTheSameCityInOneTurn()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var first = CityTile(map, cities, civ, 2, 2, withAirbase: true).Location;
        var second = CityTile(map, cities, civ, 6, 6, withAirbase: true).Location;
        var shared = CityTile(map, cities, civ, 15, 15, withAirbase: true);
        var game = BuildGame(map, cities, turn: 4);

        Assert.True(AirliftFunctions.TryAirlift(game.Object, Aircraft(civ, first), shared.Location));
        var refused = Aircraft(civ, second);

        Assert.False(AirliftFunctions.TryAirlift(game.Object, refused, shared.Location));
        AssertUntouched(refused, second);

        // The same destination opens up again at the next turn number.
        var nextTurn = BuildGame(map, cities, turn: 5);
        Assert.True(AirliftFunctions.TryAirlift(nextTurn.Object, refused, shared.Location));
        Assert.Equal(shared.Location, refused.CurrentLocation);
    }

    [Fact]
    public void CanAirlift_RefusesALandUnit()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var origin = CityTile(map, cities, civ, 2, 2, withAirbase: true).Location;
        var landing = CityTile(map, cities, civ, 15, 15, withAirbase: true).Location;
        var unit = Aircraft(civ, origin, domain: UnitGas.Ground);
        var game = BuildGame(map, cities);

        Assert.False(AirliftFunctions.CanAirlift(unit));
        Assert.False(AirliftFunctions.TryAirlift(game.Object, unit, landing));
        AssertUntouched(unit, origin);
    }

    [Fact]
    public void CanAirlift_RefusesAUnitThatAlreadyMoved()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var unit = Aircraft(civ, CityTile(map, cities, civ, 2, 2, withAirbase: true).Location);
        unit.MovePointsLost = 1;

        Assert.False(AirliftFunctions.CanAirlift(unit));
        Assert.Empty(AirliftFunctions.ValidTargets(BuildGame(map, cities).Object, unit));
    }

    [Fact]
    public void CanAirlift_RefusesAUnitAboardAShip()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var origin = CityTile(map, cities, civ, 2, 2, withAirbase: true).Location;
        var unit = Aircraft(civ, origin);
        unit.InShip = Aircraft(civ, origin);

        Assert.False(AirliftFunctions.CanAirlift(unit));
    }

    [Fact]
    public void CanAirlift_RequiresAnAirbaseUnderTheUnit()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };

        // A city with no runway, and open ground, are both no place to lift from.
        Assert.False(AirliftFunctions.CanAirlift(
            Aircraft(civ, CityTile(map, cities, civ, 2, 2, withAirbase: false).Location)));
        Assert.False(AirliftFunctions.CanAirlift(Aircraft(civ, map.Tile[9, 9])));
    }

    [Fact]
    public void CanAirlift_RequiresAFriendlyAirbaseUnderTheUnit()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var enemy = new Civilization { Id = 1 };
        var enemyBase = CityTile(map, cities, enemy, 2, 2, withAirbase: true).Location;

        Assert.False(AirliftFunctions.CanAirlift(Aircraft(civ, enemyBase)));
    }

    [Fact]
    public void ValidTargets_OffersOnlyFriendlyAirbasesWithCapacityLeft()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var enemy = new Civilization { Id = 1 };
        var originCity = CityTile(map, cities, civ, 2, 2, withAirbase: true);
        var unit = Aircraft(civ, originCity.Location);

        var welcome = CityTile(map, cities, civ, 8, 8, withAirbase: true).Location;
        var noRunway = CityTile(map, cities, civ, 10, 10, withAirbase: false).Location;
        var hostile = CityTile(map, cities, enemy, 12, 12, withAirbase: true).Location;
        var fullCity = CityTile(map, cities, civ, 14, 14, withAirbase: true);
        fullCity.AirliftTurn = 1;

        var targets = AirliftFunctions.ValidTargets(BuildGame(map, cities, turn: 1).Object, unit);

        Assert.Contains(welcome, targets);
        Assert.DoesNotContain(originCity.Location, targets);
        Assert.DoesNotContain(noRunway, targets);
        Assert.DoesNotContain(hostile, targets);
        Assert.DoesNotContain(fullCity.Location, targets);
    }

    [Fact]
    public void TryAirlift_RefusesAnEnemyCityAnAirbaselessCityAndItsOwnTile()
    {
        var map = BuildMap();
        var cities = new List<City>();
        var civ = new Civilization { Id = 0 };
        var enemy = new Civilization { Id = 1 };
        var origin = CityTile(map, cities, civ, 2, 2, withAirbase: true).Location;
        var unit = Aircraft(civ, origin);
        var game = BuildGame(map, cities);

        var hostile = CityTile(map, cities, enemy, 8, 8, withAirbase: true).Location;
        var noRunway = CityTile(map, cities, civ, 10, 10, withAirbase: false).Location;

        Assert.False(AirliftFunctions.TryAirlift(game.Object, unit, hostile));
        Assert.False(AirliftFunctions.TryAirlift(game.Object, unit, noRunway));
        Assert.False(AirliftFunctions.TryAirlift(game.Object, unit, origin));
        AssertUntouched(unit, origin);
    }
}
