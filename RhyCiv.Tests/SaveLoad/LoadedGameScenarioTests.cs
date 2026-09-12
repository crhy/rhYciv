using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.SaveLoad;
using RhyCiv.Engine.SaveLoad.SavFile;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.GameRules;

namespace RhyCiv.Tests.SaveLoad;

/// <summary>
/// A game loaded from this game's own save has a scenario to consult.
/// <para>
/// It did not. <c>JsonSaveObjects.Scenario</c> was declared <c>null!</c> and
/// nothing ever assigned it, so every game loaded from a rhYciv save carried a
/// null scenario. Nothing reads it until a city changes hands, and then
/// <c>MovementFunctions.ExecuteUnitMove</c> asks whether the scenario forbids
/// taking technology from a conquest and throws NullReferenceException — taking
/// the game down at the moment a city is captured, by anyone, in any loaded game.
/// </para>
/// <para>
/// It hid for a long time because Civilization II's own reader *does* build a
/// scenario, so every position imported from a .SAV was safe; only this game's
/// own saves were affected, and only once somebody took a city. It was found in
/// a crash report from a played game at turn 165, where barbarians captured a
/// city on their turn.
/// </para>
/// </summary>
public class LoadedGameScenarioTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("rhyciv-scenario-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AGameLoadedFromOurOwnSave_HasAScenario()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        CityActions.BuildCity(game.GetPlayerCiv.Units.First(unit => !unit.Dead), game, "Saved");

        var reloaded = SaveAndLoad(game, ruleset);

        Assert.NotNull(reloaded.ScenarioData);
    }

    [Fact]
    public void TheScenarioOfAnOrdinaryGame_ForbidsNothing()
    {
        // A saved game is not a scenario, so every restriction is off. This is the
        // field the capture path actually reads.
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        CityActions.BuildCity(game.GetPlayerCiv.Units.First(unit => !unit.Dead), game, "Saved");

        var reloaded = SaveAndLoad(game, ruleset);

        Assert.False(reloaded.ScenarioData.ForbidTechFromConquests);
        Assert.False(reloaded.ScenarioData.TotalWar);
    }

    [Fact]
    public void ANewGame_AlsoHasAScenario()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();

        Assert.NotNull(game.ScenarioData);
    }

    private IGame SaveAndLoad(Game game, Ruleset ruleset)
    {
        var path = Path.Combine(_directory, "scenario.sav");
        var serializer = new GameSerializer();
        AtomicFile.Write(path,
            stream => serializer.Write(stream, game, ruleset, new Dictionary<string, string>()));
        return new JsonSavFile().LoadGame(File.ReadAllBytes(path), ruleset,
            RulesParser.ParseRules(ruleset));
    }
}
