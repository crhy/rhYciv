using RhyCiv.Engine;
using RhyCiv.Engine.Production;
using Model;
using Model.Controls;
using Model.Images;
using Model.Interface;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Fonts;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Transformations;
using RaylibUI.BasicTypes.Controls;
using RaylibUI.Controls;
using RaylibUtils;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using RhyCiv.Engine.IO;
using Model.Core.Cities;

namespace RaylibUI.RunGame.GameControls.CityControls;

public class CityWindow : BaseDialog
{
    public GameScreen CurrentGameScreen { get; }
    public CityWindowLayout CityWindowProps => _cityWindowProps;
    private readonly CityWindowLayout _cityWindowProps;
    private readonly HeaderLabel _headerLabel;
    private readonly Button _shrinkIcon, _expandIcon, _exitIcon;
    private readonly IUserInterface _active;
    private readonly int _iconW, _iconH;
    private float _scale = 1.5f;  // scale city window size (1=normal, 1.5=large)
    private bool _backdropResolved;
    private Raylib_CSharp.Images.Image? _backdrop;
    private const float _scaleMax = 1.5f;
    private const float _scaleMin = 1.0f;
    private const float _scaleDelta = 0.5f;
    private readonly UnitSupportBox _unitSupportBox;
    private readonly CityLabel _supportLabel;
    private readonly CityLabel _citizensLabel;
    private readonly Color _citizensLabelColor;
    private readonly Color _citizensLabelShadow;

    /// <summary>
    /// Whether this window may only be looked at.
    /// <para>
    /// Set when a Diplomat's report opens somebody else's city. Everything is shown
    /// -- what it is building, what it has built, what is standing in it -- and
    /// nothing can be touched: the buttons that would spend its owner's gold or
    /// change its owner's mind are not there.
    /// </para>
    /// </summary>
    public bool ViewOnly { get; }

    public CityWindow(GameScreen gameScreen, City city, bool viewOnly = false) : base(gameScreen.Main)
    {
        CurrentGameScreen = gameScreen;
        City = city;
        ViewOnly = viewOnly;
        _active = gameScreen.MainWindow.ActiveInterface;
        var game = CurrentGameScreen.Game;

        _cityWindowProps = _active.GetCityWindowDefinition();

        _headerLabel = new HeaderLabel(this, _active.Look, $"{Labels.For(LabelIndex.City)} {Labels.For(LabelIndex.of)} {City.Name}, " +
            $"{game.Date.GameYearString(game.TurnNumber)}, {Labels.For(LabelIndex.Population)} {city.GetPopulation()} " +
            $"({Labels.For(LabelIndex.Treasury)} {city.Owner.Money} {Labels.For(LabelIndex.Gold)})",
            fontSize: _active.Look.CityHeaderLabelFontSizeNormal);

        Controls.Add(_headerLabel);

        //Tile map rendered first because in TOT it renders behind
        var tileMap = new CityTileMap(this, gameScreen.Game);
        Controls.Add(tileMap);

        var infoArea = new CityInfoArea(this);
        Controls.Add(infoArea);

        var infoButton = new CityButton(this, "Info");
        infoButton.Click += (_, _) => infoArea.SetActiveMode(CityDisplayMode.Info);
        Controls.Add(infoButton);

        var mapButton = new CityButton(this, "Map");
        mapButton.Click += (_, _) => infoArea.SetActiveMode(CityDisplayMode.SupportMap);
        Controls.Add(mapButton);

        if (!viewOnly)
        {
            var renameButton = new CityButton(this, "Rename");
            renameButton.Click += (_, _) => { };
            Controls.Add(renameButton);
        }

        var happyButton = new CityButton(this, "Happy");
        happyButton.Click += (_, _) => infoArea.SetActiveMode(CityDisplayMode.Happiness);
        Controls.Add(happyButton);

        var viewButton = new CityButton(this, "View");
        viewButton.Click += (_, _) => gameScreen.ShowDialog(new CityView(gameScreen, city)); ;
        Controls.Add(viewButton);

        var exitButton = new CityButton(this, "Exit");
        exitButton.Click += CloseButtonOnClick;
        Controls.Add(exitButton);

        var resourceTitle = Labels.For(LabelIndex.CityResources);
        //bounds = _cityWindowProps.Resources.TitlePosition;
        //Controls.Add(new CityLabel(this, Labels.For(LabelIndex.CityResources), new Color(223, 187, 63, 255), new Color(67, 67, 67, 255))
        //{
        //    Location = new(bounds.X, bounds.Y),
        //    Width = (int)bounds.Width,
        //    Height = (int)bounds.Height,
        //});
        foreach (var resource in _cityWindowProps.Resources.Resources)
        {
            Controls.Add(new ResourceProductionBar(this, resource));
        }

        Controls.Add(new CityLabel(this, _cityWindowProps.Labels["FoodStorage"]));
        Controls.Add(new FoodStorageBox(this));
        Controls.Add(new ProductionBox(this));
        _supportLabel = new CityLabel(this, _cityWindowProps.Labels["UnitsSupported"]);
        Controls.Add(_supportLabel);
        _unitSupportBox = new UnitSupportBox(this);
        Controls.Add(_unitSupportBox);
        Controls.Add(new CityLabel(this, _cityWindowProps.Labels["CityImprovements"]));
        Controls.Add(new ImprovementsBox(this));
        _citizensLabel = new CityLabel(this, _cityWindowProps.Labels["Citizens"]);
        _citizensLabelColor = _citizensLabel.ColorFront;
        _citizensLabelShadow = _citizensLabel.ColorShadow;
        Controls.Add(_citizensLabel);
        Controls.Add(new CityCitizensBox(this));
        UpdateAttitudeLabel();

        _iconW = Images.GetImageWidth(_active.PicSources["zoomIn"][0], _active);
        _iconH = Images.GetImageHeight(_active.PicSources["zoomIn"][0], _active);
        _exitIcon = new Button(this, String.Empty, backgroundImage: _active.PicSources["close"][0]);
        _shrinkIcon = new Button(this, String.Empty, backgroundImage: _active.PicSources["zoomIn"][0]);
        _expandIcon = new Button(this, String.Empty, backgroundImage: _active.PicSources["zoomOut"][0]);
        _exitIcon.Click += CloseButtonOnClick;
        _shrinkIcon.Click += (_, _) =>
        {
            _scale = Math.Max(_scale - _scaleDelta, _scaleMin);
            Resize(DisplayScale.Width, DisplayScale.Height);
        };
        _expandIcon.Click += (_, _) =>
        {
            _scale = Math.Min(_scale + _scaleDelta, _scaleMax);
            Resize(DisplayScale.Width, DisplayScale.Height);
        };
        Controls.Add(_shrinkIcon);
        Controls.Add(_expandIcon);
        Controls.Add(_exitIcon);
    }

    private void CloseButtonOnClick(object? sender, MouseEventArgs e)
    {
        CurrentGameScreen.CloseDialog(this);
    }

    public override int Width => (int)(_cityWindowProps.Width * _scale) + PaddingSide;
    public override int Height => (int)(_cityWindowProps.Height * _scale) + LayoutPadding.Top + LayoutPadding.Bottom;
    public float Scale => _scale;
    public City City { get; }

    /// <summary>
    /// The classic city-screen backdrop is a 640x421 'city' sheet that the
    /// standalone asset set does not ship. Resolve it once and fall back to the
    /// plain painted dialog base rather than throwing every frame — a missing
    /// backdrop hard-crashed the game the moment a city screen was opened.
    /// </summary>
    private Raylib_CSharp.Images.Image? GetBackdrop()
    {
        if (_backdropResolved)
        {
            return _backdrop;
        }

        _backdropResolved = true;
        try
        {
            _backdrop = Images.ExtractBitmap(_cityWindowProps.Image, _active);
        }
        catch (Exception)
        {
            _backdrop = null;
        }

        return _backdrop;
    }

    public override void Resize(int width, int height)
    {
        _headerLabel.FontSize = Math.Max(_active.Look.CityHeaderLabelFontSizeSmall, (int)(_active.Look.CityHeaderLabelFontSizeNormal * _scale));

        LayoutPadding = _active.GetPadding(_headerLabel.TextSize.Y, false);

        BackgroundImage = ImageUtils.PaintDialogBase(_active, Width, Height, LayoutPadding,
            GetBackdrop());

        _exitIcon.Location = new(11, 5);
        _shrinkIcon.Location = new(11 + (_iconW + 2) * _scale, 5);
        _expandIcon.Location = new(11 + (2 * _iconW + 2 * 2) * _scale, 5);
        _exitIcon.Scale = _scale;
        _shrinkIcon.Scale = _scale;
        _expandIcon.Scale = _scale;
        _shrinkIcon.Visible = _scale > _scaleMin;
        _expandIcon.Visible = _scale < _scaleMax;
        
        SetLocation(width, Width, height, Height);
        var headerOffset = 11 + (3 * _iconW + 3 * 2) * _scale;
        _headerLabel.Location = new(headerOffset, 0);
        _headerLabel.Width = Width - 2 * (int)headerOffset;
        _headerLabel.Height = LayoutPadding.Top;


        foreach (var control in Controls)
        {
            control.OnResize();
        }

        _supportLabel.Visible = true;
        _supportLabel.Text = $"{Labels.For(LabelIndex.UnitsSupported)}: {City.SupportedUnits.Count}";

    }

    /// <summary>
    /// The inset panels the city screen is organised into. Civ II groups the screen
    /// into sunken areas - green for the land, blue for production, grey for the
    /// lists - and without them every control floated on one flat sheet of stone
    /// with nothing to say which reading belonged to which heading. Each rectangle
    /// is in the layout's own 640x421 space and stops short of its heading, so the
    /// headings stay on the window's stone and stay legible.
    /// </summary>
    private static readonly (Rectangle Box, PanelTone Tone)[] Panels =
    [
        (new Rectangle(3, 0, 433, 44), PanelTone.Neutral),      // citizens
        (new Rectangle(5, 62, 192, 126), PanelTone.Land),       // resource map
        (new Rectangle(196, 44, 240, 166), PanelTone.Neutral),  // city resources
        (new Rectangle(437, 13, 195, 150), PanelTone.Land),     // food storage
        (new Rectangle(437, 207, 195, 145), PanelTone.Build),   // production shields
        (new Rectangle(5, 227, 188, 58), PanelTone.Neutral),    // units supported
        (new Rectangle(193, 227, 242, 186), PanelTone.Neutral), // units present
        (new Rectangle(5, 304, 188, 110), PanelTone.Neutral),   // city improvements
    ];

    private enum PanelTone
    {
        Neutral,
        Land,
        Build
    }

    private static Color FillFor(PanelTone tone) => tone switch
    {
        PanelTone.Land => new Color(34, 74, 38, 255),
        PanelTone.Build => new Color(26, 34, 92, 255),
        _ => new Color(84, 82, 78, 255)
    };

    /// <summary>
    /// Tiles the painted stone wallpaper across a panel.
    /// <para>
    /// The window chrome, the menu bar and the side panels are all cut from the
    /// painted slate sheet, but the city window's own panels were flat grey, so
    /// the busiest screen in the game was the one that looked unfinished. Using
    /// the same wallpaper the rest of the interface uses keeps them consistent
    /// without introducing another asset.
    /// </para>
    /// Falls back to the flat fill when there is no wallpaper -- a plain Civ II
    /// install, where the painted sheet is not present.
    /// </summary>
    private static void DrawStoneFill(Rectangle rect)
    {
        // The panel has to be filled opaquely, not left transparent: the classic
        // city backdrop this window composites underneath is not part of the
        // standalone art set, and what shows through where it is missing is not
        // something to put on screen. So this covers the same area the flat grey
        // used to -- just with the painted stone the rest of the interface is
        // made of, instead of a colour that matched nothing.
        var tiles = ImageUtils.Wallpaper?.Inner;
        var source = tiles is { Length: > 0 } ? tiles[0] : ImageUtils.InnerWallpaper;
        if (source.Width <= 0 || source.Height <= 0)
        {
            // A plain Civ II install with no painted sheet: keep the flat fill.
            Graphics.DrawRectangleRec(rect, FillFor(PanelTone.Neutral));
            return;
        }

        // TextureCache owns the handle and drops it on a ruleset or interface
        // change, so this does not add a texture that has to be unloaded here.
        var stone = TextureCache.GetImage(new MemoryStorage(source, "CityPanelStone"));

        Graphics.BeginScissorMode((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        for (var y = rect.Y; y < rect.Y + rect.Height; y += stone.Height)
        {
            for (var x = rect.X; x < rect.X + rect.Width; x += stone.Width)
            {
                Graphics.DrawTexture(stone, (int)x, (int)y, Color.White);
            }
        }
        Graphics.EndScissorMode();

        // Sink the panel slightly against the window face, which is what the
        // bevel drawn around it is there to suggest.
        Graphics.DrawRectangleRec(rect, new Color(0, 0, 0, 30));
    }

    public override void Draw(bool pulse)
    {
        if (BackgroundImage != null)
        {
            Graphics.DrawTexture(BackgroundImage.Value, (int)Location.X, (int)Location.Y, Color.White);
        }

        DrawPanels();

        foreach (var control in Controls)
        {
            control.Draw(pulse);
        }
    }

    private void DrawPanels()
    {
        var originX = Location.X + LayoutPadding.Left;
        var originY = Location.Y + LayoutPadding.Top;

        foreach (var (box, tone) in Panels)
        {
            var rect = new Rectangle(
                originX + box.X * _scale,
                originY + box.Y * _scale,
                box.Width * _scale,
                box.Height * _scale);

            if (tone == PanelTone.Neutral)
            {
                DrawStoneFill(rect);
            }
            else
            {
                // Land and Build keep a flat colour: those two panels are read as
                // data (food store, production progress) and a texture behind them
                // would fight the bars drawn on top.
                Graphics.DrawRectangleRec(rect, FillFor(tone));
            }

            // A shallow bevel: dark along the top and left, light along the bottom
            // and right, so the panel reads as sunk into the window.
            var edge = Math.Max(1f, _scale);
            Graphics.DrawRectangleRec(new Rectangle(rect.X, rect.Y, rect.Width, edge),
                new Color(0, 0, 0, 120));
            Graphics.DrawRectangleRec(new Rectangle(rect.X, rect.Y, edge, rect.Height),
                new Color(0, 0, 0, 120));
            Graphics.DrawRectangleRec(new Rectangle(rect.X, rect.Y + rect.Height - edge, rect.Width, edge),
                new Color(255, 255, 255, 70));
            Graphics.DrawRectangleRec(new Rectangle(rect.X + rect.Width - edge, rect.Y, edge, rect.Height),
                new Color(255, 255, 255, 70));
        }
    }

    public override void OnKeyPress(KeyboardKey key)
    {
        if (key is KeyboardKey.Escape or KeyboardKey.Enter or KeyboardKey.KpEnter)
        {
            CurrentGameScreen.CloseDialog(this);
            return;
        }

        // Left and right step through the player's cities without going back to the
        // map, which is how Civ II's city window works and how a turn's worth of
        // production is set in a few seconds rather than a few dozen clicks.
        if (key is KeyboardKey.Left or KeyboardKey.Right)
        {
            StepToAnotherCity(key == KeyboardKey.Right ? 1 : -1);
            return;
        }

        base.OnKeyPress(key);
    }

    /// <summary>
    /// Closes this window and opens the next city along, alphabetically. Does
    /// nothing when the player holds only one.
    /// <para>
    /// It used to step in the order the cities were founded, which is an order only
    /// the game knows. By name, left and right walk the same list the player sees
    /// everywhere else that cities are listed.
    /// </para>
    /// </summary>
    private void StepToAnotherCity(int step)
    {
        var cities = CurrentGameScreen.Player.Civilization.Cities
            .OrderBy(city => city.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (cities.Count < 2)
        {
            return;
        }

        var index = cities.IndexOf(City);
        if (index < 0)
        {
            return;
        }

        var next = cities[((index + step) % cities.Count + cities.Count) % cities.Count];
        CurrentGameScreen.CloseDialog(this);
        CurrentGameScreen.ShowCityWindow(next);
    }

    /// <summary>Opens this city's Change Production list. Used by the review harness.</summary>
    public void ShowChangeProduction() =>
        Controls.OfType<ProductionBox>().FirstOrDefault()?.ShowChangeProductionDialog();

    public void UpdateProduction()
    {
        City.CalculateOutput(City.Owner.Government, CurrentGameScreen.Game);
        UpdateAttitudeLabel();
        ResourceProductionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateAttitudeLabel()
    {
        var mood = City.CalculateHappiness(CurrentGameScreen.Game);
        var celebrating = mood.UnhappyCitizens == 0 &&
                          City.Size - mood.HappyCitizens <= City.Size / 2;
        _citizensLabel.ColorFront = celebrating
            ? new Color(255, 223, 79, 255)
            : mood.IsInDisorder
                ? new Color(255, 79, 63, 255)
                : _citizensLabelColor;
        _citizensLabel.ColorShadow = celebrating || mood.IsInDisorder
            ? Color.Black
            : _citizensLabelShadow;
    }

    public event EventHandler? ResourceProductionChanged;
}
