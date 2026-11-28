using Raylib_CSharp.Fonts;
using Raylib_CSharp.Textures;

namespace Model.Interface;

public static class Fonts
{
    /// <summary>
    /// Times new roman
    /// </summary>
    public static Font Tnr { get; set; } = Font.GetDefault();

    /// <summary>
    /// Bold times new roman font
    /// </summary>
    public static Font TnRbold { get; set; } = Font.GetDefault();

    /// <summary>
    /// Alternative font
    /// </summary>
    public static Font Arial { get; set; } = Font.GetDefault();

    public const int FontSize = 20;

    /// <summary>
    /// Prepares a loaded face for drawing.
    /// <para>
    /// The atlases are now baked at the size the text is drawn at (see FontPlan),
    /// so there is nothing to mipmap away: a glyph is at most mildly downsampled,
    /// and bilinear filtering takes that cleanly. Trilinear with mipmaps was the fix for the old oversized
    /// 96-to-112 atlases, where a fivefold downscale needed the smaller mip levels;
    /// at a baked-at-draw-size atlas it only blurs the strokes it no longer has to
    /// shrink.
    /// </para>
    /// </summary>
    private static Font Prepare(Font font)
    {
        font.Texture.SetFilter(TextureFilter.Bilinear);
        return font;
    }

    public static void SetTnr(Font font)
    {
        Tnr = Prepare(font);
    }

    public static void SetArial(Font font)
    {
        Arial = Prepare(font);
    }

    public static void SetBold(Font font)
    {
        TnRbold = Prepare(font);
    }
}
