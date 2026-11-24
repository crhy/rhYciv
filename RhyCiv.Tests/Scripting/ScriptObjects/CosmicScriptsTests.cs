using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using Model.Core.Cities;
using Neo.IronLua;

namespace RhyCiv.Tests.Scripting.ScriptObjects;

/// <summary>
/// The aqueduct and sewer growth caps are tied to the improvements configured
/// through the cosmic script API rather than to the hardcoded Aqueduct / Sewer
/// System improvement ids. The defaults keep today's behaviour byte-for-byte;
/// a scenario that remaps the improvement must move the cap with it.
/// </summary>
public class CosmicScriptsTests
{
    [Fact]
    public void DefaultsPointAtTheHardcodedAqueductAndSewerImprovements()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var cosmic = new CosmicScripts(game);

        Assert.Equal((int)ImprovementType.Aqueduct, cosmic.aqueductImprovement);
        Assert.Equal((int)ImprovementType.SewerSystem, cosmic.sewerImprovement);
    }

    [Fact]
    public void ByDefaultACityStallsAtTheAqueductCapUntilTheAqueductIsBuilt()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var cosmic = new CosmicScripts(game);
        cosmic.sizeAquaduct = 8;
        cosmic.sizeSewer = 12;

        var city = new City { Size = 8 };
        Assert.False(city.CanGrow(game.Rules));

        city.OrderedImprovements.Add((int)ImprovementType.Aqueduct, Improvement((int)ImprovementType.Aqueduct));
        Assert.True(city.CanGrow(game.Rules));
    }

    [Fact]
    public void ByDefaultACityStallsAtTheSewerCapUntilTheSewerIsBuilt()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var cosmic = new CosmicScripts(game);
        cosmic.sizeAquaduct = 8;
        cosmic.sizeSewer = 12;

        var city = new City { Size = 12 };
        city.OrderedImprovements.Add((int)ImprovementType.Aqueduct, Improvement((int)ImprovementType.Aqueduct));
        Assert.False(city.CanGrow(game.Rules));

        city.OrderedImprovements.Add((int)ImprovementType.SewerSystem, Improvement((int)ImprovementType.SewerSystem));
        Assert.True(city.CanGrow(game.Rules));
    }

    [Fact]
    public void CanGrowHonoursTheConfiguredAqueductImprovementNotTheHardcodedOne()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var cosmic = new CosmicScripts(game);
        cosmic.sizeAquaduct = 8;
        cosmic.sizeSewer = 12;
        cosmic.aqueductImprovement = 42;

        var city = new City { Size = 8 };
        // The hardcoded Aqueduct no longer lifts the cap once the improvement is remapped...
        city.OrderedImprovements.Add((int)ImprovementType.Aqueduct, Improvement((int)ImprovementType.Aqueduct));
        Assert.False(city.CanGrow(game.Rules));

        // ...but the configured improvement does.
        city.OrderedImprovements.Add(42, Improvement(42));
        Assert.True(city.CanGrow(game.Rules));
    }

    [Fact]
    public void CanGrowHonoursTheConfiguredSewerImprovementNotTheHardcodedOne()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var cosmic = new CosmicScripts(game);
        cosmic.sizeAquaduct = 8;
        cosmic.sizeSewer = 12;
        cosmic.aqueductImprovement = 42;
        cosmic.sewerImprovement = 43;

        var city = new City { Size = 12 };
        city.OrderedImprovements.Add(42, Improvement(42));
        // The hardcoded Sewer System is ignored now that the improvement is remapped...
        city.OrderedImprovements.Add((int)ImprovementType.SewerSystem, Improvement((int)ImprovementType.SewerSystem));
        Assert.False(city.CanGrow(game.Rules));

        // ...and the configured sewer lifts the cap.
        city.OrderedImprovements.Add(43, Improvement(43));
        Assert.True(city.CanGrow(game.Rules));
    }

    [Fact]
    public void ScenarioLuaCanSetTheConfiguredImprovements()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var cosmic = new CosmicScripts(game);

        using var l = new Lua();
        var env = l.CreateEnvironment();
        env["cosmic"] = cosmic;

        var result = env.DoChunk(
            "cosmic.aqueductImprovement = 42; cosmic.sewerImprovement = 43; return cosmic.aqueductImprovement, cosmic.sewerImprovement",
            "test.lua");

        Assert.Equal(42, (int)result[0]);
        Assert.Equal(43, (int)result[1]);
        Assert.Equal(42, game.Rules.Cosmic.AqueductImprovementType);
        Assert.Equal(43, game.Rules.Cosmic.SewerImprovementType);
    }

    private static Improvement Improvement(int type) =>
        new() { Type = type, Name = type.ToString() };
}
