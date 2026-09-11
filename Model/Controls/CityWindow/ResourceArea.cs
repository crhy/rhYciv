using Raylib_CSharp.Colors;
using Raylib_CSharp.Transformations;

namespace Model.Controls;

public class ResourceArea
{
    protected ResourceArea(Rectangle bounds, bool labelBelow)
    {
        Bounds = bounds;
        LabelBelow = labelBelow;
    }

    public Rectangle Bounds { get; }
    public bool LabelBelow { get; }

    /// <summary>
    /// The band this row's icons are counted out along, lit from the top.
    /// </summary>
    /// <remarks>
    /// Civ II gives each line of the City Resources panel a colour of its own --
    /// green for food, amber for trade, a deeper orange for what the rates take,
    /// blue for shields -- so the block reads as four separate accounts at a
    /// glance rather than as four rows of small pictures on grey. This drew the
    /// icons straight onto the panel, and the panel is the same stone as
    /// everything else, so nothing separated one line from the next.
    ///
    /// Null leaves the row as it was.
    /// </remarks>
    public Color? BarTop { get; init; }

    /// <inheritdoc cref="BarTop"/>
    public Color? BarBottom { get; init; }
}