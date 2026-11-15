using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Tests.TestFiles;
using Model.Core.Cities;
using Model.Core.Mapping;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// Super Highways and the trade of the squares a city works.
/// <para>
/// Civ II: a city holding Super Highways takes 50% more trade from the worked
/// tiles that carry a road (issue #28; docs/CIV2-PARITY-DEEP-AUDIT.md TR1 listed
/// it as the one building in that group with no tile effect attached at all, and
/// the owner's comment there named the missing piece as "a new effect plus a hook
/// in the tile-yield path"). The standalone Civilopedia already promises the
/// figure: "50 percent more trade from those developed squares". The number is
/// taken from this repo's own audit and that comment, not from a fresh Civ II
/// measurement.
/// </para>
/// <para>
/// The rule belongs in the tile-yield path because every consumer reads it -- the
/// city's output, the worked-window tile map, the auto-start -- so the window and
/// the city's totals cannot be made to disagree.
/// </para>
/// </summary>
public class SuperhighwayTradeTests
{
    private const int BaseTrade = 2;

    [Fact]
    public void ARoadedWorkedSquareGivesHalfAgainAsMuchTrade()
    {
        var (tile, _) = WorkedSquare(withRoad: true, withSuperhighways: true);

        // 2 trade becomes 3: the x1.5 the rules table writes as 150.
        Assert.Equal(3, tile.GetTrade(1));
    }

    [Fact]
    public void AnUnroadedWorkedSquareIsLeftAlone()
    {
        var (tile, _) = WorkedSquare(withRoad: false, withSuperhighways: true);

        Assert.Equal(BaseTrade, tile.GetTrade(1));
    }

    [Fact]
    public void ARoadWithoutSuperHighwaysIsLeftAlone()
    {
        var (tile, _) = WorkedSquare(withRoad: true, withSuperhighways: false);

        Assert.Equal(BaseTrade, tile.GetTrade(1));
    }

    [Theory]
    // The raised figure is what the organisation rules then act on: 3 - 1 when
    // organisation is off, 3 + 1 when it is strong.
    [InlineData(0, 2)]
    [InlineData(3, 4)]
    public void TheOrganisationRulesStillActOnTheRaisedFigure(int organizationLevel, int expected)
    {
        var (tile, _) = WorkedSquare(withRoad: true, withSuperhighways: true);

        Assert.Equal(expected, tile.GetTrade(organizationLevel));
    }

    [Fact]
    public void TheScriptedRuleIsLiveInTheLoadedRules()
    {
        // improvements.lua runs while the game loads, so the effect is not only
        // in the script file: it is on the improvement object the cities hold.
        var (_, _, rules) = CleanRoomGameFactory.CreateGame();
        var effects = rules.Improvements[(int)ImprovementType.Superhighways].TerrainEffects;

        var effect = Assert.Single(effects);
        Assert.Equal(ImprovementConstants.Trade, effect.Resource);
        Assert.Equal(CityTerrainEffect.Multiply, effect.Action);
        Assert.Equal(150, effect.Value);
        Assert.Equal(ImprovementTypes.Road, effect.Improvement);
        Assert.Equal(1, effect.Level);
    }

    [Fact]
    public void TheTradeRuleLeavesFoodAndShieldsAlone()
    {
        var (tile, _) = WorkedSquare(withRoad: true, withSuperhighways: true);

        // The effect asks for Trade, so the other two yields of the same square
        // must not move with it.
        Assert.Equal(2, tile.GetFood(false));
        Assert.Equal(1, tile.GetShields(false));
    }

    /// <summary>
    /// One worked square of ground that trades for 2, with or without a road on
    /// it, and the city working it with or without the improvement the script
    /// defined. The improvement comes from the loaded rules, so the scripted
    /// effect -- not a hand-written copy of it -- is what these tests exercise.
    /// </summary>
    private static (Tile Tile, City City) WorkedSquare(bool withRoad, bool withSuperhighways)
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var civ = game.GetPlayerCiv;

        var map = new Map(true, 0) { Tile = new Tile[2, 2], XDim = 2, YDim = 2 };
        var ground = new Terrain
        {
            Type = TerrainType.Plains,
            Name = "Test plain",
            MoveCost = 1,
            Defense = 2,
            Food = 2,
            Shields = 1,
            Trade = BaseTrade,
            Specials = []
        };
        var tile = new Tile(1, 1, ground, 1, map, 1, new bool[4]);
        map.Tile[1, 1] = tile;

        var city = new City { Owner = civ, Location = tile, Size = 1, Name = "Test" };
        tile.WorkedBy = city;

        if (withSuperhighways)
        {
            city.OrderedImprovements.Add((int)ImprovementType.Superhighways,
                rules.Improvements[(int)ImprovementType.Superhighways]);
        }

        if (withRoad)
        {
            // The road the effect asks for, at the level the effect asks for.
            tile.Improvements.Add(new ConstructedImprovement { Improvement = ImprovementTypes.Road, Level = 1 });
        }

        return (tile, city);
    }
}
