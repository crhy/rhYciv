using RaylibUI;

namespace RhyCiv.Tests.RaylibUI;

/// <summary>
/// Menu text at a size it can actually be read at.
/// <para>
/// "Menu Text is aliased and ugly." (#33). The glyph atlases are baked at 96 to
/// 112 pixels and prepared with mipmaps and trilinear filtering, but the compact
/// and alternate looks ask for fourteen-point menu text, and the old paths drew
/// whatever they were given: drawn far under the atlas size, filtered past half
/// its stroke, or scaled up from a small one. The font-size entry points now
/// clamp through <see cref="TextRendering.LegibleUiFontSize"/> and
/// <see cref="TextRendering.LegibleMapFontSize"/> to their Minimum constants.
/// These tests pin that clamp: at or above the minimum the size round-trips,
/// below it the size is raised, and the floors themselves are never lowered.
/// Only the pure sizing functions are tested; Measure, FitFontSize and the Draw
/// paths P/Invoke raylib natives and the test runner has no GL context.
/// </para>
/// </summary>
public class TextLegibilityTests
{
    [Theory]
    [InlineData(-12, TextRendering.MinimumUiFontSize)]
    [InlineData(0, TextRendering.MinimumUiFontSize)]
    [InlineData(13, TextRendering.MinimumUiFontSize)]
    [InlineData(15, TextRendering.MinimumUiFontSize)]
    [InlineData(16, 16)]
    [InlineData(17, 17)]
    [InlineData(23, 23)]
    [InlineData(112, 112)]
    public void UiTextIsClampedAndRoundTrips(int asked, int expected)
    {
        var size = TextRendering.LegibleUiFontSize(asked);

        Assert.Equal(expected, size);
        Assert.True(size >= TextRendering.MinimumUiFontSize);
    }

    [Theory]
    [InlineData(-12, TextRendering.MinimumMapFontSize)]
    [InlineData(0, TextRendering.MinimumMapFontSize)]
    [InlineData(19, TextRendering.MinimumMapFontSize)]
    [InlineData(20, 20)]
    [InlineData(21, 21)]
    [InlineData(35, 35)]
    [InlineData(112, 112)]
    public void MapTextIsClampedAndRoundTrips(int asked, int expected)
    {
        var size = TextRendering.LegibleMapFontSize(asked);

        Assert.Equal(expected, size);
        Assert.True(size >= TextRendering.MinimumMapFontSize);
    }

    [Fact]
    public void MinimumsAreNotLowered()
    {
        Assert.True(TextRendering.MinimumUiFontSize >= 16);
        Assert.True(TextRendering.MinimumMapFontSize >= 20);
        Assert.True(TextRendering.MinimumFittedFontSize >= 13);
    }
}
