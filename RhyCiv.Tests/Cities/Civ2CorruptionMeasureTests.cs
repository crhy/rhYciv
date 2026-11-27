using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Units;
using Moq;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// The corruption figures crhy/rhYciv#127 measured against Civilization II on
/// tradesave.sav (A.D. 1700, turn 196, Republic). Kells and Carmarthen are the
/// same distance from the capital -- four columns and six rows -- and Civ II
/// gives Kells, with 10 trade, no corruption at all, and Carmarthen, with 16,
/// exactly one.
///
/// The engine stores tile X doubled (x = 2 * column + row % 2), so
/// Utilities.DistanceTo reads 10 for that offset (8 by 6, a doubled 3-4-5
/// triangle); the sourced DistanceFactor is that distance in Civ II map
/// squares -- 5 -- plus a difficulty term the sources keep at zero under
/// Republic. 10 x 5 x 15/8 / 100 = 0.9375 -> 0 and 16 x 5 x 15/8 / 100 = 1.5
/// -> 1: the measured pair, with no map-scale term anywhere.
/// </summary>
public class Civ2CorruptionMeasureTests
{
    private const int CapitalCol = 10;
    private const int CapitalRow = 4;
    // Four columns and six rows away, as Kells and Carmarthen sit from the
    // capital in tradesave.sav.
    private const int MeasureCol = CapitalCol + 4;
    private const int MeasureRow = CapitalRow + 6;

    // GovernmentType order: Anarchy, Despotism, Monarchy, Communism,
    // Fundamentalism, Republic, Democracy. Communism and Fundamentalism fix
    // their distance in the ruleset; Republic reads the distance rule.
    private static Government Despotism() => new()
        { Level = 0, SettlersConsumption = 1, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = -1 };

    private static (Mock<IGame> game, Civilization civ, Map map) SetupMeasure(bool flat = false)
    {
        var rules = new Rules
        {
            Governments = new[]
            {
                Despotism(),
                Despotism(),
                new Government { Level = 1, SettlersConsumption = 1, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = -1 },
                new Government { Level = 1, SettlersConsumption = 1, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = 0 },
                new Government { Level = 1, SettlersConsumption = 1, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = 0 },
                new Government { Level = 2, SettlersConsumption = 0, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = -1 }, // Republic
                new Government { Level = 2, SettlersConsumption = 0, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = 0 }
            }
        };
        rules.Cosmic.FoodEatenPerTurn = 2;
        var civ = new Civilization { Id = 1, Government = 5 }; // Republic
        // A round-world map by default, like the classic saves the measurement
        // came from; Utilities.DistanceTo reads true Euclidean distance on both
        // map kinds (rhYciv#127 / f227b), so the pair holds on flat earth too.
        var map = new Map(flat, 0) { XDim = 40, YDim = 12 };
        map.Tile = new Tile[40, 12];
        for (int y = 0; y < 12; y++)
        {
            for (int x_idx = 0; x_idx < 40; x_idx++)
            {
                int x = x_idx * 2 + y % 2;
                var tile = new Tile(x, y, new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>(), Defense = 100 }, 0, map, x_idx + y * 40, new bool[2]);
                tile.SetVisible(civ.Id);
                map.Tile[x_idx, y] = tile;
            }
        }

        var game = new Mock<IGame>();
        game.Setup(g => g.Rules).Returns(rules);
        game.Setup(g => g.MaxDistance).Returns(100.0);
        game.Setup(g => g.AllCivilizations).Returns(new List<Civilization> { civ });
        // Difficulty must not move corruption under Republic: the sources make
        // the difficulty term 0 always under Monarchy and Republic.
        game.SetupGet(g => g.DifficultyLevel).Returns(3);
        return (game, civ, map);
    }

    private static (Mock<IGame> game, City city) MeasureCity(int terrainTrade, bool flat = false)
    {
        var (game, civ, map) = SetupMeasure(flat);

        var capital = new City { Owner = civ, Location = map.Tile[CapitalCol, CapitalRow], Size = 1 };
        capital.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.Capital] = 0 } });
        civ.Cities.Add(capital);

        var tile = map.Tile[MeasureCol, MeasureRow];
        tile.Terrain.Food = 2;
        tile.Terrain.Shields = 4;
        tile.Terrain.Trade = terrainTrade;
        var city = new City { Owner = civ, Location = tile, Size = 1 };
        city.WorkedTiles.Add(tile);
        tile.CityHere = city;
        civ.Cities.Add(city);
        return (game, city);
    }

    // Kells in tradesave.sav: 10 trade at that distance corrupts not at all in
    // Civ II. (Organization 2 adds +1 trade, hence terrain 9.)
    [Fact]
    public void Kells_TradeTen_CorruptsToZero()
    {
        var (game, city) = MeasureCity(9);
        city.CalculateOutput(5, game.Object); // Republic
        Assert.Equal(10, city.TileTrade);
        Assert.Equal(0, city.Corruption);
    }

    // Carmarthen in tradesave.sav: 16 trade at the same distance corrupts by
    // exactly one in Civ II.
    [Fact]
    public void Carmarthen_TradeSixteen_CorruptsToOne()
    {
        var (game, city) = MeasureCity(15);
        city.CalculateOutput(5, game.Object); // Republic
        Assert.Equal(16, city.TileTrade);
        Assert.Equal(1, city.Corruption);
    }

    // f227b: on a flat map DistanceTo used to return the SQUARE, so these same
    // tiles read 100, DistanceFactor hit the 32/16 caps, and both cities were
    // corrupt everywhere. The measured pair must hold with the same unit on
    // both map kinds -- same distance 10, same DistanceFactor 5, same figures.
    [Fact]
    public void Kells_TradeTen_CorruptsToZero_FlatMap()
    {
        var (game, city) = MeasureCity(9, flat: true);
        city.CalculateOutput(5, game.Object); // Republic
        Assert.Equal(10, city.TileTrade);
        Assert.Equal(0, city.Corruption);
    }

    [Fact]
    public void Carmarthen_TradeSixteen_CorruptsToOne_FlatMap()
    {
        var (game, city) = MeasureCity(15, flat: true);
        city.CalculateOutput(5, game.Object); // Republic
        Assert.Equal(16, city.TileTrade);
        Assert.Equal(1, city.Corruption);
    }
}
