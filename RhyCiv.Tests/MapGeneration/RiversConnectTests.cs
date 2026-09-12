using RhyCiv.Tests.TestFiles;
using Model.Core.Mapping;

using RhyCiv.Engine;
using Model.Core;

namespace RhyCiv.Tests.MapGeneration;

/// <summary>
/// Every river tile joins the river system it belongs to.
/// <para>
/// A river is drawn as one picture per tile, chosen by which of the tile's four
/// <em>edge-sharing</em> neighbours also carry a river. Two river tiles that
/// share only a corner cannot be joined by any picture — neither appears in the
/// other's mask, so both are drawn as a stub running to an edge with nothing on
/// the far side.
/// </para>
/// <para>
/// The generator walked rivers through the eight-neighbour set, which includes
/// the four corner-touching tiles, so roughly half of every river's steps landed
/// somewhere the renderer could not connect. Rivers came out as a scatter of
/// short disjointed squiggles: generated in one adjacency, drawn in another.
/// </para>
/// </summary>
public class RiversConnectTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(29)]
    public void NoRiverTileStandsAlone(int seed)
    {
        // The world generator itself, not a saved map: this is about how rivers are
        // laid down, so it has to lay some down.
        var (_, _, rules) = CleanRoomGameFactory.CreateGame();
        var config = new GameInitializationConfig
        {
            Rules = rules,
            Random = new FastRandom(seed),
            WorldSize = [60, 40],
            Climate = 1,
        };

        var world = ClassicWorldGenerator.Generate(config, 60, 40);

        var rivers = new List<(int X, int Y)>();
        for (var y = 0; y < 40; y++)
        for (var x = 0; x < 60; x++)
        {
            if (world.Rivers[x, y]) rivers.Add((x, y));
        }

        Assert.NotEmpty(rivers);

        var orphans = rivers.Where(cell => !JoinsSomething(world, cell, 60, 40)).ToList();

        Assert.True(orphans.Count == 0,
            $"{orphans.Count} of {rivers.Count} river tiles share no edge with another river " +
            "tile or the sea, so they can only be drawn as a stub running to nothing: " +
            string.Join(", ", orphans.Take(8).Select(c => $"({c.X},{c.Y})")));
    }

    /// <summary>
    /// Whether this river cell has anywhere for its water to go across a shared
    /// edge: another river cell, or the sea. These are the four offsets
    /// <c>DirectNeighbours</c> yields, which is what the renderer connects through.
    /// </summary>
    private static bool JoinsSomething(GeneratedWorld world, (int X, int Y) cell, int width, int height)
    {
        var odd = cell.Y & 1;
        int[][] offsets = [[odd, -1], [odd, 1], [-1 + odd, 1], [-1 + odd, -1]];
        foreach (var offset in offsets)
        {
            var nx = cell.X + offset[0];
            var ny = cell.Y + offset[1];
            if (ny < 0 || ny >= height) continue;
            nx = ((nx % width) + width) % width;
            if (world.Rivers[nx, ny] || world.Terrain[nx, ny] == TerrainType.Ocean)
            {
                return true;
            }
        }

        return false;
    }

}
