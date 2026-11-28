using RhyCiv.Engine;

namespace RaylibUI;

/// <summary>
/// The pure decisions behind how the interface fonts are baked, separated from
/// the raylib calls that bake them so they can be tested without a GL context.
/// </summary>
public static class FontPlan
{
    /// <summary>
    /// Whether the interface chrome (menus, dialogs, buttons, labels) is drawn with
    /// the serif face. Sans is the default and clearer at UI sizes; the serif is
    /// the "Classic" option rhY can switch back to.
    /// </summary>
    public static bool InterfaceUsesSerif(InterfaceFontStyle style) =>
        style == InterfaceFontStyle.Classic;

    /// <summary>
    /// The size to bake a font atlas at for text drawn at <paramref name="drawSize"/>.
    /// <para>
    /// The atlases used to be baked at a fixed 96 to 112 pixels and then scaled down
    /// to the fourteen-to-twenty point the chrome actually draws at. Scaling a glyph
    /// by a fifth sampled a twentieth of the pixels it covered: hairlines vanished
    /// and stems flickered. Baking at the size the text is drawn at -- never below
    /// the f232 legibility floor -- keeps strokes the width they were designed to be,
    /// and lets bilinear filtering rather than a heavy downscale do the work.
    /// </para>
    /// </summary>
    public static int AtlasSizeFor(int drawSize) => TextRendering.LegibleUiFontSize(drawSize);
}
