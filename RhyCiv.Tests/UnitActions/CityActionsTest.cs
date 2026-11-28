using RhyCiv.Engine;
using RhyCiv.Engine.Production;
using RhyCiv.Engine.UnitActions;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Constants;
using Model.Core.Mapping;
using Model.Core.Production;
using Model.Core.Units;
using Moq;

namespace RhyCiv.Tests.UnitActions;

public class CityActionsTest
{
    private static Mock<IGame> SetupGame(int citiesToCreate, out Civilization civ)
    {
        var game = new Mock<IGame>();
        civ = new Civilization { TribeName = "TestCiv" };
        game.Setup(g => g.AllCivilizations).Returns([civ]);
        
        var cityNames = new Dictionary<string, List<string>?>
        {
            ["TESTCIV"] = ["City1", "City2", "City3"]
        };
        game.Setup(g => g.CityNames).Returns(cityNames);
        
        var mockHistory = new Mock<IHistory>();
        game.Setup(g => g.History).Returns(mockHistory.Object);
        
        var citiesBuiltCount = new Dictionary<Civilization, int>
        {
            [civ] = citiesToCreate
        };
        game.Setup(g => g.CitiesBuiltSoFar).Returns(citiesBuiltCount);
        return game;
    }

    [Fact]
    public void GetCityName_SuggestsNameInOrder()
    {
        var game = SetupGame(0, out var civ);
        Assert.Equal("City1", CityActions.GetCityName(civ, game.Object));
        
        game.Object.CitiesBuiltSoFar[civ] = 1;
        Assert.Equal("City2", CityActions.GetCityName(civ, game.Object));
    }

    [Fact]
    public void GetCityName_ReturnsDummyName_WhenListExhausted()
    {
        var game = SetupGame(3, out var civ);
        Assert.Equal("Dummy Name", CityActions.GetCityName(civ, game.Object));
    }

    [Fact]
    public void GetCityName_UsesExtra_WhenTribeNotFound()
    {
        var game = new Mock<IGame>();
        var civ = new Civilization { TribeName = "UnknownTribe" };
        var cityNames = new Dictionary<string, List<string>?>
        {
            ["EXTRA"] = ["ExtraCity1"]
        };
        game.Setup(g => g.CityNames).Returns(cityNames);
        game.Setup(g => g.CitiesBuiltSoFar).Returns(new Dictionary<Civilization, int>());

        Assert.Equal("ExtraCity1", CityActions.GetCityName(civ, game.Object));
    }

    [Fact]
    public void BuildCity_InitializesCityCorrectly()
    {
        // Arrange
        var game = new Mock<IGame>();
        var civ = new Civilization { Id = 0, TribeName = "Romans", Government = 0, Advances = new bool[10] };
        var unitDef = new UnitDefinition { Move = 10, Flags = new bool[20], Cost = 20 };
        var unit = new Unit { Owner = civ, TypeDefinition = unitDef };
        
        var map = new Map(true, 0)
        {
            XDim = 10,
            YDim = 10,
            Tile = new Tile[10, 10]
        };
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                map.Tile[x, y] = new Tile(x * 2, y, new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>() }, 0, map, x, new bool[2]);
            }
        }

        var tile = map.Tile[0, 0];
        unit.CurrentLocation = tile;

        var rules = new Rules
        {
            Advances = [new Advance { Name = "None" }],
            Improvements = [],
            Cosmic =
            {
                FoodEatenPerTurn = 2
            },
            Governments = [new Government { Level = 0, SettlersConsumption = 1 }]
        };

        var mockProd = new Mock<IProductionOrder>();
        mockProd.Setup(p => p.Cost).Returns(10);
        mockProd.Setup(p => p.RequiredTech).Returns(AdvancesConstants.Nil);
        mockProd.Setup(p => p.ExpiresTech).Returns(AdvancesConstants.Nil);
        rules.ProductionOrders = [mockProd.Object];
        ProductionPossibilities.InitializeProductionLists([civ], rules.ProductionOrders);
        
        game.Setup(g => g.Rules).Returns(rules);
        game.Setup(g => g.AllCities).Returns(new List<City>());
        game.Setup(g => g.CitiesBuiltSoFar).Returns(new Dictionary<Civilization, int>());
        game.Setup(g => g.History).Returns(new Mock<IHistory>().Object);
        game.Setup(g => g.Maps).Returns(new List<Map> { map });
        game.Setup(g => g.MaxDistance).Returns(100.0);
        game.Setup(g => g.TerrainImprovements).Returns(new Dictionary<int, TerrainImprovement>());

        // Act
        var city = CityActions.BuildCity(unit, game.Object, "Rome");

        // Assert
        Assert.Equal("Rome", city.Name);
        Assert.Equal(tile, city.Location);
        Assert.Equal(civ, city.Owner);
        Assert.True(unit.Dead);
        Assert.Contains(city, game.Object.AllCities);
        Assert.Contains(city, civ.Cities);
        Assert.Equal(1, game.Object.CitiesBuiltSoFar[civ]);
        Assert.Equal(city, tile.CityHere);
        Assert.Equal(city, tile.WorkedBy);
    }

    // crash-20260906-233052.log and crash-20260916-001130.log (#203): founding the
    // first city of a civ ran the Capital-improvement search straight over the ruleset's
    // improvement table, indexing game.Rules.Advances[i.Prerequisite] and
    // AllowedAdvanceGroups[...AdvanceGroup] without checking the bounds first. A
    // ruleset whose Capital improvement carried an out-of-range prerequisite (the format
    // has a lot of them) threw System.IndexOutOfRangeException from BuildCity, killing
    // the barbarian turn that had walked a settler onto a goody hut. Fixed in c50d669e
    // (#205) by guarding the indexes; this locks that fix down. Remove the guards in
    // CityActions.BuildCity and this test throws IndexOutOfRangeException again.
    [Fact]
    public void BuildCity_WithOutOfRangeCapitalPrerequisite_DoesNotCrash()
    {
        var game = new Mock<IGame>();
        var civ = new Civilization { Id = 0, TribeName = "Romans", Government = 0, Advances = new bool[10] };
        var unitDef = new UnitDefinition { Move = 10, Flags = new bool[20], Cost = 20 };
        var unit = new Unit { Owner = civ, TypeDefinition = unitDef };

        var map = new Map(true, 0) { XDim = 10, YDim = 10, Tile = new Tile[10, 10] };
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
                map.Tile[x, y] = new Tile(x * 2, y, new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>() }, 0, map, x, new bool[2]);

        var tile = map.Tile[0, 0];
        unit.CurrentLocation = tile;

        // A Capital improvement whose prerequisite index is past the end of the
        // one-advance table -- exactly the bad-data shape that crashed the log.
        var capital = new Improvement { Name = "Capital marker", Cost = 1 };
        capital.Effects[Effects.Capital] = 1;
        capital.Prerequisite = 999;

        var rules = new Rules
        {
            Advances = [new Advance { Name = "None" }],
            Improvements = [capital],
            Cosmic = { FoodEatenPerTurn = 2 },
            Governments = [new Government { Level = 0, SettlersConsumption = 1 }]
        };

        var mockProd = new Mock<IProductionOrder>();
        mockProd.Setup(p => p.Cost).Returns(10);
        mockProd.Setup(p => p.RequiredTech).Returns(AdvancesConstants.Nil);
        mockProd.Setup(p => p.ExpiresTech).Returns(AdvancesConstants.Nil);
        rules.ProductionOrders = [mockProd.Object];
        ProductionPossibilities.InitializeProductionLists([civ], rules.ProductionOrders);

        game.Setup(g => g.Rules).Returns(rules);
        game.Setup(g => g.AllCities).Returns(new List<City>());
        game.Setup(g => g.CitiesBuiltSoFar).Returns(new Dictionary<Civilization, int>());
        game.Setup(g => g.History).Returns(new Mock<IHistory>().Object);
        game.Setup(g => g.Maps).Returns(new List<Map> { map });
        game.Setup(g => g.MaxDistance).Returns(100.0);
        game.Setup(g => g.TerrainImprovements).Returns(new Dictionary<int, TerrainImprovement>());

        var city = Record.Exception(() => CityActions.BuildCity(unit, game.Object, "Rome"));

        Assert.Null(city);
    }

    private static Mock<IProductionOrder> Order(ItemType type, int cost = 40)
    {
        var order = new Mock<IProductionOrder>();
        order.Setup(o => o.Type).Returns(type);
        order.Setup(o => o.Cost).Returns(cost);
        return order;
    }

    private static (City city, Rules rules) CityReadyToBuild()
    {
        var rules = new Rules { Advances = [], Improvements = [], Governments = [] };
        rules.Cosmic.ShieldPenaltyTypeChange = 50;
        var city = new City
        {
            Owner = new Civilization { Id = 1 },
            ItemInProduction = Order(ItemType.Unit).Object,
            ShieldsProgress = 40
        };
        return (city, rules);
    }

    [Fact]
    public void ChangeProduction_ChargesHalfWhenCrossingCategories()
    {
        var (city, rules) = CityReadyToBuild();

        city.ChangeProduction(Order(ItemType.Building).Object, rules);

        Assert.Equal(20, city.ShieldsProgress);
        Assert.True(city.ProductionChanged);
    }

    [Fact]
    public void ChangeProduction_IsFreeWithinACategory()
    {
        var (city, rules) = CityReadyToBuild();

        city.ChangeProduction(Order(ItemType.Unit, cost: 60).Object, rules);

        Assert.Equal(40, city.ShieldsProgress);
        Assert.False(city.ProductionChanged);
    }

    [Fact]
    public void ChangeProduction_ChargesOnlyOnceInATurn()
    {
        var (city, rules) = CityReadyToBuild();

        city.ChangeProduction(Order(ItemType.Building).Object, rules);
        city.ChangeProduction(Order(ItemType.Unit, cost: 10).Object, rules);

        // Half once, not half twice: changing your mind again is free.
        Assert.Equal(20, city.ShieldsProgress);
    }

    [Fact]
    public void ChangeProduction_UsesTheRulesetsOwnRate()
    {
        var (city, rules) = CityReadyToBuild();
        rules.Cosmic.ShieldPenaltyTypeChange = 25;

        city.ChangeProduction(Order(ItemType.Building).Object, rules);

        Assert.Equal(30, city.ShieldsProgress);
    }
}
