using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.SaveLoad;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.GameRules;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.SaveLoad.SavFile;

namespace RhyCiv.Tests.SaveLoad;

/// <summary>
/// Saving and loading a real game, rather than a hand-built stand-in.
/// <para>
/// The other tests here cover the serializer against objects assembled for the
/// purpose, which is worth having but cannot catch a field that a real game fills
/// in and the writer forgets. These start from the same generated game the rest of
/// the suite uses, play a little of it, write it out and read it back.
/// </para>
/// </summary>
public class SaveRoundTripTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("rhyciv-save-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void APlayedGame_ComesBackAsItWentIn()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var civ = game.GetPlayerCiv;
        civ.Money = 4321;
        var settler = civ.Units.First(unit => !unit.Dead);
        var city = CityActions.BuildCity(settler, game, "Roundtrip");
        city.ShieldsProgress = 7;

        var expectedTurn = game.TurnNumber;
        var expectedCities = civ.Cities.Count;
        var expectedUnits = civ.Units.Count(unit => !unit.Dead);
        var expectedLocation = (city.Location.X, city.Location.Y);

        var reloaded = SaveAndLoad(game, ruleset);
        var reloadedCiv = reloaded.AllCivilizations.First(c => c.Id == civ.Id);
        var reloadedCity = reloadedCiv.Cities.Single();

        Assert.Equal(expectedTurn, reloaded.TurnNumber);
        Assert.Equal(4321, reloadedCiv.Money);
        Assert.Equal(expectedCities, reloadedCiv.Cities.Count);
        Assert.Equal(expectedUnits, reloadedCiv.Units.Count(unit => !unit.Dead));
        Assert.Equal("Roundtrip", reloadedCity.Name);
        Assert.Equal(expectedLocation, (reloadedCity.Location.X, reloadedCity.Location.Y));
        Assert.Equal(7, reloadedCity.ShieldsProgress);
    }

    [Fact]
    public void ScriptDataOnAUnit_SurvivesTheRoundTrip()
    {
        // Extended data is a dictionary, and the save writer treated every
        // dictionary as a plain sequence -- so it came out as an array of
        // {Key, Value} objects while the reader expected an object, and the save
        // could not be read back at all. Scripts put this on units routinely: the
        // barbarians carry a horde flag, so in practice a game became unloadable as
        // soon as barbarians appeared.
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var unit = game.GetPlayerCiv.Units.First(u => !u.Dead);
        unit.ExtendedData["horde"] = "1";
        unit.ExtendedData["errand"] = "scout the coast";

        var reloaded = SaveAndLoad(game, ruleset);
        var reloadedUnit = reloaded.AllCivilizations
            .First(civ => civ.Id == game.GetPlayerCiv.Id)
            .Units.First(u => !u.Dead && u.ExtendedData.Count > 0);

        Assert.Equal("1", reloadedUnit.ExtendedData["horde"]);
        Assert.Equal("scout the coast", reloadedUnit.ExtendedData["errand"]);
    }

    [Fact]
    public void ASaveFromBeforeTheDictionaryFix_StillLoads()
    {
        // Older builds wrote extended data as an array of key/value objects. A game
        // saved then must still open.
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        game.GetPlayerCiv.Units.First(u => !u.Dead).ExtendedData["horde"] = "1";

        var path = Path.Combine(_directory, "legacy.sav");
        Write(game, ruleset, path);

        // Matched without assuming how the platform ends its lines. Written against
        // "\n", this rewrite silently did nothing on Windows -- the file kept the
        // current format, the test proved nothing, and the assertion below failed
        // on Windows CI only.
        var legacy = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            "\"ExtendedData\":\\s*\\{\\s*\"horde\":\\s*\"1\"\\s*\\}",
            "\"ExtendedData\": [{ \"Key\": \"horde\", \"Value\": \"1\" }]");
        File.WriteAllText(path, legacy);
        Assert.DoesNotContain("\"ExtendedData\": {", File.ReadAllText(path));

        var reloaded = new JsonSavFile().LoadGame(File.ReadAllBytes(path), ruleset,
            RulesParser.ParseRules(ruleset));
        var reloadedUnit = reloaded.AllCivilizations
            .First(civ => civ.Id == game.GetPlayerCiv.Id)
            .Units.First(u => !u.Dead && u.ExtendedData.Count > 0);

        Assert.Equal("1", reloadedUnit.ExtendedData["horde"]);
    }

    [Fact]
    public void TheMap_ComesBackUnchanged()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var map = game.Maps[0];
        var before = Enumerable.Range(0, map.XDim)
            .SelectMany(x => Enumerable.Range(0, map.YDim).Select(y => map.Tile[x, y]))
            .Select(tile => (tile.Type, tile.River, tile.Special))
            .ToList();

        var reloaded = SaveAndLoad(game, ruleset);
        var reloadedMap = reloaded.Maps[0];
        var after = Enumerable.Range(0, reloadedMap.XDim)
            .SelectMany(x => Enumerable.Range(0, reloadedMap.YDim).Select(y => reloadedMap.Tile[x, y]))
            .Select(tile => (tile.Type, tile.River, tile.Special))
            .ToList();

        Assert.Equal(before, after);
    }

    [Fact]
    public void SavingTwiceOverTheSameFile_LeavesAGameThatLoads()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var path = Path.Combine(_directory, "repeat.sav");
        Write(game, ruleset, path);
        game.GetPlayerCiv.Money = 999;
        Write(game, ruleset, path);

        var reloaded = new JsonSavFile().LoadGame(File.ReadAllBytes(path), ruleset,
            RulesParser.ParseRules(ruleset));

        Assert.Equal(999, reloaded.AllCivilizations.First(c => c.Id == game.GetPlayerCiv.Id).Money);
    }

    [Fact]
    public void ABarbarianCity_DoesNotStopTheSave()
    {
        // The saved per-tribe counter is indexed by tribe and the barbarians carry
        // TribeId -1, which used to throw on every save from the moment they took
        // a city. Covered by its own test at the data layer; this checks the whole
        // path, because that is where it was actually noticed.
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var barbarians = game.AllCivilizations.FirstOrDefault(c => c.PlayerType == PlayerType.Barbarians);
        Assert.NotNull(barbarians);

        var settler = game.GetPlayerCiv.Units.First(unit => !unit.Dead);
        var city = CityActions.BuildCity(settler, game, "Taken");
        game.GetPlayerCiv.Cities.Remove(city);
        city.Owner = barbarians;
        barbarians.Cities.Add(city);

        var reloaded = SaveAndLoad(game, ruleset);

        Assert.Equal(game.TurnNumber, reloaded.TurnNumber);
    }

    /// <summary>
    /// Writes the game out and reads it back, the way the Load Game command does.
    /// <para>
    /// The rules are parsed afresh rather than reused. That is what the real load
    /// path does, and it has to: the ruleset's Lua adds effects to the shared
    /// improvement objects, so running it twice over one set of rules throws on the
    /// duplicate key.
    /// </para>
    /// </summary>
    private IGame SaveAndLoad(Game game, Ruleset ruleset)
    {
        var path = Path.Combine(_directory, "roundtrip.sav");
        Write(game, ruleset, path);
        return new JsonSavFile().LoadGame(File.ReadAllBytes(path), ruleset,
            RulesParser.ParseRules(ruleset));
    }

    private static void Write(Game game, Ruleset ruleset, string path)
    {
        var serializer = new GameSerializer();
        AtomicFile.Write(path,
            stream => serializer.Write(stream, game, ruleset, new Dictionary<string, string>()));
    }
}
