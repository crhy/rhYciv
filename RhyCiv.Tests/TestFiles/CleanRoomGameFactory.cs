using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.NewGame;
using RhyCiv.Engine.SaveLoad;
using RhyCiv.Engine.SaveLoad.SavFile;
using Model.Core;
using Model.Core.Advances;
using Model.Core.GameRules;
using System.Reflection;
using System.Text.Json;

namespace RhyCiv.Tests.TestFiles;

/// <summary>
/// Builds the shared test game exclusively from rhYciv's standalone data and
/// deterministic engine APIs. No commercial rules, labels, or save file is
/// needed in the source tree or test output.
/// </summary>
internal static class CleanRoomGameFactory
{
    private sealed record Template(byte[] Save, Ruleset Ruleset);

    private static readonly Lazy<Template> GameTemplate = new(BuildTemplate);

    internal static string RepositoryRoot { get; } = FindRepositoryRoot();
    internal static string StandaloneDirectory { get; } =
        Path.Combine(RepositoryRoot, "RaylibUI", "FOSSart", "Standalone");
    internal static string ScriptsDirectory { get; } = Path.Combine(RepositoryRoot, "Engine", "Scripts");

    internal static (Game Game, Ruleset Ruleset, Rules Rules) CreateGame()
    {
        var template = GameTemplate.Value;
        Labels.UpdateLabels(template.Ruleset);
        var rules = RulesParser.ParseRules(template.Ruleset);
        ValidateTemplate(template, rules);
        var game = (Game)new JsonSavFile().LoadGame(template.Save, template.Ruleset, rules);
        return (game, template.Ruleset, rules);
    }

    /// <summary>
    /// The same game with the Barbarity question answered a particular way.
    /// <para>
    /// The setting is fixed when a game is created and readable but not writable
    /// afterwards, which is right -- it is one of the answers the player gave at
    /// the start. So it is set where the player's answer is set: in the saved game
    /// the template is loaded from.
    /// </para>
    /// </summary>
    internal static (Game Game, Ruleset Ruleset, Rules Rules) CreateGame(BarbarianActivityType activity)
    {
        var template = GameTemplate.Value;
        Labels.UpdateLabels(template.Ruleset);
        var rules = RulesParser.ParseRules(template.Ruleset);
        ValidateTemplate(template, rules);

        var save = WithBarbarianActivity(template.Save, (int)activity);
        var game = (Game)new JsonSavFile().LoadGame(save, template.Ruleset, rules);
        if (game.BarbarianActivity != (int)activity)
        {
            throw new InvalidOperationException(
                $"asked for barbarian activity {activity} but the loaded game reports {game.BarbarianActivity}");
        }

        return (game, template.Ruleset, rules);
    }

    private static byte[] WithBarbarianActivity(byte[] save, int activity)
    {
        using var document = JsonDocument.Parse(save);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name != "game")
                {
                    property.WriteTo(writer);
                    continue;
                }

                writer.WriteStartObject("game");
                foreach (var gameProperty in property.Value.EnumerateObject())
                {
                    if (gameProperty.Name != "data")
                    {
                        gameProperty.WriteTo(writer);
                        continue;
                    }

                    writer.WriteStartObject("data");
                    foreach (var data in gameProperty.Value.EnumerateObject())
                    {
                        if (data.Name != "BarbarianActivity")
                        {
                            data.WriteTo(writer);
                        }
                    }

                    writer.WriteNumber("BarbarianActivity", activity);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void ValidateTemplate(Template template, Rules rules)
    {
        using var document = JsonDocument.Parse(template.Save);
        var maps = document.RootElement.GetProperty("game").GetProperty("maps");
        if (maps.GetArrayLength() > rules.Terrains.Count)
        {
            throw new InvalidDataException(
                $"Generated save has {maps.GetArrayLength()} maps but rules define {rules.Terrains.Count} terrain sets.");
        }
        var mapIndex = 0;
        foreach (var map in maps.EnumerateArray())
        {
            var terrainCount = rules.Terrains[mapIndex].Length;
            var invalid = map.GetProperty("Tiles").EnumerateArray()
                .Select(tile => tile.TryGetProperty("T", out var type) ? type.GetInt32() : 0)
                .FirstOrDefault(type => type < 0 || type >= terrainCount, -1);
            if (invalid != -1)
            {
                throw new InvalidDataException(
                    $"Generated map {mapIndex} contains terrain {invalid}; valid range is 0..{terrainCount - 1}.");
            }
            mapIndex++;
        }
    }

    internal static byte[] CreateJsonSave() => GameTemplate.Value.Save.ToArray();

    private static Template BuildTemplate()
    {
        var ruleset = new Ruleset(
            "rhYciv clean-room tests",
            new Dictionary<string, string> { ["Source"] = "Generated by CleanRoomGameFactory" },
            StandaloneDirectory,
            ScriptsDirectory);
        Labels.UpdateLabels(ruleset);
        var rules = RulesParser.ParseRules(ruleset);

        var human = CreateCivilization(rules, rules.Leaders[0], 1, PlayerType.Local, 3);
        var opponent = CreateCivilization(rules, rules.Leaders[1], 2, PlayerType.Ai, 3);
        var config = new GameInitializationConfig
        {
            Random = new FastRandom(20260831),
            Rules = rules,
            WorldSize = [24, 32],
            NumberOfCivs = 2,
            DifficultyLevel = 1,
            BarbarianActivity = 1,
            PlayerCiv = human,
            Civilizations = [Barbarians.Civilization, human, opponent],
            Bloodlust = true,
            DontRestartEliminatedPlayers = true
        };

        var maps = MapGenerator.GenerateMap(config).GetAwaiter().GetResult();
        var game = NewGameInitialisation.StartNewGame(config, maps, config.Civilizations, ruleset.Paths);
        SetActivePlayerForSerialization(game, human);

        using var stream = new MemoryStream();
        new GameSerializer().Write(stream, game, ruleset, new Dictionary<string, string> { ["Zoom"] = "0" });
        return new Template(stream.ToArray(), ruleset);
    }

    private static void SetActivePlayerForSerialization(Game game, Civilization human)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Game).GetField("_activeCiv", flags)!.SetValue(game, human);
        typeof(Game).GetField("_activeCivId", flags)!.SetValue(game, human.Id);
        game.Players[human.Id].SetUnitActive(human.Units.FirstOrDefault(), false);
    }

    private static Civilization CreateCivilization(
        Rules rules, LeaderDefaults leader, int id, PlayerType playerType, int civilizationCount)
    {
        var titles = rules.Governments.Select(government => government.TitleMale).ToArray();
        return new Civilization
        {
            TribeId = leader.TribeId,
            Id = id,
            Alive = true,
            CityStyle = leader.CityStyle,
            LeaderName = leader.NameMale,
            LeaderTitle = titles[0],
            LeaderGender = 0,
            TribeName = leader.Plural,
            Adjective = leader.Adjective,
            Government = 0,
            ScienceRate = 60,
            TaxRate = 40,
            Advances = new bool[rules.Advances.Length],
            Titles = titles,
            PlayerType = playerType,
            NormalColour = leader.Color,
            AllowedAdvanceGroups = leader.AdvanceGroups ?? [AdvanceGroupAccess.CanResearch],
            CasualtiesPerUnitType = new int[rules.UnitTypes.Length],
            Attitude = new int[civilizationCount],
            Reputation = new int[civilizationCount],
            Relations = new Relation?[civilizationCount]
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rhYciv.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the rhYciv repository root.");
    }
}
