namespace RhyCiv.UI.Classic.ImageLoader;

/// <summary>
/// Bit maths for the river connection mask: which of the tile's four edge-sharing
/// neighbours also carry a river. Bit 0 is the first neighbour
/// <c>MapNavigationFunctions.DirectNeighbours</c> yields (ne), then se, sw, nw.
/// </summary>
/// <remarks>
/// The painted river set in rhYcivtextures was drawn as free scenes, not as a
/// labelled grid of sixteen. At load time each painting is classified by which
/// diamond edges its water touches; masks with no painting of their own are
/// filled by flipping or rotating a painting that does, because a horizontal
/// flip swaps ne with nw and se with sw, a vertical flip swaps ne with se, and a
/// quarter-turn in the square source walks a corner through the four turns.
/// </remarks>
public static class RiverMask
{
    /// <summary>The four edge-sharing neighbours, in DirectNeighbours order.</summary>
    public const int Count = 16;

    public static int FlipHorizontal(int mask) =>
        ((mask & 0b0001) << 3) | ((mask & 0b0010) << 1) | ((mask & 0b0100) >> 1) | ((mask & 0b1000) >> 3);

    public static int FlipVertical(int mask) =>
        ((mask & 0b0001) << 1) | ((mask & 0b0010) >> 1) | ((mask & 0b0100) << 1) | ((mask & 0b1000) >> 1);

    /// <summary>
    /// A quarter-turn of the square source art: the diamond's ne edge becomes se,
    /// se becomes sw, sw becomes nw, nw becomes ne.
    /// </summary>
    public static int RotateQuarterTurn(int mask) =>
        ((mask & 0b0001) << 1) | ((mask & 0b0010) << 1) | ((mask & 0b0100) << 1) | ((mask & 0b1000) >> 3);

    /// <summary>
    /// Every mask reachable from the given set by flips and quarter-turns, plus
    /// the set itself. A painting of mask M can be installed as any mask in this
    /// closure.
    /// </summary>
    public static HashSet<int> Closure(IEnumerable<int> seeds)
    {
        var seen = new HashSet<int>(seeds);
        var queue = new Queue<int>(seen);
        while (queue.Count > 0)
        {
            var mask = queue.Dequeue();
            foreach (var next in new[] { FlipHorizontal(mask), FlipVertical(mask), RotateQuarterTurn(mask) })
            {
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }

    /// <summary>
    /// How to turn a seed mask into a wanted mask: a sequence of transforms to
    /// apply to the painting. Empty when the wanted mask is the seed itself;
    /// null when it is not reachable.
    /// </summary>
    public static IReadOnlyList<RiverTransform>? Plan(int seed, int wanted)
    {
        if (seed == wanted)
        {
            return [];
        }

        var cameFrom = new Dictionary<int, (int From, RiverTransform Step)> { [seed] = (-1, RiverTransform.None) };
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == wanted)
            {
                break;
            }

            foreach (var (step, next) in new[]
                     {
                         (RiverTransform.FlipHorizontal, FlipHorizontal(current)),
                         (RiverTransform.FlipVertical, FlipVertical(current)),
                         (RiverTransform.QuarterTurn, RotateQuarterTurn(current)),
                     })
            {
                if (cameFrom.ContainsKey(next))
                {
                    continue;
                }

                cameFrom[next] = (current, step);
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(wanted))
        {
            return null;
        }

        var steps = new List<RiverTransform>();
        var at = wanted;
        while (at != seed)
        {
            var (from, step) = cameFrom[at];
            steps.Add(step);
            at = from;
        }

        steps.Reverse();
        return steps;
    }
}

public enum RiverTransform
{
    None,
    FlipHorizontal,
    FlipVertical,
    QuarterTurn,
}
