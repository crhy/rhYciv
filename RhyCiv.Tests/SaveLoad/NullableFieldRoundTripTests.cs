using System.Reflection;
using System.Text.Json.Serialization;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.SaveLoad;
using RhyCiv.Engine.SaveLoad.SavFile;
using RhyCiv.Engine.SaveLoad.SerializationUtils;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.GameRules;

namespace RhyCiv.Tests.SaveLoad;

/// <summary>
/// The fields a save keeps as "a value, or nothing at all".
/// <para>
/// These were written as <c>{}</c>: the writer asked the framework for each
/// property's type code, and a nullable value type answers "object", so the branch
/// that writes an object wrote the boxed number as an empty one. Saving reported
/// success and the file was unreadable. Because a research goal is set the first
/// time the player answers the research prompt, this took out very nearly every
/// real save -- issue #123, "Game Load and Save don't work".
/// </para>
/// </summary>
public class NullableFieldRoundTripTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("rhyciv-nullable-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AResearchGoalAndARevolution_ComeBackAsTheyWentIn()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var civ = game.GetPlayerCiv;
        civ.ResearchGoal = 5;
        civ.AnarchyTurnsRemaining = 3;

        var reloaded = SaveAndLoad(game, ruleset);
        var reloadedCiv = reloaded.AllCivilizations.First(c => c.Id == civ.Id);

        Assert.Equal(5, reloadedCiv.ResearchGoal);
        Assert.Equal(3, reloadedCiv.AnarchyTurnsRemaining);
    }

    [Fact]
    public void ACityWithStolenTechnology_ComesBackAsItWentIn()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var settler = game.GetPlayerCiv.Units.First(unit => !unit.Dead);
        var city = CityActions.BuildCity(settler, game, "Plundered");
        city.TechnologyStolen = true;

        var reloaded = SaveAndLoad(game, ruleset);

        Assert.True(reloaded.AllCities.Single(c => c.Name == "Plundered").TechnologyStolen);
    }

    [Fact]
    public void AGoalOfTheFirstAdvance_IsNotLostAsADefault()
    {
        // Zero is a real advance, and for a nullable field it has to stay apart
        // from "no goal at all" -- which is why these are written whenever they are
        // set rather than being dropped for matching the underlying default.
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        game.GetPlayerCiv.ResearchGoal = 0;

        var reloaded = SaveAndLoad(game, ruleset);

        Assert.Equal(0, reloaded.AllCivilizations.First(c => c.Id == game.GetPlayerCiv.Id).ResearchGoal);
    }

    [Fact]
    public void ASaveHoldingTheDamagedEmptyObject_StillLoads()
    {
        // The games players already have on disk. Written by a build that emitted
        // "{}" for these fields, they have to open rather than being lost.
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        game.GetPlayerCiv.ResearchGoal = 5;

        var path = Path.Combine(_directory, "damaged.sav");
        Write(game, ruleset, path);

        var damaged = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path), "\"ResearchGoal\":\\s*5", "\"ResearchGoal\": {}");
        File.WriteAllText(path, damaged);
        Assert.Contains("\"ResearchGoal\": {}", File.ReadAllText(path));

        var reloaded = new JsonSavFile().LoadGame(File.ReadAllBytes(path), ruleset,
            RulesParser.ParseRules(ruleset));

        // Absent, which is what the save would have said had the field been dropped
        // rather than mangled -- not a crash, and not advance zero.
        Assert.Equal(-1, reloaded.AllCivilizations.First(c => c.Id == game.GetPlayerCiv.Id).ResearchGoal);
    }

    /// <summary>
    /// Keeps the next one of these from shipping. A nullable value type on a save
    /// object needs the forgiving converter, or a save written by a build that got
    /// this wrong cannot be read; the writer no longer produces that form, but the
    /// files already on disk do.
    /// </summary>
    [Fact]
    public void EveryNullableValueOnASaveObject_CanStillReadTheDamagedForm()
    {
        var saveObjects = typeof(JsonCivData).Assembly.GetTypes()
            .Where(type => type.Namespace != null &&
                           type.Namespace.StartsWith("RhyCiv.Engine.SaveLoad") &&
                           type.Name.StartsWith("Json"))
            .ToList();
        Assert.NotEmpty(saveObjects);

        var unguarded = saveObjects
            .SelectMany(type => type.GetProperties())
            .Where(property => Nullable.GetUnderlyingType(property.PropertyType) != null)
            .Where(property => property.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType is not
            { IsGenericType: true } converter ||
                               converter.GetGenericTypeDefinition() != typeof(ForgivingNullableConverter<>))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToList();

        Assert.True(unguarded.Count == 0,
            "These nullable save fields have no ForgivingNullableConverter, so a save written by a build " +
            "that wrote them as '{}' will not load: " + string.Join(", ", unguarded));
    }

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
