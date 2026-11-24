using Model;
using Model.Controls;
using RaylibUI.BasicTypes.Controls;
using System.Numerics;

namespace RaylibUI.RunGame.GameControls.CityControls;

public class CityLabel : LabelControl
{
    private readonly CityWindow _cityWindow;
    private readonly IUserInterface _active;
    private readonly CityLabelProperties _properties;

    public CityLabel(CityWindow controller, CityLabelProperties properties) : 
        base(controller, properties.Text, true, font: controller.MainWindow.ActiveInterface.Look.CityWindowFont, colorFront: properties.Color,
        colorShadow: properties.ColorShadow, horizontalAlignment: properties.Alignment, shadowOffset: properties.ShadowOffset)
    {
        _cityWindow = controller;
        _active = _cityWindow.MainWindow.ActiveInterface;
        _properties = properties;
    }

    public override void OnResize()
    {
        var scale = _cityWindow.Scale;
        FontSize = Math.Max(TextRendering.MinimumFittedFontSize,
            (int)Math.Round(_active.Look.CityWindowFontSize * scale * 0.78f));
        var verticalInset = Math.Max(1, (int)Math.Round(2 * scale));

        // The layout declares every rectangle in the window's own coordinates, so
        // a heading belongs at the window's content origin plus that rectangle no
        // matter which control it happens to be nested in. Location is relative to
        // the parent's bounds, so the parent's offset is subtracted. The old branch
        // on Parent == _cityWindow meant the same key meant two different things
        // depending on where the label was added, which is how "Resource Map" ended
        // up measured from the tile map and "Units Present" from the info panel.
        var absolute = new Vector2(
            _cityWindow.Location.X + _cityWindow.LayoutPadding.Left + _properties.Box.X * scale,
            _cityWindow.Location.Y + _cityWindow.LayoutPadding.Top + _properties.Box.Y * scale + verticalInset);
        Location = new(absolute.X - Parent.Bounds.X, absolute.Y - Parent.Bounds.Y);
        Width = (int)(_properties.Box.Width * scale);
        Height = Math.Max(FontSize + verticalInset, (int)(_properties.Box.Height * scale) - verticalInset);
    }
}
