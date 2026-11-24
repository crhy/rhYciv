using System.IO;
using System.Linq;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Tests.UnitActions;

/// <summary>
/// Units standing in one area are given their orders one after another.
/// <para>
/// Reported as: "you move one unit, then it jumps to some totally different part
/// of the map to move another unit, even though I had several unmoved units in
/// that same area." The selector looked at the active square and its eight
/// neighbours and, finding nothing there, took the first awaiting unit in list
/// order -- which is another part of the world entirely. With the rule on it
/// keeps expanding outward ring by ring, so the whole area is spent before
/// anything further away is considered.
/// </para>
/// <para>
/// The order Civ II asks about units in is not wrong, just not what the reporter
/// wants, so this sits behind an Advanced Settings checkbox that is off unless
/// the player ticks it. The old order has to survive the box being left alone,
/// which is what the last of these checks.
/// </para>
/// </summary>
public class NearbyUnitSelectionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "rhyciv-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Changing an advanced setting writes it out, so these sessions get a
    /// settings directory of their own and never touch the player's.
    /// </summary>
    public NearbyUnitSelectionTests()
    {
        Settings.DataFolder = () => _folder;
    }

    public void Dispose()
    {
        Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false,
            nearbyUnitsFirst: false);
        Settings.DataFolder = () => Path.Combine(Path.GetTempPath(), "rhyciv-tests-unset");
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void AUnitInTheSameArea_IsOfferedBeforeOneAcrossTheWorld()
    {
        var area = Area(nearbyUnitsFirst: true);

        area.Game.ChooseNextUnit();

        // The far unit stands first in the civilisation's list, which is what
        // used to decide. The near one is three rings out, which now does.
        Assert.Same(area.NearWorking, area.Player.LastActivated);
    }

    [Fact]
    public void TheAreaIsSpent_BeforeTheSelectorMovesOn()
    {
        var area = Area(nearbyUnitsFirst: true);

        var offered = new List<Unit?>();
        for (var round = 0; round < 3; round++)
        {
            area.Game.ChooseNextUnit();
            var thisRound = area.Player.LastActivated ??
                            throw new InvalidOperationException($"round {round} offered no unit");
            offered.Add(thisRound);

            // The player has finished with it: its movement is spent, so it is
            // no longer waiting for orders.
            thisRound.MovePointsLost = thisRound.MaxMovePoints;
        }

        // Both units of the area come before the one on the far side, and neither
        // is offered twice.
        Assert.Same(area.NearWorking, offered[0]);
        Assert.Same(area.NearNeighbour, offered[1]);
        Assert.Same(area.FarSide, offered[2]);
    }

    [Fact]
    public void WithTheBoxUnticked_TheOldOrderIsKept()
    {
        var area = Area(nearbyUnitsFirst: false);

        area.Game.ChooseNextUnit();

        // Nothing in the active square or its one ring, so the old selector falls
        // back to the unit list and answers with the unit on the far side.
        Assert.Same(area.FarSide, area.Player.LastActivated);
    }

    [Fact]
    public void TheRuleIsOffUntilThePlayerAsksForIt()
    {
        Assert.False(Settings.NearbyUnitsFirstEnabled);
    }

    [Fact]
    public void TheChoiceIsWrittenOutAndReadBack()
    {
        Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false,
            nearbyUnitsFirst: true);

        var settingsFile = Path.Combine(_folder, "appsettings.json");
        var written = File.ReadAllText(settingsFile);
        Assert.Contains("\"NearbyUnitsFirstEnabled\":true", written);

        // A session that has not read the file yet starts with the rule off.
        Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false,
            nearbyUnitsFirst: false);
        Assert.False(Settings.NearbyUnitsFirstEnabled);

        // Reading back what the player left behind turns it on again.
        File.WriteAllText(settingsFile, written);
        Reload(settingsFile);
        Assert.True(Settings.NearbyUnitsFirstEnabled);
    }

    /// <summary>
    /// A game with three of the player's units awaiting orders: two three rings
    /// out from the square the player is working on, one at the far edge of the
    /// map and deliberately first in the civilisation's list. Every other unit
    /// of the civilisation has its turn spent, so those three are all the
    /// selector has to choose among.
    /// </summary>
    private static TestArea Area(bool nearbyUnitsFirst)
    {
        Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false,
            nearbyUnitsFirst: nearbyUnitsFirst);

        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var civ = game.GetPlayerCiv;
        var player = new RecordingPlayer(civ);
        game.ConnectPlayer(player);

        // The starting army is spent, so the three units below are all the
        // selector has to choose among.
        foreach (var unit in civ.Units)
        {
            unit.MovePointsLost = unit.MaxMovePoints;
        }

        var map = game.Maps[0];
        var working = game.ActiveTile;
        var rings = RingsFrom(working);
        var open = OpenGround(map, working);

        var near = SquaresAtRing(open, rings, 3);
        if (near.Count < 2)
        {
            near = SquaresAtRing(open, rings, 2);
        }
        Assert.True(near.Count >= 2, "the map has no pair of open squares near the working square");

        Assert.True(open.Count > 0, "the map has no open ground to stand a unit on");
        var farSide = open.MaxBy(tile => rings[tile])!;

        // The near pair has to be outside the one ring the old selector already
        // covers, or the two settings would not differ in these tests.
        Assert.DoesNotContain(near[0], working.Neighbours());
        Assert.DoesNotContain(near[1], working.Neighbours());
        Assert.NotSame(near[0], near[1]);
        Assert.NotSame(near[0], farSide);

        var type = civ.Units.First(unit => unit.MaxMovePoints > 0).TypeDefinition;
        var far = AddUnit(civ, type);
        var nearWorking = AddUnit(civ, type);
        var nearNeighbour = AddUnit(civ, type);

        // First in the list, and last in the area: the answer the old selector
        // gives, and the one the new one must not.
        civ.Units.Remove(far);
        civ.Units.Insert(0, far);

        PlaceAwaiting(far, farSide);
        PlaceAwaiting(nearWorking, near[0]);
        PlaceAwaiting(nearNeighbour, near[1]);

        return new TestArea(game, civ, player, far, nearWorking, nearNeighbour);
    }

    /// <summary>
    /// A unit of this civilisation standing nowhere yet. Built from one of the
    /// rules' own unit definitions rather than a saved game, as CONTRIBUTING asks.
    /// </summary>
    private static Unit AddUnit(Civilization civ, UnitDefinition type)
    {
        var unit = new Unit { Owner = civ, TypeDefinition = type };
        civ.Units.Add(unit);
        return unit;
    }

    private static void PlaceAwaiting(Unit unit, Tile tile)
    {
        unit.Order = (int)OrderType.NoOrders;
        unit.MovePointsLost = 0;
        unit.CurrentLocation = tile;
    }

    /// <summary>
    /// The squares this civilisation could stand a unit on: land, passable,
    /// empty, and not the square it is working from.
    /// </summary>
    private static List<Tile> OpenGround(Map map, Tile from)
    {
        var open = new List<Tile>();
        for (var y = 0; y < map.YDim; y++)
        {
            for (var x = 0; x < map.XDim; x++)
            {
                var tile = map.Tile[x, y];
                if (tile == null || tile == from || tile.Type == TerrainType.Ocean ||
                    tile.Terrain.Impassable || tile.UnitsHere.Count > 0)
                {
                    continue;
                }

                open.Add(tile);
            }
        }

        return open;
    }

    /// <summary>
    /// Open ground a given number of rings out, in the order the selector would
    /// take it: by row, then by column.
    /// </summary>
    private static List<Tile> SquaresAtRing(List<Tile> open, Dictionary<Tile, int> rings, int ring)
    {
        return open.Where(tile => rings[tile] == ring)
            .OrderBy(tile => (tile.Y, tile.X))
            .ToList();
    }

    /// <summary>
    /// How far every square is from a square, counted the way the selector walks
    /// them: one step is one of the eight squares around it.
    /// </summary>
    private static Dictionary<Tile, int> RingsFrom(Tile from)
    {
        var rings = new Dictionary<Tile, int> { [from] = 0 };
        var ring = new List<Tile> { from };
        var ringNumber = 0;

        while (ring.Count > 0)
        {
            var next = new List<Tile>();
            foreach (var tile in ring)
            {
                foreach (var neighbour in tile.Neighbours())
                {
                    if (!rings.ContainsKey(neighbour))
                    {
                        rings[neighbour] = ringNumber + 1;
                        next.Add(neighbour);
                    }
                }
            }

            ring = next;
            ringNumber++;
        }

        return rings;
    }

    private record TestArea(Game Game, Civilization Civ, RecordingPlayer Player,
        Unit FarSide, Unit NearWorking, Unit NearNeighbour);

    /// <summary>Remembers which unit was handed to the player, and when.</summary>
    private sealed class RecordingPlayer(Civilization civ) : MockPlayer(civ)
    {
        public Unit? LastActivated { get; private set; }

        public override void SetUnitActive(Unit? unit, bool move)
        {
            if (unit != null)
            {
                LastActivated = unit;
            }

            base.SetUnitActive(unit, move);
        }
    }

    /// <summary>Reads a settings file the way a new session does.</summary>
    private static void Reload(string settingsFile) =>
        typeof(Settings)
            .GetMethod("LoadSettings", System.Reflection.BindingFlags.NonPublic |
                                       System.Reflection.BindingFlags.Static)!
            .Invoke(null, [settingsFile]);
}
