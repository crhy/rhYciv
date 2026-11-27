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
/// Table-driven verification of corruption and waste against government,
/// difficulty, city distance, courthouse effects and the capital, as
/// crhy/rhYciv#6 asks. Each row builds a real city next to a Capital city on
/// the map, runs the engine's <see cref="CityExtensions.CalculateOutput"/>, and
/// checks the engine's Corruption, Waste, Trade and Production against the
/// sourced Civ II formulas (Apolyton 62524, the tested DistanceFactor source),
/// reproduced here independently of the engine so a disagreement fails the row
/// rather than hiding in the expectation.
///
/// Sourced TenTen factors (Apolyton 62524, source (b)):
///   corruption: Anarchy 15/4, Despotism 15/5, Monarchy 15/6, Republic 15/8;
///               Communism, Fundamentalism and Democracy are always 0.
///   waste:      Anarchy 15/4, Despotism 15/5, Monarchy 15/12, Republic 15/24;
///               Communism, Fundamentalism and Democracy are always 0.
/// DistanceFactor: a Capital sits at distance 0; a fixed-distance government
///   (Communism, Fundamentalism, Democracy) uses the ruleset distance; every
///   other government uses Euclidean distance to the nearest Capital in Civ II
///   map squares (engine halves the doubled tile X), plus the difficulty term
///   chieftain=0..deity=5 only under Anarchy and Despotism (Level &lt; 1) and
///   always 0 under Monarchy and Republic. Corruption caps at DistanceFactor 32,
///   waste at 16, and a courthouse (ReduceCorruption) multiplies both.
/// </summary>
public class Civ2CorruptionWasteMatrixTests
{
    // GovernmentType order: Anarchy 0, Despotism 1, Monarchy 2, Communism 3,
    // Fundamentalism 4, Republic 5, Democracy 6.
    private const int Anarchy = (int)GovernmentType.Anarchy;
    private const int Despotism = (int)GovernmentType.Despotism;
    private const int Monarchy = (int)GovernmentType.Monarchy;
    private const int Communism = (int)GovernmentType.Communism;
    private const int Fundamentalism = (int)GovernmentType.Fundamentalism;
    private const int Republic = (int)GovernmentType.Republic;
    private const int Democracy = (int)GovernmentType.Democracy;

    private const int CapitalCol = 10;
    private const int CapitalRow = 4;
    // Two provincial cities at fixed map positions, as the issue asks: A due
    // south (four Civ II squares away), B due east (six squares away). The
    // engine stores tile X doubled, so Utilities.DistanceTo reads 8 and 12 and
    // the sourced DistanceFactor is half that -- 4 and 6.
    private const int CityACol = 10;
    private const int CityARow = 12; // DistanceTo 8 -> half 4
    private const int CityBCol = 16;
    private const int CityBRow = 4;  // DistanceTo 12 -> half 6

    // Fixed ruleset distance for the fixed-distance governments (Communism,
    // Fundamentalism, Democracy). Nonzero so the fixed-distance branch is
    // actually taken; their corruption and waste are sourced to be always 0.
    private const int FixedDistance = 7;

    private static Government Gov(int level, int distance) =>
        new() { Level = level, SettlersConsumption = 1, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = distance };

    private static Rules BuildRules()
    {
        var rules = new Rules
        {
            Governments = new[]
            {
                Gov(0, -1), // Anarchy        - distance from the map + difficulty
                Gov(0, -1), // Despotism      - ditto
                Gov(1, -1), // Monarchy       - distance from the map, no difficulty
                Gov(1, FixedDistance), // Communism     - fixed distance, always 0
                Gov(1, FixedDistance), // Fundamentalism - fixed distance, always 0
                Gov(2, -1), // Republic       - distance from the map, no difficulty
                Gov(2, FixedDistance) // Democracy - fixed distance, always 0
            }
        };
        rules.Cosmic.FoodEatenPerTurn = 2;
        return rules;
    }

    private static (City city, City capital, Mock<IGame> game) BuildCity(
        int government, int terrainTrade, int terrainShields, int cityCol, int cityRow, int difficulty, bool courthouse)
    {
        var rules = BuildRules();
        var civ = new Civilization { Id = 1, Government = government };

        var map = new Map(false, 0) { XDim = 40, YDim = 20 };
        map.Tile = new Tile[40, 20];
        for (int y = 0; y < 20; y++)
        {
            for (int xIdx = 0; xIdx < 40; xIdx++)
            {
                int x = xIdx * 2 + y % 2;
                var tile = new Tile(x, y, new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>(), Defense = 100 }, 0, map, xIdx + y * 40, new bool[2]);
                tile.SetVisible(civ.Id);
                map.Tile[xIdx, y] = tile;
            }
        }

        var capitalTile = map.Tile[CapitalCol, CapitalRow];
        var capital = new City { Owner = civ, Location = capitalTile, Size = 1 };
        capital.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.Capital] = 0 } });
        civ.Cities.Add(capital);

        var cityTile = map.Tile[cityCol, cityRow];
        cityTile.Terrain.Food = 2;
        cityTile.Terrain.Shields = terrainShields;
        cityTile.Terrain.Trade = terrainTrade;
        var city = new City { Owner = civ, Location = cityTile, Size = 1 };
        city.WorkedTiles.Add(cityTile);
        cityTile.CityHere = city;
        civ.Cities.Add(city);

        if (courthouse)
        {
            city.AddImprovement(new Improvement { Type = 2, Effects = { [Effects.ReduceCorruption] = 50 } });
        }

        var game = new Mock<IGame>();
        game.Setup(g => g.Rules).Returns(rules);
        game.Setup(g => g.MaxDistance).Returns(100.0);
        game.Setup(g => g.AllCivilizations).Returns(new List<Civilization> { civ });
        game.SetupGet(g => g.DifficultyLevel).Returns(difficulty);
        return (city, capital, game);
    }

    // ---- the sourced formula, written independently of the engine ----

    private static double SourcedCorruptionTenTen(int government) => government switch
    {
        Anarchy => 15d / 4, Despotism => 15d / 5, Monarchy => 15d / 6, Republic => 15d / 8,
        _ => 0d // Communism, Fundamentalism, Democracy: always 0
    };

    private static double SourcedWasteTenTen(int government) => government switch
    {
        Anarchy => 15d / 4, Despotism => 15d / 5, Monarchy => 15d / 12, Republic => 15d / 24,
        _ => 0d // Communism, Fundamentalism, Democracy: always 0
    };

    // The nearest capital's distance factor as the sources define it: 0 for a
    // capital, the fixed ruleset distance for the fixed-distance governments,
    // and the half-halved Euclidean distance (plus difficulty under Anarchy and
    // Despotism only) for everyone else.
    private static double SourcedDistance(int government, int difficulty, int cityCol, int cityRow)
    {
        if (government is Communism or Fundamentalism or Democracy)
        {
            return FixedDistance;
        }

        double half = HalfDistanceToCapital(cityCol, cityRow);
        return government is Anarchy or Despotism ? half + difficulty : half;
    }

    // Utilities.DistanceTo reads the doubled tile X (x = 2 * column + row % 2),
    // so the due-east city four columns over reads 8 across / 2 =... -- the
    // engine stores tile X doubled, the sourced DistanceFactor is half of that
    // Euclidean figure: 4 due south (six rows), 6 due east (six columns).
    private static double HalfDistanceToCapital(int cityCol, int cityRow)
    {
        double dx = (cityCol - CapitalCol) * 2;
        double dy = cityRow - CapitalRow;
        return Math.Sqrt(dx * dx + dy * dy) / 2;
    }

    private static void AssertRow(int government, int difficulty, bool courthouse, int cityCol, int cityRow)
    {
        var (city, _, game) = BuildCity(government, 10, 4, cityCol, cityRow, difficulty, courthouse);
        city.CalculateOutput(government, game.Object);

        double distance = SourcedDistance(government, difficulty, cityCol, cityRow);
        double modifier = courthouse ? 0.5 : 1.0;

        int expectedCorruption = (int)Math.Floor(
            city.TileTrade * Math.Min(32, distance) * SourcedCorruptionTenTen(government) / 100 * modifier);
        int expectedWaste = (int)Math.Floor(
            (city.TotalProduction - 0) * Math.Min(16, distance) * SourcedWasteTenTen(government) / 100 * modifier);

        Assert.Equal(expectedCorruption, city.Corruption);
        Assert.Equal(expectedWaste, city.Waste);
        // Trade and Production fall out of the same corruption/waste the engine applied.
        Assert.Equal(city.TileTrade - city.Corruption, city.Trade);
        Assert.Equal(city.TotalProduction - city.Waste, city.Production);
    }

    public static IEnumerable<object[]> Matrix()
    {
        foreach (int government in new[] { Anarchy, Despotism, Monarchy, Communism, Fundamentalism, Republic, Democracy })
        foreach (int difficulty in new[] { 0, 1, 2, 3, 4, 5 })   // chieftain .. deity
        foreach (bool courthouse in new[] { false, true })
        foreach (var (col, row) in new[] { (CityACol, CityARow), (CityBCol, CityBRow) })
        {
            yield return new object[] { government, difficulty, courthouse, col, row };
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void CorruptionAndWaste_MatchSourcedRules(int government, int difficulty, bool courthouse, int col, int row)
    {
        AssertRow(government, difficulty, courthouse, col, row);
    }

    // A Capital sits at distance 0 and is corrupt and wastes nothing under every
    // government and difficulty, courthouse or not.
    public static IEnumerable<object[]> CapitalCases()
    {
        foreach (int government in new[] { Anarchy, Despotism, Monarchy, Communism, Fundamentalism, Republic, Democracy })
        foreach (int difficulty in new[] { 0, 1, 2, 3, 4, 5 })   // chieftain .. deity
        foreach (bool courthouse in new[] { false, true })
        {
            yield return new object[] { government, difficulty, courthouse };
        }
    }

    [Theory]
    [MemberData(nameof(CapitalCases))]
    public void Capital_SitsAtDistanceZero_AndNeverCorruptsOrWastes(int government, int difficulty, bool courthouse)
    {
        var (city, capital, game) = BuildCity(government, 10, 4, CityACol, CityARow, difficulty, courthouse);
        capital.CalculateOutput(government, game.Object);
        Assert.Equal(0, capital.Corruption);
        Assert.Equal(0, capital.Waste);
        Assert.Equal(capital.TileTrade, capital.Trade);
        Assert.Equal(capital.TotalProduction, capital.Production);
    }

    // A worked example with fixed numbers, so the matrix is anchored to concrete
    // Civ II figures rather than only to the formula it mirrors. A Despotism
    // capital four squares away (Anarchy/Despotism add difficulty to distance):
    // total trade 9 (10 - 1 low organisation), distance 4 + deity(5) = 9,
    // corruption 9 * 9 * 15/5 / 100 = 2.43 -> 2; waste 3 * 9 * 15/5 / 100 = 0.81 -> 0.
    [Fact]
    public void Despotism_DifficultyAddsToDistance_KnownExample()
    {
        var (city, _, game) = BuildCity(Despotism, 10, 4, CityACol, CityARow, 5, courthouse: false);
        city.CalculateOutput(Despotism, game.Object);
        Assert.Equal(9, city.TileTrade);
        Assert.Equal(2, city.Corruption);
        Assert.Equal(0, city.Waste);
    }

    // Monarchy four squares from the capital, deity difficulty ignored (source:
    // difficulty is always 0 under Monarchy): total trade 10, distance 4,
    // corruption 10 * 4 * 15/6 / 100 = 1.0 -> 1; waste 4 * 4 * 15/12 / 100 = 0.2 -> 0.
    [Fact]
    public void Monarchy_IgnoresDifficulty_KnownExample()
    {
        var (city, _, game) = BuildCity(Monarchy, 10, 4, CityACol, CityARow, 5, courthouse: false);
        city.CalculateOutput(Monarchy, game.Object);
        Assert.Equal(10, city.TileTrade);
        Assert.Equal(1, city.Corruption);
        Assert.Equal(0, city.Waste);
    }

    // A courthouse (ReduceCorruption) halves both corruption and waste: the same
    // Republic six-square city without the courthouse corrupts 11 * 6 * 15/8 / 100
    // = 1.23 -> 1 and wastes 4 * 6 * 15/24 / 100 = 0.15 -> 0; the corruption half
    // of 11 * 6 * 15/8 / 100 * 1/2 = 0.62 -> 0 pins the courthouse multiplier.
    [Fact]
    public void Courthouse_HalvesCorruption_KnownExample()
    {
        var (plain, _, plainGame) = BuildCity(Republic, 10, 4, CityBCol, CityBRow, 0, courthouse: false);
        plain.CalculateOutput(Republic, plainGame.Object);
        var (hall, _, hallGame) = BuildCity(Republic, 10, 4, CityBCol, CityBRow, 0, courthouse: true);
        hall.CalculateOutput(Republic, hallGame.Object);
        Assert.Equal(11, plain.TileTrade);
        Assert.Equal(1, plain.Corruption);
        Assert.Equal(0, hall.Corruption); // 11 * 6 * 15/8 / 100 * 1/2 = 0.62 -> 0
    }
}
