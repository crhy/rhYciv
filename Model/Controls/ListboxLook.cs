using Model.Interface;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Fonts;
using System.Numerics;

namespace Model.Controls;

public class ListboxLooks
{
    public Color BoxBackgroundColor { get; set; } = Color.Blank;
    public Color BoxLineColor { get; set; } = Color.Blank;

    public Font Font { get; set; } = Fonts.Tnr;

    /// <summary>
    /// Row text size. This was 12, set when the interface was laid out against a
    /// much smaller window, and it is what made the city names in the Go To dialog
    /// unreadable. Rows size themselves from their label's preferred height, so
    /// raising it grows the row rather than clipping the text. 18 was still being
    /// reported as hard to read at 1080p in 0.1.5.
    /// </summary>
    public int FontSize { get; set; } = 22;
    public Color TextColorFront { get; set; } = Color.Black;
    public Color TextColorShadow { get; set; } = Color.Blank;
    public Vector2 TextShadowOffset { get; set; } = Vector2.Zero;

    public Font SelectedTextFont { get; set; } = Fonts.Tnr;
    public Color SelectedTextBackgroundColor { get; set; } = Color.Gray;
    public Color SelectedTextColorFront { get; set; } = Color.White;
    public Color SelectedTextColorShadow { get; set; } = Color.Black;
    public Vector2 SelectedTextShadowOffset { get; set; } = new(1, 1);
}
