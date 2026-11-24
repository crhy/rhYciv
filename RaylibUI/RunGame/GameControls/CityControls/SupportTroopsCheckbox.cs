using System.Numerics;
using Model;
using Model.Images;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Transformations;
using RaylibUtils;
using RhyCiv.Engine;

namespace RaylibUI.RunGame.GameControls.CityControls;

/// <summary>
/// The per-city "support troops" option (#158) shown in the classic city window.
/// <para>
/// Clicking it flips <see cref="City.SupportTroopsInCity"/> and immediately runs
/// <see cref="CityExtensions.SetUnitSupport"/> again, so the units homed to this
/// city drop onto support (or come back off it) the moment the box is ticked,
/// whatever the government's free-unit budget would otherwise have allowed.
/// </para>
/// </summary>
public class SupportTroopsCheckbox : BaseControl
{
    private const int TextGap = 6;
    private const string LabelText = "Support troops in this city";

    private readonly CityWindow _cityWindow;
    private readonly IImageSource _boxUnchecked;
    private readonly IImageSource _boxChecked;
    private readonly int _boxWidth;
    private readonly int _boxHeight;
    private readonly Rectangle _layoutBox;

    public SupportTroopsCheckbox(CityWindow cityWindow, IImageSource boxUnchecked, IImageSource boxChecked, Rectangle layoutBox)
        : base(cityWindow, eventTransparent: false)
    {
        _cityWindow = cityWindow;
        _boxUnchecked = boxUnchecked;
        _boxChecked = boxChecked;
        _layoutBox = layoutBox;
        var active = cityWindow.MainWindow.ActiveInterface;
        _boxWidth = Images.GetImageWidth(boxChecked, active);
        _boxHeight = Images.GetImageHeight(boxChecked, active);
        Click += (_, _) => Toggle();
    }

    private void Toggle()
    {
        _cityWindow.City.SupportTroopsInCity = !_cityWindow.City.SupportTroopsInCity;

        // Recompute which of the city's own units are on support so the units box
        // and the support count answer the click straight away.
        var government = _cityWindow.CurrentGameScreen.Game.Rules.Governments[_cityWindow.City.Owner.Government];
        _cityWindow.City.SetUnitSupport(government);
        _cityWindow.UpdateProduction();
    }

    public override void OnResize()
    {
        var scale = _cityWindow.Scale;
        Location = new Vector2(
            _cityWindow.LayoutPadding.Left + _layoutBox.X * scale,
            _cityWindow.LayoutPadding.Top + _layoutBox.Y * scale);
        Width = (int)(_layoutBox.Width * scale);
        Height = (int)(_layoutBox.Height * scale);
        base.OnResize();
    }

    public override void Draw(bool pulse)
    {
        if (!Visible)
        {
            return;
        }

        var active = _cityWindow.MainWindow.ActiveInterface;
        var scale = _cityWindow.Scale;

        var box = _cityWindow.City.SupportTroopsInCity ? _boxChecked : _boxUnchecked;
        var texture = TextureCache.GetImage(box);
        if (texture.Width > 0 && texture.Height > 0)
        {
            var drawWidth = (int)(_boxWidth * scale);
            var drawHeight = (int)(_boxHeight * scale);
            Graphics.DrawTexturePro(texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
                new Rectangle(Bounds.X, Bounds.Y + (Height - drawHeight) / 2f, drawWidth, drawHeight),
                Vector2.Zero, 0f, Color.White);

            var fontSize = Math.Max(9, (int)Math.Round(active.Look.CityWindowFontSize * scale * 0.78f));
            var textPosition = new Vector2(
                Bounds.X + drawWidth + TextGap * scale,
                Bounds.Y + (Height - fontSize) / 2f);
            TextRendering.Draw(active.Look.CityWindowFont, LabelText, textPosition, fontSize, 0f,
                new Color(46, 34, 16, 255));
        }

        base.Draw(pulse);
    }
}
