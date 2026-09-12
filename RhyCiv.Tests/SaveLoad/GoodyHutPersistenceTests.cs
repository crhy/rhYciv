using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.SaveLoad;
using RhyCiv.Engine.SaveLoad.SavFile;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.GameRules;
using Model.Core.Mapping;

namespace RhyCiv.Tests.SaveLoad;

/// <summary>
/// A goody hut that has been entered stays entered.
/// <para>
/// Where the huts are is not stored. It is worked out from each square's
/// coordinates and the map's seed, the way Civ II does it, and
/// <see cref="Tile.RefreshGoodyHut"/> runs from the tile's constructor — so
/// loading a game put a hut back on every square the seed says should have one,
/// including all the ones already taken. Reported as huts appearing in country
/// that had already been explored.
/// </para>
/// </summary>
public class GoodyHutPersistenceTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("rhyciv-huts-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AHutThatHasBeenTaken_DoesNotComeBackWhenTheGameIsLoaded()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var taken = HutTiles(game).First();
        var (x, y) = (taken.X, taken.Y);
        taken.HasGoodieHut = false;          // what entering one does
        Assert.False(taken.HasGoodyHut);

        var reloaded = SaveAndLoad(game, ruleset);

        var sameTile = TileAt(reloaded, x, y);
        Assert.False(sameTile.HasGoodyHut,
            "a hut that had already been entered was standing again after loading");
    }

    [Fact]
    public void AHutNobodyHasVisited_IsStillThere()
    {
        // The other half of it: the fix must not simply strip the map of huts.
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var huts = HutTiles(game);
        Assert.True(huts.Count > 1, "the test map should carry more than one hut");
        huts[0].HasGoodieHut = false;
        var untouched = huts[1];
        var (x, y) = (untouched.X, untouched.Y);

        var reloaded = SaveAndLoad(game, ruleset);

        Assert.True(TileAt(reloaded, x, y).HasGoodyHut,
            "a hut nobody had been near was missing after loading");
    }

    private static List<Tile> HutTiles(IGame game) =>
        game.Maps[0].Tile.Cast<Tile>().Where(t => t is { HasGoodyHut: true }).ToList();

    private static Tile TileAt(IGame game, int x, int y) =>
        game.Maps[0].Tile.Cast<Tile>().Single(t => t.X == x && t.Y == y);

    private IGame SaveAndLoad(Game game, Ruleset ruleset)
    {
        var path = Path.Combine(_directory, "huts.sav");
        var serializer = new GameSerializer();
        AtomicFile.Write(path,
            stream => serializer.Write(stream, game, ruleset, new Dictionary<string, string>()));
        return new JsonSavFile().LoadGame(File.ReadAllBytes(path), ruleset,
            RulesParser.ParseRules(ruleset));
    }
}
