using RhyCiv.UI.Classic.ImageLoader;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// The painted river scenes are classified into the renderer's sixteen connection
/// masks, then flipped or turned to fill any mask the set did not paint. These
/// are the transforms that make that filling logical rather than arbitrary: a
/// horizontal flip must swap the same neighbour pair the map does, and every
/// mask must be reachable from the common shapes.
/// </summary>
public class RiverMaskTests
{
    [Theory]
    [InlineData(0b0001, 0b1000)] // ne <-> nw
    [InlineData(0b0010, 0b0100)] // se <-> sw
    [InlineData(0b0101, 0b1010)] // ne+sw <-> nw+se
    [InlineData(0b1111, 0b1111)]
    [InlineData(0b0000, 0b0000)]
    public void HorizontalFlipSwapsEastAndWestNeighbours(int mask, int expected)
    {
        Assert.Equal(expected, RiverMask.FlipHorizontal(mask));
    }

    [Theory]
    [InlineData(0b0001, 0b0010)] // ne <-> se
    [InlineData(0b1000, 0b0100)] // nw <-> sw
    [InlineData(0b1001, 0b0110)] // north corner <-> south corner
    [InlineData(0b1111, 0b1111)]
    public void VerticalFlipSwapsNorthAndSouthNeighbours(int mask, int expected)
    {
        Assert.Equal(expected, RiverMask.FlipVertical(mask));
    }

    [Theory]
    [InlineData(0b0001, 0b0010)] // ne -> se
    [InlineData(0b0010, 0b0100)] // se -> sw
    [InlineData(0b0100, 0b1000)] // sw -> nw
    [InlineData(0b1000, 0b0001)] // nw -> ne
    public void QuarterTurnWalksEdgesClockwise(int mask, int expected)
    {
        Assert.Equal(expected, RiverMask.RotateQuarterTurn(mask));
    }

    [Fact]
    public void FourQuarterTurnsReturnToStart()
    {
        for (var mask = 0; mask < RiverMask.Count; mask++)
        {
            var turned = mask;
            for (var i = 0; i < 4; i++)
            {
                turned = RiverMask.RotateQuarterTurn(turned);
            }

            Assert.Equal(mask, turned);
        }
    }

    [Fact]
    public void FlipsAndTurnsReachEveryMaskFromTheCommonShapes()
    {
        // What the painted set actually contains: a single edge, both pairs of
        // opposite edges, a through-diagonal, a three-way delta, and the cross.
        // Mask 0 has no water to paint and keeps the spoke art.
        var closure = RiverMask.Closure([0b0100, 0b1001, 0b0101, 0b1010, 0b1110, 0b1111]);

        for (var mask = 1; mask < RiverMask.Count; mask++)
        {
            Assert.True(closure.Contains(mask),
                $"mask {mask:B4} is unreachable, so that connection can only fall back to the spoke art");
        }
    }

    [Fact]
    public void PlanToSelfIsEmpty()
    {
        var steps = RiverMask.Plan(0b1010, 0b1010);
        Assert.NotNull(steps);
        Assert.Empty(steps);
    }

    [Fact]
    public void PlanWithinAnOrbitReachesEveryMask()
    {
        // Flips and turns preserve how many edges are crossed, so a through-
        // scene only stands in for its own orbit — but within that orbit every
        // member must be reachable, or a missing shape leaves a hole.
        foreach (var seed in new[] { 0b0100, 0b1001, 0b1010, 0b1110, 0b1111 })
        {
            foreach (var wanted in RiverMask.Closure([seed]))
            {
                var steps = RiverMask.Plan(seed, wanted);
                Assert.True(steps != null, $"no plan from {seed:B4} to {wanted:B4}");
            }
        }
    }

    [Fact]
    public void PlannedStepsActuallyProduceTheWantedMask()
    {
        for (var seed = 0; seed < RiverMask.Count; seed++)
        {
            for (var wanted = 0; wanted < RiverMask.Count; wanted++)
            {
                var steps = RiverMask.Plan(seed, wanted);
                if (steps == null)
                {
                    continue;
                }

                var mask = seed;
                foreach (var step in steps)
                {
                    mask = step switch
                    {
                        RiverTransform.FlipHorizontal => RiverMask.FlipHorizontal(mask),
                        RiverTransform.FlipVertical => RiverMask.FlipVertical(mask),
                        RiverTransform.QuarterTurn => RiverMask.RotateQuarterTurn(mask),
                        _ => mask,
                    };
                }

                Assert.Equal(wanted, mask);
            }
        }
    }
}
