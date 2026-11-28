using RaylibUI;
using RhyCiv.Engine;

namespace RhyCiv.Tests.Fonts;

/// <summary>
/// The interface is drawn with a sans by default, and its glyph atlases are baked
/// at the size the text is actually drawn at rather than a fixed oversample.
/// <para>
/// crhy/rhYciv#66: the menu and dialog text was hard to read. The old face was a
/// serif and the atlases were baked at 96 to 112 pixels then scaled down to the
/// fourteen-to-twenty point the chrome uses, which dropped most of each stroke.
/// These tests pin the two decisions behind the fix -- the typeface follows the
/// "Interface font" setting and defaults to sans, and the atlas size tracks the
/// draw size (clamped to the f232 legibility floor), never a blanket 96. Only the
/// pure decisions are tested; the raylib LoadEx/Draw paths need a GL context.
/// </para>
/// </summary>
public class FontAtlasTests
{
    [Fact]
    public void InterfaceFontDefaultsToTheClearerSans()
    {
        Assert.Equal(InterfaceFontStyle.Sans, Settings.InterfaceFont);
    }

    [Theory]
    [InlineData(InterfaceFontStyle.Sans, false)]
    [InlineData(InterfaceFontStyle.Classic, true)]
    public void TypefaceFollowsTheInterfaceFontSetting(InterfaceFontStyle style, bool expectsSerif)
    {
        Assert.Equal(expectsSerif, FontPlan.InterfaceUsesSerif(style));
    }

    [Theory]
    [InlineData(14, TextRendering.MinimumUiFontSize)]  // below the floor: raised to it
    [InlineData(16, 16)]
    [InlineData(18, 18)]
    [InlineData(20, 20)]
    [InlineData(22, 22)]
    [InlineData(34, 34)]
    [InlineData(96, 96)]
    public void AtlasIsBakedAtTheDrawSize(int drawSize, int expectedAtlasSize)
    {
        Assert.Equal(expectedAtlasSize, FontPlan.AtlasSizeFor(drawSize));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(22)]
    public void AtlasIsNeverTheOldBlindNinetySix(int drawSize)
    {
        var atlasSize = FontPlan.AtlasSizeFor(drawSize);

        // The atlases used to be baked at a fixed 96 whatever the draw size; the
        // whole point of the fix is that the bake size follows the draw size, so a
        // face drawn at UI size is never baked at 96 and scaled down to a smear.
        Assert.NotEqual(96, atlasSize);
        Assert.True(atlasSize <= drawSize + TextRendering.MinimumUiFontSize,
            $"a {drawSize}-point face should not be oversampled to {atlasSize} pixels");
    }

    [Fact]
    public void AtlasIsNeverBelowTheLegibilityFloor()
    {
        Assert.True(FontPlan.AtlasSizeFor(1) >= TextRendering.MinimumUiFontSize);
        Assert.True(FontPlan.AtlasSizeFor(0) >= TextRendering.MinimumUiFontSize);
        Assert.True(TextRendering.MinimumUiFontSize >= 16);
    }
}
