using RhyCiv.Engine.Scripting.ScriptObjects;
using Neo.IronLua;

namespace RhyCiv.Tests.Scripting.ScriptObjects;

public class UnitTypeTests
{
    [Fact]
    public void UnitType_Properties()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var def = game.Rules.UnitTypes.First();
        var api = new UnitType(def, game);

        Assert.Equal(def.Name, api.name);
        Assert.Equal(def.Attack, api.attack);
        Assert.Equal(def.Defense, api.defense);
        Assert.Equal(def.Cost, api.cost);
    }

    [Fact]
    public void AdvancedFlags_ShortBitmaskKeepsItsBitsAndClearsTheRest()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var def = game.Rules.UnitTypes.First();
        var api = new UnitType(def, game);
        def.IsEngineer = true;

        // Only bits 0 and 1 are set, so the decoded array is shorter than the
        // setter reads. It used to throw; padding must keep the bits that are set.
        api.advancedFlags = 0b11;

        Assert.True(def.Invisible);
        Assert.True(def.NonDispandable);
        Assert.False(def.UnbribaleBarb);
        Assert.False(def.IsEngineer);
    }

    [Fact]
    public void UnitType_Lua_Access()
    {
        var (game, _, _) = ApiTestHarness.CreateGameAndAi();
        var def = game.Rules.UnitTypes.First();
        def.Name = "Phalanx";
        var api = new UnitType(def, game);

        using var l = new Lua();
        var g = l.CreateEnvironment();
        g["ut"] = api;

        var result = g.DoChunk("return ut.name, ut.attack", "test.lua");
        Assert.Equal("Phalanx", (string)result[0]);
        Assert.Equal(def.Attack, (int)result[1]);
    }
}