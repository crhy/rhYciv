using Model.Controls;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// The shields in the city window's production box are a count of what the item
/// costs, and a count reads best as a block.
/// <para>
/// It used to fill each row to the width of the panel and let the last row hold
/// whatever was left, so a thirty-shield item came out as two full rows and then a
/// single shield sitting on its own, and a ten-shield item as one thin line
/// stretched across the box. Reported twice.
/// </para>
/// </summary>
public class ShieldGridTests
{
    // A shield is a little wider than it is tall, which is what makes the squarest
    // block have fewer rows than columns.
    private const float ShieldWidth = 12f;
    private const float ShieldHeight = 10f;

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(60)]
    public void EveryRowHoldsTheSameNumber(int cost)
    {
        var (rows, perRow) = Grid(cost);

        Assert.Equal(0, cost % rows);
        Assert.Equal(cost, rows * perRow);
    }

    [Fact]
    public void TheBlockIsRoughlySquare()
    {
        // Forty shields in a box ten wide: a 4x10 line and a 40x1 column both fit,
        // and neither is what should be drawn.
        var (rows, perRow) = Grid(40);

        var width = perRow * ShieldWidth;
        var height = rows * ShieldHeight;
        var ratio = Math.Max(width, height) / Math.Min(width, height);

        Assert.True(ratio < 2.0,
            $"40 shields came out {rows}x{perRow}, which is {ratio:F1} times longer than it is deep");
    }

    [Fact]
    public void ACheapItem_StaysNarrow()
    {
        // Ten shields must not be stretched across a box that could hold thirty.
        var (_, perRow) = Grid(10, maxPerRow: 30);

        Assert.True(perRow <= 5, $"ten shields spread to {perRow} across");
    }

    [Fact]
    public void AnAwkwardCost_StillFits()
    {
        // A prime cost cannot divide evenly into more than one row, so it has to
        // fall back to a single row rather than leaving a stub.
        var (rows, perRow) = Grid(17, maxPerRow: 20);

        Assert.True(rows * perRow >= 17);
        Assert.True(perRow <= 20);
    }

    [Fact]
    public void ACostTallerThanTheBox_IsSpreadAcross()
    {
        // Ninety shields with room for only ten rows: it has to widen.
        var (rows, perRow) = Grid(90, maxRows: 10, maxPerRow: 30);

        Assert.True(rows <= 10);
        Assert.True(rows * perRow >= 90);
    }

    [Fact]
    public void NothingIsEverZero()
    {
        var (rows, perRow) = Grid(0);

        Assert.True(rows >= 1);
        Assert.True(perRow >= 1);
    }

    private static (int Rows, int PerRow) Grid(int cost, int maxRows = 10, int maxPerRow = 20) =>
        ShieldBoxLayout.Choose(cost, maxRows, maxPerRow, ShieldWidth, ShieldHeight);

    // The production box in the city window, at roughly the size it is drawn.
    private const float BoxWidth = 150f;
    private const float BoxHeight = 190f;

    [Fact]
    public void ACheapItemIsDrawnAtItsNaturalSize()
    {
        var grid = ShieldBoxLayout.Fit(20, BoxWidth, BoxHeight, ShieldWidth, ShieldHeight);

        Assert.Equal(ShieldWidth, grid.StepX);
        Assert.Equal(ShieldHeight, grid.StepY);
        Assert.Equal(20, grid.Rows * grid.PerRow);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(600)]
    public void AnExpensiveItemStillShowsItsWholeCost(int cost)
    {
        var grid = ShieldBoxLayout.Fit(cost, BoxWidth, BoxHeight, ShieldWidth, ShieldHeight);

        // Every shield the item costs has somewhere to go. Without this a wonder
        // came out as one row of however many fitted across the panel, which says
        // nothing about how far along it is.
        Assert.True(grid.Rows * grid.PerRow >= cost,
            $"{cost} shields need a block of at least that many, got {grid.Rows}x{grid.PerRow}");
    }

    [Fact]
    public void AnExpensiveItemClosesTheShieldsUpRatherThanSpillingOut()
    {
        // A box too small to hold the cost at the shields' natural size: 150 by 100
        // takes twelve across and ten down, and the item costs two hundred.
        const float shortBoxHeight = 100f;
        var grid = ShieldBoxLayout.Fit(200, BoxWidth, shortBoxHeight, ShieldWidth, ShieldHeight);

        Assert.True(grid.StepX < ShieldWidth, "the shields should overlap horizontally");
        Assert.True(grid.StepY < ShieldHeight, "the shields should overlap vertically");
        Assert.True(grid.PerRow * grid.StepX <= BoxWidth + ShieldWidth,
            "the block should stay inside the box");
        Assert.True(grid.Rows * grid.StepY <= shortBoxHeight + ShieldHeight,
            "the block should stay inside the box");
    }

    [Fact]
    public void ShieldsAreNeverSqueezedIntoAnUnreadableSmear()
    {
        // A ludicrous cost still has to be drawn as shields, not as a solid band.
        var grid = ShieldBoxLayout.Fit(5000, BoxWidth, BoxHeight, ShieldWidth, ShieldHeight);

        Assert.True(grid.StepX >= ShieldWidth * 0.45f - 0.01f);
        Assert.True(grid.StepY >= ShieldHeight * 0.45f - 0.01f);
    }
}
