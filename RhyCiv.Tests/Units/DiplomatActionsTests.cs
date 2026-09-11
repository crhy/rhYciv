using RhyCiv.Engine.Enums;
using RhyCiv.Engine.UnitActions;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Tests.Units;
using Mocks;

/// <summary>
/// A Diplomat's whole purpose is buying what cannot be taken. None of it existed:
/// a Diplomat has no attack strength, so walking one into an enemy unit or city was
/// refused outright and the unit was good for nothing.
/// </summary>
public class DiplomatActionsTests
{
    [Fact]
    public void ARicherOwner_ChargesMoreForTheSameUnit()
    {
        var (game, _, _) = World();
        var poor = Rival(money: 100);
        var rich = Rival(money: 5000);

        var cheap = DiplomatActions.BribeCost(game, UnitOf(poor, game));
        var dear = DiplomatActions.BribeCost(game, UnitOf(rich, game));

        Assert.True(dear > cheap,
            $"a rich owner's unit should cost more to buy ({dear} vs {cheap})");
    }

    [Fact]
    public void AUnitFurtherFromItsCapital_IsCheaper()
    {
        var (game, map, _) = World();
        var owner = Rival(money: 2000);
        GiveCapital(owner, map.Tile[1, 1]);

        var near = DiplomatActions.BribeCost(game, UnitOf(owner, game, map.Tile[2, 2]));
        var far = DiplomatActions.BribeCost(game, UnitOf(owner, game, map.Tile[9, 9]));

        Assert.True(far < near,
            $"a unit on a far frontier should be cheaper than one by the capital ({far} vs {near})");
    }

    [Fact]
    public void AVeteran_CostsMore()
    {
        var (game, _, _) = World();
        var owner = Rival(money: 2000);

        var regular = UnitOf(owner, game);
        var veteran = UnitOf(owner, game);
        veteran.Veteran = true;

        Assert.True(DiplomatActions.BribeCost(game, veteran) > DiplomatActions.BribeCost(game, regular));
    }

    [Fact]
    public void TheRulesetMinimum_IsAFloor()
    {
        var (game, _, _) = World();
        var owner = Rival(money: 0);
        var target = UnitOf(owner, game);
        target.TypeDefinition.MinBribe = 9999;

        Assert.Equal(9999, DiplomatActions.BribeCost(game, target));
    }

    [Fact]
    public void ALoneUnit_CanBeBought()
    {
        var (game, map, mine) = World();
        var owner = Rival(money: 500);
        var tile = map.Tile[4, 4];
        var target = UnitOf(owner, game, tile);
        var diplomat = DiplomatOf(mine, game, map.Tile[4, 3]);

        Assert.NotNull(DiplomatActions.BribableUnitAt(diplomat, tile));
    }

    [Fact]
    public void AStack_CannotBeBought()
    {
        var (game, map, mine) = World();
        var owner = Rival(money: 500);
        var tile = map.Tile[4, 4];
        UnitOf(owner, game, tile);
        UnitOf(owner, game, tile);
        var diplomat = DiplomatOf(mine, game, map.Tile[4, 3]);

        // Gold buys one commander. Two units watching each other cannot both be
        // turned at once, which is why standing a second unit beside a valuable one
        // is worth doing.
        Assert.Null(DiplomatActions.BribableUnitAt(diplomat, tile));
    }

    [Fact]
    public void BuyingAUnit_TakesTheGoldAndTheUnitAndLeavesTheAgentAlive()
    {
        var (game, map, mine) = World();
        mine.Money = 10_000;
        var owner = Rival(money: 200);
        var tile = map.Tile[4, 4];
        var target = UnitOf(owner, game, tile);
        var diplomat = DiplomatOf(mine, game, map.Tile[4, 3]);

        var cost = DiplomatActions.BribeCost(game, target);
        Assert.True(DiplomatActions.BribeUnit(game, diplomat, target));

        Assert.Equal(10_000 - cost, mine.Money);
        Assert.Same(mine, target.Owner);
        Assert.Contains(target, mine.Units);
        Assert.DoesNotContain(target, owner.Units);
        // It has changed sides, not been handed fresh orders.
        Assert.Equal(0, target.MovePoints);

        // And the agent lives. Civ II's mission table gives "Mission Success" for
        // Bribe Unit and nothing else, for a Diplomat and a Spy alike: it is the
        // one job either walks away from, while every other mission kills a
        // Diplomat outright. This used to spend the Diplomat as though it had
        // incited a revolt, so turning one warrior cost the agent as well as the
        // gold.
        Assert.False(diplomat.Dead);
        // It has spent the day on it: a whole move point, which is what stops one
        // agent working down a line of units in a single turn.
        Assert.Equal(6 - 3, diplomat.MovePoints);
    }

    [Fact]
    public void BuyingAUnit_IsRefusedWithoutTheGold()
    {
        var (game, map, mine) = World();
        mine.Money = 1;
        var owner = Rival(money: 5000);
        var target = UnitOf(owner, game, map.Tile[4, 4]);
        var diplomat = DiplomatOf(mine, game, map.Tile[4, 3]);

        Assert.False(DiplomatActions.BribeUnit(game, diplomat, target));
        Assert.Same(owner, target.Owner);
        Assert.False(diplomat.Dead);
    }

    [Fact]
    public void ACapital_CannotBeBought()
    {
        var (game, map, _) = World();
        var owner = Rival(money: 500);
        var capital = GiveCapital(owner, map.Tile[1, 1]);

        Assert.False(DiplomatActions.CanIncite(capital));
    }

    [Fact]
    public void BuyingACity_TakesTheCityAndItsGarrison()
    {
        var (game, map, mine) = World();
        mine.Money = 100_000;
        var owner = Rival(money: 300);
        GiveCapital(owner, map.Tile[1, 1]);

        var tile = map.Tile[6, 6];
        var city = CityAt(owner, tile, size: 3);
        var garrison = UnitOf(owner, game, tile);
        var diplomat = DiplomatOf(mine, game, map.Tile[6, 5]);

        Assert.True(DiplomatActions.InciteRevolt(game, diplomat, city));

        Assert.Same(mine, city.Owner);
        Assert.Contains(city, mine.Cities);
        Assert.DoesNotContain(city, owner.Cities);
        // The garrison comes over with the city; leaving it behind would put an
        // enemy unit inside a city the player now owns.
        Assert.Same(mine, garrison.Owner);
        Assert.True(diplomat.Dead);
    }

    [Fact]
    public void ABiggerCity_CostsMore()
    {
        var (game, map, _) = World();
        var owner = Rival(money: 2000);
        GiveCapital(owner, map.Tile[1, 1]);

        var small = CityAt(owner, map.Tile[6, 6], size: 1);
        var large = CityAt(owner, map.Tile[6, 7], size: 8);

        Assert.True(DiplomatActions.InciteCost(game, large) > DiplomatActions.InciteCost(game, small));
    }

    // ---- fixture -----------------------------------------------------------

    // Both actions report the loss through game.Players, which is indexed by
    // civilisation id, so every civilisation a test uses has to exist before the
    // player list is built. The pool is handed out by Rival().
    private const int Civilisations = 9;

    private readonly List<Civilization> _civs = new();
    private int _handedOut;

    private (MockGame Game, Map Map, Civilization Mine) World()
    {
        var map = new Map(true, 0) { Tile = new Tile[12, 12], XDim = 12, YDim = 12 };
        var terrain = new Terrain { Type = TerrainType.Grassland, MoveCost = 1, Specials = [] };
        for (var x = 0; x < map.XDim; x++)
        for (var y = 0; y < map.YDim; y++)
        {
            map.Tile[x, y] = new Tile(x * 2 + y % 2, y, terrain, 1, map, x, new bool[Civilisations]);
        }

        for (var id = 0; id < Civilisations; id++)
        {
            _civs.Add(new Civilization { Id = id, TribeName = $"Civ{id}", Money = 0 });
        }

        var game = new MockGame
        {
            AllCivilizations = _civs.ToList(),
            Maps = new List<Map> { map },
            Rules = new Rules(),
            Players = _civs.Select(civ => (Model.Core.Player.IPlayer)new MockPlayer(civ)).ToArray()
        };

        return (game, map, Rival(money: 1000));
    }

    /// <summary>The next unused civilisation, with a treasury.</summary>
    private Civilization Rival(int money)
    {
        var civ = _civs[_handedOut++];
        civ.Money = money;
        return civ;
    }

    private static Unit UnitOf(Civilization owner, MockGame game, Tile? tile = null) =>
        Place(new Unit
        {
            Owner = owner,
            Dead = false,
            TypeDefinition = new UnitDefinition
            {
                Name = "Warriors",
                Domain = UnitGas.Ground,
                Move = 3,
                Attack = 1,
                Defense = 1,
                Flags = Enumerable.Repeat(false, 20).ToArray()
            }
        }, owner, tile ?? game.Maps[0].Tile[5, 5]);

    private static Unit DiplomatOf(Civilization owner, MockGame game, Tile tile) =>
        Place(new Unit
        {
            Owner = owner,
            Dead = false,
            TypeDefinition = new UnitDefinition
            {
                Name = "Diplomat",
                Domain = UnitGas.Ground,
                Move = 6,
                Attack = 0,
                Defense = 1,
                AIrole = AiRoleType.Diplomacy,
                Flags = Enumerable.Repeat(false, 20).ToArray()
            }
        }, owner, tile);

    private static Unit Place(Unit unit, Civilization owner, Tile tile)
    {
        unit.X = tile.X;
        unit.Y = tile.Y;
        unit.CurrentLocation = tile;
        owner.Units.Add(unit);
        return unit;
    }

    private static City CityAt(Civilization owner, Tile tile, int size)
    {
        var city = new City { Name = $"City{tile.X}", Location = tile, Owner = owner, Size = size };
        tile.CityHere = city;
        owner.Cities.Add(city);
        return city;
    }

    private static City GiveCapital(Civilization owner, Tile tile)
    {
        var city = CityAt(owner, tile, size: 4);
        var palace = new Improvement { Name = "Palace", Type = 1 };
        palace.Effects[Effects.Capital] = 1;
        city.OrderedImprovements[palace.Type] = palace;
        return city;
    }
}
