using RaylibUI;
using Xunit;

namespace RhyCiv.Tests.Controls;

/// <summary>
/// Pins the advisor-height math that lets a player size a dialog by hand. The dialog
/// itself is not directly instantiable in a headless test (its constructor needs a live
/// host and interface), so the height math is pinned through the public clamp it uses.
/// </summary>
public class DynamicSizingDialogHeightTests
{
    [Fact]
    public void ManualHeightWithinRangeIsThatHeight()
    {
        // With a manual height set, the computed content height equals that value.
        Assert.Equal(150, DynamicSizingDialog.ClampHeight(150, 40, 900));
    }

    [Fact]
    public void ManualHeightSmallerThanMinimumClampsToTheMinimumScrollHeight()
    {
        Assert.Equal(40, DynamicSizingDialog.ClampHeight(10, 40, 900));
    }

    [Fact]
    public void ManualHeightTallerThanTheScreenClampsToTheScreen()
    {
        Assert.Equal(900, DynamicSizingDialog.ClampHeight(9999, 40, 900));
    }

    [Fact]
    public void UnsetManualHeightMeansTheAutoFitSentinel()
    {
        // 0 means "fall back to the prior auto-fit value", carried through unchanged.
        Assert.Equal(0, DynamicSizingDialog.ClampHeight(0, 40, 900));
    }
}
