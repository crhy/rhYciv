using System.Linq;
using System.Reflection;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Tests.Units;

/// <summary>
/// A raider that is not allowed to touch its target still has to finish its turn.
/// <para>
/// The raiders' turn is a loop that runs while the unit has move points left, and
/// every round either attacks a neighbouring square or steps toward one. Both can
/// be refused without deciding anything, and a refusal leaves the unit exactly as
/// it was: a land raider cannot attack a ship at sea, a raider riding a transport
/// is turned down when it reaches for the shore, one that cannot engage air units
/// cannot attack an air unit, and a step can be blocked by an enemy zone of control.
/// The square it was refused stays the nearest target, so the loop put the same
/// question to the same square forever and the raider's turn never came back.
/// </para>
/// <para>
/// That is the hang which stopped a test run from ever printing a result. Which
/// test was in the way changed from run to run, because the raiders wander, and
/// the desktop environment was never involved.
/// </para>
/// </summary>
public class BarbarianRaidTurnTests
{
    [Fact]
    public void ARaiderFacingAShipItCannotAttack_EndsItsTurn()
    {
        var (game, raider, ship) = ARaiderFacingAShipAtSea();

        game.ProcessBarbarianTurn(new MockPlayer(BarbariansOf(game)));

        // Nothing was attacked and nothing moved, so the round is over for good:
        // the raider's turn is ended rather than asked again forever.
        Assert.Equal(raider.MaxMovePoints, raider.MovePointsLost);
        Assert.Equal(0, raider.MovePoints);
    }

    [Fact]
    public void TheRefusedTarget_IsLeftAlone()
    {
        var (game, raider, ship) = ARaiderFacingAShipAtSea();

        game.ProcessBarbarianTurn(new MockPlayer(BarbariansOf(game)));

        // The rule that refused the attack is Civ II's, not a bug to work around:
        // a land raider has no business with a ship at sea. It must survive the
        // turn untouched, along with the raider that was refused it.
        Assert.False(ship.Dead);
        Assert.False(raider.Dead);
        Assert.Contains(ship, ship.CurrentLocation.UnitsHere);
    }

    private static (Game Game, Unit Raider, Unit Ship) ARaiderFacingAShipAtSea()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var barbarians = BarbariansOf(game);
        var opponent = game.AllCivilizations.First(civ => civ.PlayerType == PlayerType.Ai);

        // Any coast will do: a dry square with a wet one beside it, neither of
        // them already carrying something.
        Tile? shore = null;
        Tile? water = null;
        foreach (var tile in game.Maps[0].Tile.Cast<Tile>())
        {
            if (tile.Type == TerrainType.Ocean || tile.CityHere != null || tile.UnitsHere.Count != 0)
            {
                continue;
            }

            var neighbour = tile.Neighbours()
                .FirstOrDefault(tile2 => tile2.Type == TerrainType.Ocean && tile2.UnitsHere.Count == 0);
            if (neighbour != null)
            {
                shore = tile;
                water = neighbour;
                break;
            }
        }

        if (shore == null || water == null)
        {
            throw new InvalidOperationException("the clean-room map has no coast to stage this on");
        }

        var raider = Place(barbarians,
            rules.UnitTypes.First(type => type.Attack > 0 && type.Domain == UnitGas.Ground), shore);
        var ship = Place(opponent, rules.UnitTypes.First(type => type.Domain == UnitGas.Sea), water);

        // The raiders are the civilisation whose turn is being played.
        Set(game, "_activeCiv", barbarians);
        Set(game, "_activeCivId", barbarians.Id);

        return (game, raider, ship);
    }

    private static Civilization BarbariansOf(Game game) =>
        game.AllCivilizations.First(civ => civ.PlayerType == PlayerType.Barbarians);

    private static Unit Place(Civilization owner, UnitDefinition definition, Tile tile)
    {
        var unit = new Unit
        {
            Id = owner.Units.Count != 0 ? owner.Units.Max(other => other.Id) + 1 : 0,
            Owner = owner,
            TypeDefinition = definition,
            X = tile.X,
            Y = tile.Y,
            MapIndex = tile.Z,
            CurrentLocation = tile,
            Order = (int)OrderType.NoOrders,
            NeedsSupport = false,
        };

        owner.Units.Add(unit);
        tile.UnitsHere.Add(unit);
        return unit;
    }

    private static void Set(Game game, string field, object value) =>
        typeof(Game)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(game, value);
}
