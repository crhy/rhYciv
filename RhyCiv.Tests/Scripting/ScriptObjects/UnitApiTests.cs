using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using Model.Core;
using Model.Core.Units;
using RhyCiv.Engine.Scripting.ScriptObjects;
using RhyCiv.Tests.Mocks;
using Neo.IronLua;

namespace RhyCiv.Tests.Scripting.ScriptObjects;

public class UnitApiTests
{
    [Fact]
    public void UnitApi_BasicProperties()
    {
        var (game, _, civ) = ApiTestHarness.CreateGameAndAi();
        var tile = ApiTestHarness.FindEmptyTile(game);
        var unitType = game.Rules.UnitTypes.First();
        var unit = ApiTestHarness.CreateUnit(civ, unitType, tile);

        var api = new UnitApi(unit, game);

        Assert.Equal(unit.Id, api.id);
        Assert.Equal(unit.RemainingHitpoints, api.hitpoints);
        Assert.Equal(unit.MovePointsLost, api.moveSpent);
        Assert.Equal(unit.Veteran, api.veteran);
        Assert.Equal(tile.X, api.location.x);
        Assert.Equal(tile.Y, api.location.y);
        Assert.Equal(unitType.Name, api.type.name);
    }

    [Fact]
    public void UnitApi_Lua_PropertyAccess()
    {
        var (game, _, civ) = ApiTestHarness.CreateGameAndAi();
        var tile = ApiTestHarness.FindEmptyTile(game);
        var unitType = game.Rules.UnitTypes.First();
        var unit = ApiTestHarness.CreateUnit(civ, unitType, tile);
        var api = new UnitApi(unit, game);

        using var l = new Lua();
        var g = l.CreateEnvironment();
        g["unit"] = api;

        var result = g.DoChunk("return unit.id, unit.hitpoints, unit.type.name", "test.lua");

        Assert.Equal(unit.Id, (int)result[0]);
        Assert.Equal(unit.RemainingHitpoints, (int)result[1]);
        Assert.Equal(unitType.Name, (string)result[2]);
    }

    [Fact]
    public void UnitApi_ExtendedData()
    {
        var (game, _, civ) = ApiTestHarness.CreateGameAndAi();
        var tile = ApiTestHarness.FindEmptyTile(game);
        var unitType = game.Rules.UnitTypes.First();
        var unit = ApiTestHarness.CreateUnit(civ, unitType, tile);
        var api = new UnitApi(unit, game);

        api.SetNum("test_num", 42);
        Assert.Equal(42, api.GetNum("test_num"));

        api.SetString("test_str", "hello");
        Assert.Equal("hello", api.GetString("test_str"));

        using var l = new Lua();
        var g = l.CreateEnvironment();
        g["unit"] = api;

        g.DoChunk("unit:SetNum('lua_num', 123); unit:SetString('lua_str', 'world')", "test.lua");

        Assert.Equal(123, api.GetNum("lua_num"));
        Assert.Equal("world", api.GetString("lua_str"));
    }

    /// <summary>
    /// A unit that has run out of moves and is sitting on an order is exactly what
    /// a scenario script calls activate() for. Afterwards it is back in the game's
    /// hands: no order, no moves spent, and the interface told to select it in the
    /// same way the turn cycle does when it offers a unit.
    /// </summary>
    [Fact]
    public void UnitApi_Activate_ClearsOrdersAndSelectsHumanUnit()
    {
        var (game, _, civ) = ApiTestHarness.CreateGameAndAi();
        civ.PlayerType = PlayerType.Local;
        var tile = ApiTestHarness.FindEmptyTile(game);
        var unitType = game.Rules.UnitTypes.First(t => t.Move > 0);
        var unit = ApiTestHarness.CreateUnit(civ, unitType, tile);

        unit.Order = (int)OrderType.GoTo;
        unit.MovePointsLost = unit.MaxMovePoints;
        Assert.False(unit.AwaitingOrders);

        var player = new RecordingPlayer(civ);
        game.Players[civ.Id] = player;

        var activations = new List<ActivationEventArgs>();
        game.OnUnitEvent += (_, args) =>
        {
            if (args is ActivationEventArgs activation)
            {
                activations.Add(activation);
            }
        };

        var api = new UnitApi(unit, game);
        using var l = new Lua();
        var g = l.CreateEnvironment();
        g["unit"] = api;
        g.DoChunk("unit:activate()", "test.lua");

        Assert.Equal((int)OrderType.NoOrders, unit.Order);
        Assert.Equal(0, unit.MovePointsLost);
        Assert.True(unit.AwaitingOrders);

        // The engine's own activation event, raised once, for this unit.
        Assert.Single(activations);
        Assert.Same(unit, activations[0].Unit);
        Assert.Equal(UnitEventType.NewUnitActivated, activations[0].EventType);
        Assert.True(activations[0].UserInitiated);
        Assert.False(activations[0].Reactivation);

        // ...and the unit selected, with the move flag the turn cycle passes.
        Assert.Equal(1, player.SetUnitActiveCalls);
        Assert.Same(unit, player.LastUnit);
        Assert.True(player.LastMove);
        Assert.Same(unit, player.ActiveUnit);
    }

    /// <summary>
    /// A dead unit is not in the game any more, so waking it up must do nothing at
    /// all: no cleared orders, no event, nothing selected.
    /// </summary>
    [Fact]
    public void UnitApi_Activate_LeavesDeadUnitExactlyAsItWas()
    {
        var (game, _, civ) = ApiTestHarness.CreateGameAndAi();
        civ.PlayerType = PlayerType.Local;
        var tile = ApiTestHarness.FindEmptyTile(game);
        var unitType = game.Rules.UnitTypes.First(t => t.Move > 0);
        var unit = ApiTestHarness.CreateUnit(civ, unitType, tile);

        unit.Order = (int)OrderType.GoTo;
        unit.MovePointsLost = 3;
        unit.Dead = true;

        var player = new RecordingPlayer(civ);
        game.Players[civ.Id] = player;

        var activations = new List<ActivationEventArgs>();
        game.OnUnitEvent += (_, args) =>
        {
            if (args is ActivationEventArgs activation)
            {
                activations.Add(activation);
            }
        };

        var api = new UnitApi(unit, game);
        using var l = new Lua();
        var g = l.CreateEnvironment();
        g["unit"] = api;
        g.DoChunk("unit:activate()", "test.lua");

        Assert.Equal((int)OrderType.GoTo, unit.Order);
        Assert.Equal(3, unit.MovePointsLost);
        Assert.True(unit.Dead);
        Assert.Empty(activations);
        Assert.Equal(0, player.SetUnitActiveCalls);
        Assert.Null(player.ActiveUnit);
    }

    /// <summary>
    /// A computer player and the barbarians move their own units, so there is no
    /// interface to point at their units. Their orders are still cleared -- a script
    /// may well be setting a unit up for the player that comes later -- but nothing
    /// is raised and nothing is selected.
    /// </summary>
    [Fact]
    public void UnitApi_Activate_ClearsButNeverSelectsForComputerOrBarbarianOwner()
    {
        var (game, _, civ) = ApiTestHarness.CreateGameAndAi();
        civ.PlayerType = PlayerType.Ai;
        var horde = game.AllCivilizations.First(c => c.PlayerType == PlayerType.Barbarians);

        var tile = ApiTestHarness.FindEmptyTile(game);
        var unitType = game.Rules.UnitTypes.First(t => t.Move > 0);

        var civPlayer = new RecordingPlayer(civ);
        game.Players[civ.Id] = civPlayer;
        var hordePlayer = new RecordingPlayer(horde);
        game.Players[horde.Id] = hordePlayer;

        var activations = new List<ActivationEventArgs>();
        game.OnUnitEvent += (_, args) =>
        {
            if (args is ActivationEventArgs activation)
            {
                activations.Add(activation);
            }
        };

        foreach (var owner in new[] { civ, horde })
        {
            var unit = ApiTestHarness.CreateUnit(owner, unitType, tile);
            unit.Order = (int)OrderType.GoTo;
            unit.MovePointsLost = 0;

            var api = new UnitApi(unit, game);
            using var l = new Lua();
            var g = l.CreateEnvironment();
            g["unit"] = api;
            g.DoChunk("unit:activate()", "test.lua");

            Assert.Equal((int)OrderType.NoOrders, unit.Order);
            Assert.Equal(0, unit.MovePointsLost);
            Assert.Empty(activations);
            Assert.Equal(0, civPlayer.SetUnitActiveCalls);
            Assert.Equal(0, hordePlayer.SetUnitActiveCalls);
            Assert.Null(civPlayer.ActiveUnit);
            Assert.Null(hordePlayer.ActiveUnit);
        }
    }

    /// <summary>
    /// The point of activating a unit is that the turn cycle offers it again: it
    /// looks for units that are awaiting orders -- no order, not dead, movement left
    /// -- and hands that unit to the interface. Woken unit, and the turn cycle that
    /// comes round again, are checked together here.
    /// </summary>
    [Fact]
    public void UnitApi_Activate_LeavesTheUnitForTheTurnCycleToOffer()
    {
        var (game, _, civ) = ApiTestHarness.CreateGameAndAi();
        civ.PlayerType = PlayerType.Local;
        var tile = ApiTestHarness.FindEmptyTile(game);
        var unitType = game.Rules.UnitTypes.First(t => t.Move > 0);
        var unit = ApiTestHarness.CreateUnit(civ, unitType, tile);

        // Everything else the civilisation owns has spent its moves, so the woken
        // unit is the only one the turn cycle could offer.
        foreach (var other in civ.Units)
        {
            if (other == unit) continue;
            other.MovePointsLost = other.MaxMovePoints;
        }

        unit.Order = (int)OrderType.GoTo;
        unit.MovePointsLost = unit.MaxMovePoints;
        Assert.False(unit.AwaitingOrders);

        var player = new RecordingPlayer(civ);
        player.ActiveTile = tile;
        game.Players[civ.Id] = player;

        var api = new UnitApi(unit, game);
        using var l = new Lua();
        var g = l.CreateEnvironment();
        g["unit"] = api;
        g.DoChunk("unit:activate()", "test.lua");

        Assert.True(unit.AwaitingOrders);
        Assert.True(unit.MovePoints > 0);
        Assert.False(unit.TurnEnded);
        Assert.False(unit.Dead);

        // Not just the predicate: the turn cycle really does offer it now.
        game.ChooseNextUnit();

        Assert.Equal(2, player.SetUnitActiveCalls);
        Assert.Same(unit, player.LastUnit);
        Assert.True(player.LastMove);
    }

    /// <summary>
    /// Stands in for the interface so the test can see whether the unit was selected,
    /// and with the move flag the engine's activation path passes.
    /// </summary>
    private sealed class RecordingPlayer(Civilization civ) : MockPlayer(civ)
    {
        public int SetUnitActiveCalls { get; private set; }
        public Unit? LastUnit { get; private set; }
        public bool LastMove { get; private set; }

        public override void SetUnitActive(Unit? unit, bool move)
        {
            SetUnitActiveCalls++;
            LastUnit = unit;
            LastMove = move;
            ActiveUnit = unit;
        }
    }
}