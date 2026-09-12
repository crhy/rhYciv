using System.Collections.Generic;
using Model.Core.Mapping;

namespace RhyCiv.Engine.MapObjects;

/// <summary>
/// How far each river tile is from the sea, counted along the river.
/// </summary>
/// <remarks>
/// A river should be a trickle where it rises and broadest where it meets the
/// ocean, and nothing in a Civ II-shaped save records that: a tile either has a
/// river or it does not. It can be recovered from the map, though, because the
/// shape of the watercourse is all the information needed -- walk outwards from
/// every river tile that touches the sea, through river tiles only, and the
/// number of steps taken to reach a tile is how far up the river it sits.
/// <para>
/// Computed once when a map is generated or loaded. It is a property of the
/// watercourse rather than of the square, so working it out per tile while
/// drawing would mean walking the whole river for every tile of it.
/// </para>
/// </remarks>
public static class RiverFlow
{
    /// <summary>
    /// A river that reaches no sea at all. Its tiles are still ordered relative to
    /// one another, from this far-inland value downwards, so an inland system
    /// still narrows towards its springs instead of being one flat gauge.
    /// </summary>
    private const int Landlocked = 6;

    public static void Compute(Map map)
    {
        var queue = new Queue<Tile>();

        foreach (Tile? entry in map.Tile)
        {
            if (entry is null)
            {
                continue;
            }

            entry.RiverFlow = -1;
            if (!entry.River)
            {
                continue;
            }

            // A river tile touching the ocean is a mouth, and mouths are where the
            // counting starts.
            foreach (var neighbour in map.DirectNeighbours(entry))
            {
                if (neighbour.Type != TerrainType.Ocean)
                {
                    continue;
                }

                entry.RiverFlow = 0;
                queue.Enqueue(entry);
                break;
            }
        }

        Spread(map, queue);

        // Anything left is a watercourse that never reaches the sea. Start it again
        // from its own far end so it still has a gradient of its own.
        foreach (Tile? entry in map.Tile)
        {
            if (entry is { River: true, RiverFlow: -1 })
            {
                entry.RiverFlow = Landlocked;
                queue.Enqueue(entry);
                Spread(map, queue);
            }
        }

    }

    private static void Spread(Map map, Queue<Tile> queue)
    {
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            foreach (var neighbour in map.DirectNeighbours(tile))
            {
                if (!neighbour.River || neighbour.RiverFlow >= 0)
                {
                    continue;
                }

                neighbour.RiverFlow = tile.RiverFlow + 1;
                queue.Enqueue(neighbour);
            }
        }
    }
}
