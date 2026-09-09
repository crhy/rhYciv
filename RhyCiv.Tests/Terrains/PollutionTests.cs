using RhyCiv.Engine;
using RhyCiv.Tests.Mocks;
using Model.Constants;
using Model.Core;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Player;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// Pollution and the climate.
/// <para>
/// A city works out how much pollution it makes, the improvement exists, settlers
/// can clear it and the messages are written -- but until now nothing ever put
/// any of it on the map, so none of that ran and every defence against it
/// (Mass Transit, the Recycling Center, the Solar Plant, Hoover Dam) protected
/// against nothing.
/// </para>
/// </summary>
public class PollutionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "rhyciv-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Warming is an advanced setting and is off unless the player asks for it, so
    /// these turn it on -- against a settings directory of their own, never the
    /// player's, since turning it on writes it out.
    /// </summary>
    public PollutionTests()
    {
        Settings.DataFolder = () => _folder;
        Settings.SetAdvancedSettings(globalWarming: true, cheatMenu: false, editorMenu: false);
    }

    public void Dispose()
    {
        Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false);
        Settings.DataFolder = () => Path.Combine(Path.GetTempPath(), "rhyciv-tests-unset");
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void WarmingIsOffUntilItIsAskedFor()
    {
        var game = World(out var map);
        PolluteSquares(map, PollutionFunctions.WarmingThreshold * 4);
        Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false);

        for (var turn = 0; turn < 50; turn++)
        {
            Assert.False(PollutionFunctions.ResolveGlobalWarming(game));
        }
    }

    [Fact]
    public void PollutedSquaresCountsWhatIsOnTheMap()
    {
        var game = World(out var map);
        Pollute(map.Tile[2, 2]);
        Pollute(map.Tile[5, 6]);

        Assert.Equal(2, PollutionFunctions.PollutedSquares(game).Count);
    }

    [Fact]
    public void ACleanWorldDoesNotWarm()
    {
        var game = World(out _);

        Assert.False(PollutionFunctions.ResolveGlobalWarming(game));
    }

    [Fact]
    public void PollutionUpToTheThresholdIsToleratedIndefinitely()
    {
        var game = World(out var map);
        PolluteSquares(map, PollutionFunctions.WarmingThreshold);

        // A hundred turns at the limit and the climate holds.
        for (var turn = 0; turn < 100; turn++)
        {
            Assert.False(PollutionFunctions.ResolveGlobalWarming(game));
        }
    }

    [Fact]
    public void EnoughPollutionEventuallyChangesTheWorld()
    {
        var game = World(out var map);
        PolluteSquares(map, PollutionFunctions.WarmingThreshold * 2);

        var warmed = false;
        for (var turn = 0; turn < 20 && !warmed; turn++)
        {
            warmed = PollutionFunctions.ResolveGlobalWarming(game);
        }

        Assert.True(warmed, "Twenty turns at twice the threshold and nothing happened.");
    }

    [Fact]
    public void WarmingTurnsForestToJungleAndTellsEverybody()
    {
        var game = World(out var map);
        PolluteSquares(map, PollutionFunctions.WarmingThreshold * 4);

        while (!PollutionFunctions.ResolveGlobalWarming(game))
        {
        }

        var jungle = AllTiles(map).Count(tile => tile.Type == TerrainType.Jungle);
        Assert.True(jungle > 0, "The forest was left standing.");

        foreach (var player in game.Players.Cast<MockPlayer>())
        {
            Assert.True(player.WarmedSquares > 0, "A civilisation was not told the climate had shifted.");
        }
    }

    [Fact]
    public void WarmingLeavesTheSeaAlone()
    {
        var game = World(out var map);
        var ocean = map.Tile[0, 0];
        ocean.Terrain = Terrain(TerrainType.Ocean);
        PolluteSquares(map, PollutionFunctions.WarmingThreshold * 4);

        for (var turn = 0; turn < 50; turn++)
        {
            PollutionFunctions.ResolveGlobalWarming(game);
        }

        Assert.Equal(TerrainType.Ocean, ocean.Type);
    }

    /// <summary>
    /// A forested world with two civilisations watching it, and the pollution
    /// improvement defined as the ruleset defines it.
    /// </summary>
    private static MockGame World(out Map map)
    {
        map = new Map(true, 0) { Tile = new Tile[12, 12], XDim = 12, YDim = 12 };
        var forest = Terrain(TerrainType.Forest);

        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 12; x++)
            {
                map.Tile[x, y] = new Tile(x, y, forest, 1, map, x, new bool[4]);
            }
        }

        var terrains = System.Enum.GetValues<TerrainType>().Select(Terrain).ToArray();
        var civilizations = new List<Civilization>
        {
            new() { Id = 0, TribeName = "Americans", Alive = true },
            new() { Id = 1, TribeName = "Romans", Alive = true }
        };

        return new MockGame
        {
            Maps = [map],
            Rules = new Rules { Terrains = [terrains] },
            AllCivilizations = civilizations,
            Players = civilizations.Select(civ => (IPlayer)new MockPlayer(civ)).ToArray()
        };
    }

    private static Terrain Terrain(TerrainType type) => new() { Type = type, Specials = [] };

    private static void Pollute(Tile tile) =>
        tile.Improvements.Add(new ConstructedImprovement { Improvement = ImprovementTypes.Pollution, Level = 0 });

    private static void PolluteSquares(Map map, int count)
    {
        foreach (var tile in AllTiles(map).Take(count))
        {
            Pollute(tile);
        }
    }

    private static IEnumerable<Tile> AllTiles(Map map) => map.Tile.Cast<Tile>().Where(tile => tile != null);
}
