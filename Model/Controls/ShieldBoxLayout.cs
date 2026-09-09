using System;

namespace Model.Controls;

/// <summary>
/// How the shields for an item are arranged in the city window's production box.
/// </summary>
public static class ShieldBoxLayout
{
    /// <summary>
    /// Chooses how many rows of shields to draw, and how many go in each row.
    /// <para>
    /// Every row holds the same number, so the rows are only ever a whole division
    /// of the cost. Filling each row to the width of the panel and letting the last
    /// row take the remainder left a thirty-shield item as two full rows and then a
    /// single shield on its own, which reads as a mistake rather than as a total.
    /// </para>
    /// <para>
    /// Among the divisions that fit, the one nearest square wins, and the block
    /// does not have to span the width of the box: a cheap item should be a small
    /// square rather than a thin line stretched across the panel.
    /// </para>
    /// <para>
    /// A cost with no useful divisor -- a prime, or one whose factors are all too
    /// wide for the box -- has to leave a short last row, so an uneven division is
    /// accepted rather than nothing being drawn.
    /// </para>
    /// </summary>
    /// <param name="cost">Shields the item costs.</param>
    /// <param name="maxRows">Rows there is room for.</param>
    /// <param name="maxPerRow">Shields that fit across the box.</param>
    /// <param name="shieldWidth">Drawn width of one shield.</param>
    /// <param name="shieldHeight">Drawn height of one shield.</param>
    public static (int Rows, int PerRow) Choose(int cost, int maxRows, int maxPerRow,
        float shieldWidth, float shieldHeight)
    {
        cost = Math.Max(1, cost);
        maxRows = Math.Max(1, maxRows);
        maxPerRow = Math.Max(1, maxPerRow);

        return Squarest(cost, maxRows, maxPerRow, shieldWidth, shieldHeight, evenOnly: true)
               ?? Squarest(cost, maxRows, maxPerRow, shieldWidth, shieldHeight, evenOnly: false)
               ?? (1, Math.Min(cost, maxPerRow));
    }

    /// <summary>
    /// How the shields for an item are laid out in the production box: how many
    /// rows, how many to a row, and how far apart to step. A step smaller than the
    /// shield itself means they overlap.
    /// </summary>
    public readonly record struct ShieldGrid(int Rows, int PerRow, float StepX, float StepY);

    /// <summary>
    /// Fits the whole cost of an item into the box.
    /// <para>
    /// The block has to stand for the total, because that is the only way the
    /// player can see how far along the item is: eleven shields in a row says
    /// nothing about whether the item costs twenty or two hundred. Cheap items fit
    /// at their natural size and are laid out as an even block. An expensive one --
    /// a wonder at two hundred shields -- cannot, and used to fall back to a single
    /// row of whatever happened to fit across the panel, which is what a Pyramid
    /// looked like at eleven shields wide. It is drawn tighter instead, the shields
    /// overlapping a little, until the whole cost is in the box.
    /// </para>
    /// </summary>
    /// <param name="cost">Shields the item costs.</param>
    /// <param name="boxWidth">Width available, in pixels.</param>
    /// <param name="boxHeight">Height available, in pixels.</param>
    /// <param name="shieldWidth">Drawn width of one shield.</param>
    /// <param name="shieldHeight">Drawn height of one shield.</param>
    public static ShieldGrid Fit(int cost, float boxWidth, float boxHeight,
        float shieldWidth, float shieldHeight)
    {
        cost = Math.Max(1, cost);
        shieldWidth = Math.Max(1f, shieldWidth);
        shieldHeight = Math.Max(1f, shieldHeight);
        boxWidth = Math.Max(shieldWidth, boxWidth);
        boxHeight = Math.Max(shieldHeight, boxHeight);

        var maxPerRow = Math.Max(1, (int)(boxWidth / shieldWidth));
        var maxRows = Math.Max(1, (int)(boxHeight / shieldHeight));

        if (cost <= maxPerRow * maxRows)
        {
            var (rows, perRow) = Choose(cost, maxRows, maxPerRow, shieldWidth, shieldHeight);
            return new ShieldGrid(rows, perRow, shieldWidth, shieldHeight);
        }

        // Squeeze both directions by the same amount, so the shields keep their
        // shape while they close up. The area the block needs grows with the square
        // of the spacing, which is where the square root comes from; a couple of
        // passes then correct the rounding down to whole shields.
        var squeeze = (float)Math.Sqrt(boxWidth * boxHeight / (cost * shieldWidth * shieldHeight));
        squeeze = Math.Clamp(squeeze, MinimumSqueeze, 1f);

        var stepX = shieldWidth * squeeze;
        var stepY = shieldHeight * squeeze;
        var columns = Math.Max(1, (int)(boxWidth / stepX));
        var rowCount = (int)Math.Ceiling(cost / (double)columns);

        for (var pass = 0; pass < 4 && rowCount * stepY > boxHeight && squeeze > MinimumSqueeze; pass++)
        {
            squeeze = Math.Max(MinimumSqueeze, squeeze * 0.85f);
            stepX = shieldWidth * squeeze;
            stepY = shieldHeight * squeeze;
            columns = Math.Max(1, (int)(boxWidth / stepX));
            rowCount = (int)Math.Ceiling(cost / (double)columns);
        }

        return new ShieldGrid(rowCount, columns, stepX, stepY);
    }

    /// <summary>
    /// How close together shields may be drawn, as a share of their own size. Past
    /// this they stop reading as separate shields and the block becomes a smear.
    /// </summary>
    private const float MinimumSqueeze = 0.45f;

    /// <summary>
    /// The arrangement closest to square among those that fit, or null if none do.
    /// </summary>
    private static (int Rows, int PerRow)? Squarest(int cost, int maxRows, int maxPerRow,
        float shieldWidth, float shieldHeight, bool evenOnly)
    {
        (int Rows, int PerRow)? best = null;
        var bestScore = double.MaxValue;

        for (var rows = 1; rows <= maxRows; rows++)
        {
            if (evenOnly && cost % rows != 0)
            {
                continue;
            }

            var perRow = (int)Math.Ceiling(cost / (double)rows);
            if (perRow > maxPerRow)
            {
                continue;
            }

            // How far from square the block is, measured in drawn pixels so that a
            // shield being wider than it is tall is accounted for.
            var width = perRow * shieldWidth;
            var height = rows * shieldHeight;
            double score = Math.Abs(width - height) / Math.Max(1f, Math.Max(width, height));

            if (score < bestScore)
            {
                bestScore = score;
                best = (rows, perRow);
            }
        }

        return best;
    }
}
