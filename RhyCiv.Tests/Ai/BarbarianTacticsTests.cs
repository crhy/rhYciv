using System.Linq;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Scripting.ScriptObjects;
using RhyCiv.Engine.Scripting.UnitActions;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;
using Model.Constants;
using Neo.IronLua;
using Xunit;

namespace RhyCiv.Tests.Ai;

/// Barbarians that turn up on their own with a stronger neighbour in reach.
/// <para>
/// A horde is the barbarian's way of walking at an enemy, and it was the only
/// thing the barbarian script could do: a unit with no company nearby was put
/// into a horde aimed at the nearest enemy and marched into it. Civ II leaves
/// a lone barbarian that is outmatched to back away instead, which is what
/// <c>isolated_and_outmatched</c> and <c>retreat_move</c> in
/// <c>Engine/Scripts/barbarian.ai.lua</c> now do (issue #7).
/// </para>
/// <para>
/// The scenarios use the clean room map's own squares rather than reshaping it:
/// (23,5) sits on the northern continent with seven ways out, (27,5) is two
/// steps east of it, and (28,14) is a square with only the sea and one enemy
/// around it.
/// </para>
public class BarbarianTacticsTests
{
    [Fact]
    public void LoneBarbarian_RetreatsFromTheEnemy()
    {
        var (game, aiPlayer, barbarians, enemyCiv) = CreateGameAndBarbarians();
        var home = game.Maps[0].TileC2(23, 5);
        var enemyTile = game.Maps[0].TileC2(27, 5);

        var unit = CreateUnit(barbarians, game, home);
        CreateUnit(enemyCiv, game, enemyTile);

        var result = aiPlayer.Ai.Call(AiEvent.UnitOrdersNeeded, new LuaTable { { "Unit", new UnitApi(unit, game) } });

        var move = Assert.IsType<MoveAction>(UnwrapLuaResult(result));
        Assert.True(Utilities.DistanceTo(move.Tile, enemyTile) > Utilities.DistanceTo(home, enemyTile),
            $"the barbarian moved to ({move.Tile.X},{move.Tile.Y}), no farther from the enemy at ({enemyTile.X},{enemyTile.Y})");
    }

    [Fact]
    public void BarbarianWithCompany_KeepsTheHordeBehaviour()
    {
        var (game, aiPlayer, barbarians, enemyCiv) = CreateGameAndBarbarians();
        var home = game.Maps[0].TileC2(23, 5);
        var enemyTile = game.Maps[0].TileC2(27, 5);

        var unit = CreateUnit(barbarians, game, home);
        var companionType = game.Rules.UnitTypes.First(t => t.Domain == UnitGas.Ground && t.AIrole == AiRoleType.Attack);
        CreateUnit(barbarians, game, game.Maps[0].TileC2(25, 5), companionType);
        CreateUnit(enemyCiv, game, enemyTile);

        var result = UnwrapLuaResult(aiPlayer.Ai.Call(AiEvent.UnitOrdersNeeded, new LuaTable { { "Unit", new UnitApi(unit, game) } }));

        // Not on its own, so the horde path runs exactly as before: no retreat
        // move is handed back.
        Assert.IsNotType<MoveAction>(result);
    }

    [Fact]
    public void LoneBarbarian_WithNobodyAround_IsLeftAlone()
    {
        var (game, aiPlayer, barbarians, _) = CreateGameAndBarbarians();
        var home = game.Maps[0].TileC2(22, 22);

        var unit = CreateUnit(barbarians, game, home);

        var result = UnwrapLuaResult(aiPlayer.Ai.Call(AiEvent.UnitOrdersNeeded, new LuaTable { { "Unit", new UnitApi(unit, game) } }));

        // Nothing in reach to run from and nothing to join: unchanged, and no
        // crash on the way.
        Assert.Null(result);
    }

    [Fact]
    public void LoneBarbarian_BoxedInByTheEnemy_GivesUp()
    {
        var (game, aiPlayer, barbarians, enemyCiv) = CreateGameAndBarbarians();
        var home = game.Maps[0].TileC2(28, 14);
        var enemyTile = game.Maps[0].TileC2(29, 15);

        var unit = CreateUnit(barbarians, game, home);
        CreateUnit(enemyCiv, game, enemyTile);

        var result = UnwrapLuaResult(aiPlayer.Ai.Call(AiEvent.UnitOrdersNeeded, new LuaTable { { "Unit", new UnitApi(unit, game) } }));

        // Sea on every other side, so no move gets any farther away: the retreat
        // says so instead of walking into the enemy.
        Assert.Null(result);
    }

    [Fact]
    public void LoneBarbarian_RetreatsToTheSameSquareEveryTime()
    {
        var first = RetreatSquare();
        var second = RetreatSquare();

        Assert.Equal(first, second);
    }

    // The same scenario built twice. Nothing in the retreat draws a random
    // number, so the answer is the same square both times.
    private static (int x, int y) RetreatSquare()
    {
        var (game, aiPlayer, barbarians, enemyCiv) = CreateGameAndBarbarians();
        var home = game.Maps[0].TileC2(23, 5);
        var enemyTile = game.Maps[0].TileC2(27, 5);

        var unit = CreateUnit(barbarians, game, home);
        CreateUnit(enemyCiv, game, enemyTile);

        var result = UnwrapLuaResult(aiPlayer.Ai.Call(AiEvent.UnitOrdersNeeded,
            new LuaTable { { "Unit", new UnitApi(unit, game) } }));

        var move = Assert.IsType<MoveAction>(result);
        return (move.Tile.X, move.Tile.Y);
    }

    private static (Game game, AiPlayer aiPlayer, Civilization barbarians, Civilization enemy) CreateGameAndBarbarians()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();

        var barbarians = game.AllCivilizations.First(c => c.PlayerType == PlayerType.Barbarians);
        var enemy = game.AllCivilizations.First(c => c.PlayerType != PlayerType.Barbarians);
        var aiPlayer = (AiPlayer)game.Players[barbarians.Id];

        return (game, aiPlayer, barbarians, enemy);
    }

    private static Unit CreateUnit(Civilization civ, Game game, Tile tile, UnitDefinition? type = null)
    {
        type ??= game.Rules.UnitTypes.First(t => t.Domain == UnitGas.Ground && t.Prereq < 0 && t.Attack > 0);

        var unit = new Unit
        {
            Id = civ.Units.Count,
            Owner = civ,
            TypeDefinition = type,
            CurrentLocation = tile,
            X = tile.X,
            Y = tile.Y,
            MapIndex = tile.Z
        };

        civ.Units.Add(unit);
        return unit;
    }

    private static object? UnwrapLuaResult(object? result)
    {
        if (result is LuaResult { Count: > 0 } luaResult)
        {
            return luaResult[0];
        }

        return result;
    }
}
