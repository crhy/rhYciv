using Model.Controls;
using RaylibUI.BasicTypes;
using System.Drawing;
using Model.Core.Cities;

namespace RaylibUI.RunGame.GameControls.CityControls;

public class UnitsPresentBox : Listbox
{
    private readonly CityWindow _cityWindow;
    private readonly CityInfoArea _infoArea;
    private float _oldScale = 0f;
    private int _unitsSignature = -1;

    public UnitsPresentBox(CityWindow cityWindow, CityInfoArea infoArea) : base(cityWindow)
    {
        _cityWindow = cityWindow;

        // Disbanding a unit changes this list without resizing the window, so the
        // box has to be told to lay out again. CityCitizensBox listens to the same
        // signal for the same reason.
        _cityWindow.ResourceProductionChanged += (_, _) => OnResize();
        _infoArea = infoArea;
        ItemSelected += OpenPopup;
    }


    /// <summary>
    /// Identity and order of the units this box is showing. The list is rebuilt
    /// when this changes, not only when the window is rescaled: disbanding a unit
    /// leaves the scale alone, so the box went on showing a unit that no longer
    /// existed until something else resized the window.
    /// </summary>
    private int UnitsSignature()
    {
        var signature = new HashCode();
        foreach (var unit in _cityWindow.City.UnitsInCity)
        {
            signature.Add(unit);
        }
        return signature.ToHashCode();
    }

    public override void OnResize()
    {
        var signature = UnitsSignature();
        if (_oldScale != _cityWindow.Scale || _unitsSignature != signature)
        {
            Definition = MakeListbox(_cityWindow);
            _oldScale = _cityWindow.Scale;
            _unitsSignature = signature;
        }

        var pos = _cityWindow.CityWindowProps.InfoPanel.UnitsPresent.Box;
        Location = new(pos.X * _cityWindow.Scale, pos.Y * _cityWindow.Scale);
        Width = (int)(pos.Width * _cityWindow.Scale);
        Height = (int)(pos.Height * _cityWindow.Scale);

        Visible = _infoArea.Mode == CityDisplayMode.Info;

        base.OnResize();
    }

    static ListboxDefinition MakeListbox(CityWindow cityWindow)
    {
        var units = cityWindow.City.UnitsInCity;
        var active = cityWindow.MainWindow.ActiveInterface;
        var properties = cityWindow.CityWindowProps.InfoPanel.UnitsPresent;

        List<ListboxGroup> groups = [];
        foreach (var unit in units)
        {
            var group = new ListboxGroup()
            {
                Elements = [
                    new ListboxGroupElement { Unit = unit, Game = cityWindow.CurrentGameScreen.Game,
                        ScaleIcon = UnitScaleFor(cityWindow, properties)},
                    new ListboxGroupElement { Text = ShortCityName(unit.HomeCity), Xoffset = 0, 
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom }],
                Height = (int)Math.Ceiling(properties.Box.Height / properties.Rows * cityWindow.Scale)
            };
            groups.Add(group);
        }

        return new ListboxDefinition()
        {
            Rows = properties.Rows,
            Columns = properties.Columns,
            HorizontalStacking = true,
            Selectable = false,
            Groups = groups,
            Looks = new()
            {
                Font = active.Look.CityWindowFont,
                FontSize = Math.Max(10, (int)Math.Round(active.Look.CityWindowFontSize * cityWindow.Scale * 0.72f)),
                TextColorFront = Raylib_CSharp.Colors.Color.Black,
                TextColorShadow = new Raylib_CSharp.Colors.Color(135, 135, 135, 255)
            }
        };
    }

    private void OpenPopup(object? sender, ListboxSelectionEventArgs args)
    {
        var city = _cityWindow.City;
        if (args.Index < 0 || args.Index >= city.UnitsInCity.Count)
        {
            return;
        }

        var unit = city.UnitsInCity[args.Index];

        if (_cityWindow.ViewOnly)
        {
            // Their garrison. Seeing it is the point of the report; ordering it is
            // not on offer.
            return;
        }

        CityUnitMenu.Show(_cityWindow, unit);
    }


    /// <summary>
    /// Returns 3 character city name
    /// </summary>
    /// <param name="city"></param>
    /// <returns></returns>
    /// <summary>
    /// The caption under a unit is its own home city, which is what Civ II shows and
    /// what makes the box useful. It used to be the city being looked at, so a unit
    /// merely passing through was labelled as if it belonged here - and a unit with
    /// no home at all still got a name.
    /// </summary>
    private static string ShortCityName(City? city)
    {
        if (city == null)
        {
            return string.Empty;
        }

        return city.Name.Length < 3 ? city.Name : city.Name[..3];
    }

    /// <summary>
    /// How large to draw a unit in one cell of this box. The old value was a fixed
    /// 0.82 that took no account of the city window's scale, so at the default 1.5
    /// the box grew and the units in it did not. This fills the cell it is given
    /// and grows with the window.
    /// </summary>
    private static float UnitScaleFor(CityWindow cityWindow, UnitBox properties)
    {
        var unit = cityWindow.MainWindow.ActiveInterface.UnitImages.UnitRectangle;
        if (unit.Width <= 0 || unit.Height <= 0 || properties.Rows <= 0 || properties.Columns <= 0)
        {
            return cityWindow.Scale;
        }

        // Fit the row, not the column. Civ II sizes these to the height of the row
        // and lets neighbours overlap a little; fitting the cell width as well
        // shrank every unit to about seven tenths of the size it should be.
        var cellHeight = properties.Box.Height / properties.Rows;
        return Math.Max(0.1f, cellHeight / unit.Height * cityWindow.Scale);
    }
}
