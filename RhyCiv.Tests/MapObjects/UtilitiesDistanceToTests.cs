using Model.Core.Mapping;
using RhyCiv.Engine.MapObjects;
using Xunit;

namespace RhyCiv.Tests.MapObjects;

/// <summary>
/// rhYciv#127 follow-up (f227b): Utilities.DistanceTo used to return the
/// SQUARED Euclidean distance on flat maps while round maps returned the true
/// distance, so the corruption/waste DistanceFactor (/2 into Civ II squares)
/// read 100 for a city 10 squares from the capital and capped at 32/16.
/// Both map kinds must return the same unit for the same offset.
/// </summary>
public class UtilitiesDistanceToTests
{
    // Tiles are stored with doubled X (x = 2 * column + row % 2). The measured
    // Kells/Carmarthen offset -- four columns and six rows -- reads 8 by 6 on
    // those coordinates, and sits far from the wrap seam of a 40-column map.
    private const int CapitalX = 20;
    private const int CapitalY = 4;
    private const int MeasureX = 28;
    private const int MeasureY = 10;

    private static Tile TileAt(int x, int y, Map map)
        => new(x, y, new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>(), Defense = 100 }, 0, map, x, new bool[2]);

    private static Map MapOf(bool flat)
    {
        var map = new Map(flat, 0) { XDim = 40, YDim = 12 };
        return map;
    }

    [Fact]
    public void FlatAndRoundReturnTheSameDistanceAwayFromTheWrapSeam()
    {
        var flatMap = MapOf(true);
        var roundMap = MapOf(false);

        var flatDistance = Utilities.DistanceTo(TileAt(CapitalX, CapitalY, flatMap), TileAt(MeasureX, MeasureY, flatMap));
        var roundDistance = Utilities.DistanceTo(TileAt(CapitalX, CapitalY, roundMap), TileAt(MeasureX, MeasureY, roundMap));

        Assert.Equal(10d, roundDistance);
        Assert.Equal(roundDistance, flatDistance);
    }

    [Fact]
    public void FlatReturnsTheEuclideanDistanceNotTheSquare()
    {
        var flatMap = MapOf(true);
        var distance = Utilities.DistanceTo(TileAt(0, 0, flatMap), TileAt(6, 8, flatMap));
        Assert.Equal(10d, distance);
    }

    [Fact]
    public void SameTileIsZeroOnBothMapKinds()
    {
        var flatMap = MapOf(true);
        var roundMap = MapOf(false);
        Assert.Equal(0d, Utilities.DistanceTo(TileAt(20, 4, flatMap), TileAt(20, 4, flatMap)));
        Assert.Equal(0d, Utilities.DistanceTo(TileAt(20, 4, roundMap), TileAt(20, 4, roundMap)));
    }
}
