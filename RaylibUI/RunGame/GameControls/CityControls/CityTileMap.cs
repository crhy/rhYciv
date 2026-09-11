using Model.Core.Cities;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model;
using Model.Controls;
using Model.Core;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Fonts;
using Raylib_CSharp.Images;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Transformations;
using RaylibUI.RunGame.GameControls.Mapping;
using RaylibUtils;
using System.Numerics;
using Model.Core.Mapping;

namespace RaylibUI.RunGame.GameControls.CityControls;

public class CityTileMap : BaseControl
{
    private readonly CityWindow _cityWindow;
    private Texture2D? _texture;
    private float _scaleFactor;
    private Vector2 _offset;
    private readonly string _text;
    private readonly IUserInterface _active;
    private readonly int _organizationLevel;
    private readonly CityLabel _label;
    private readonly CityWindowLayout _props;

    private IList<IViewElement> _viewElements;

    public CityTileMap(CityWindow cityWindow, IGame game) : base(cityWindow)
    {
        _cityWindow = cityWindow;
        _active = cityWindow.MainWindow.ActiveInterface;
        _props = _cityWindow.CityWindowProps;
        Click += OnClick;
        _label = new CityLabel(_cityWindow, _props.Labels["ResourceMap"]);
        Controls.Add(_label);
        _organizationLevel = cityWindow.City.GetOrganizationLevel(game.Rules);
    }

    /// <summary>
    /// Where the resource map puts each square, in the coordinates of the picture it
    /// composes. The picture is four tiles across and the city sits at its centre,
    /// so a square's position follows directly from how far it is from the city.
    /// <para>
    /// Drawing and clicking have to agree about this, and used not to: the click
    /// worked the square out again from scratch with its own division, its own
    /// diagonal-edge corrections and an adjustment its author recorded as not
    /// understanding. Squares it disagreed with could not be clicked at all.
    /// </para>
    /// </summary>
    private static Vector2 TilePosition(Tile tile, City city, MapDimensions dim, int xCentre, int yCentre) =>
        new(xCentre + (tile.X - city.Location.X) * dim.HalfWidth,
            yCentre + (tile.Y - city.Location.Y) * dim.HalfHeight);

    private static (int XCentre, int YCentre) MapCentre(MapDimensions dim) =>
        (dim.TileWidth * 4 / 2 - dim.HalfWidth, dim.TileHeight * 4 / 2 - dim.HalfHeight);

    /// <summary>
    /// The square under a point in the picture, or null. A map square is a diamond,
    /// so a point is inside it when its distance from the centre, measured as a
    /// share of the half-width and half-height, adds up to no more than one.
    /// </summary>
    private static Tile? TileAt(Vector2 point, City city, MapDimensions dim, int xCentre, int yCentre)
    {
        Tile? best = null;
        var bestDistance = double.MaxValue;

        foreach (var tile in city.Location.CityRadius())
        {
            var position = TilePosition(tile, city, dim, xCentre, yCentre);
            var offsetX = Math.Abs(point.X - (position.X + dim.HalfWidth));
            var offsetY = Math.Abs(point.Y - (position.Y + dim.HalfHeight));
            var distance = offsetX / (double)dim.HalfWidth + offsetY / (double)dim.HalfHeight;
            if (distance <= 1.0 && distance < bestDistance)
            {
                best = tile;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void OnClick(object? sender, MouseEventArgs e)
    {
        if (_cityWindow.ViewOnly)
        {
            // Somebody else's city, opened by a Diplomat's report. Their citizens
            // are not ours to move about.
            return;
        }

        var city = _cityWindow.City;
        var gameScreen = _cityWindow.CurrentGameScreen;

        var clickInPicture = (GetRelativeMousePosition() - _offset) / _scaleFactor;
        var dim = gameScreen.TileCache.GetDimensions(city.Location.Map, gameScreen.Zoom);
        var (xCentre, yCentre) = MapCentre(dim);

        if (TileAt(clickInPicture, city, dim, xCentre, yCentre) is not { } tile)
        {
            return;
        }

        // if there are foreign units here can't use this square
        if (tile.UnitsHere.Any(u => u.Owner != city.Owner))
        {
            // play bad action sound??
            return;
        }
        if (tile.CityHere != null)
        {
            if (tile.CityHere != city)
            {
                //play bad action sound?
                return;
            }

            foreach (var wt in city.WorkedTiles.ToArray())
            {
                wt.WorkedBy = null;
            }
            city.AutoAddDistributionWorkers(gameScreen.Game.Rules);
                    
        }
        else if (tile.WorkedBy != null)
        {
            if (tile.WorkedBy != city)
            {
                //Play bad action sound?
                return;
            }

            // The citizen comes off the land and becomes a specialist rather than
            // vanishing. Without this the city quietly lost a worker's output and
            // gained nothing, and the specialist count could never rise.
            if (city.NoOfSpecialistsx4 / 4 >= city.Size)
            {
                return;
            }

            tile.WorkedBy = null;
            city.NoOfSpecialistsx4 += 4;
            city.GetSpecialistTypes();
        }
        else
        {
            // Putting a citizen back on the land takes one off the specialists.
            if (city.NoOfSpecialistsx4 < 4)
            {
                // Play bad action?
                return;
            }

            city.NoOfSpecialistsx4 -= 4;
            city.GetSpecialistTypes();
            tile.WorkedBy = city;
        }

        _cityWindow.UpdateProduction();
        Redraw();
    }

    public override void Draw(bool pulse)
    {
        var adjustedLocation = new Vector2(Parent.Bounds.X, Parent.Bounds.Y) + Location + _offset;
        Graphics.DrawTextureEx(_texture.Value, adjustedLocation, 0, _scaleFactor, Color.White);

        foreach (var element in _viewElements)
        {
            element.Draw(element.Location * _scaleFactor + adjustedLocation, _scaleFactor);
        }

        _label.Draw(true);
    }

    /// <summary>
    /// The composed resource map. It replaces its own texture on each redraw, but
    /// the last one went with the window when it closed and was never given back.
    /// </summary>
    public override void ReleaseTextures()
    {
        if (_texture.HasValue)
        {
            _texture.Value.Unload();
            _texture = null;
        }

        base.ReleaseTextures();
    }

    public override void OnResize()
    {
        var pos = _cityWindow.CityWindowProps.TileMap.ScaleAll(_cityWindow.Scale);
        Location = new(_cityWindow.LayoutPadding.Left + pos.X, _cityWindow.LayoutPadding.Top + pos.Y);
        Width = (int)pos.Width;
        Height = (int)pos.Height;
        base.OnResize();
        _label.OnResize();
        Redraw();
    }

    private void Redraw()
    {
        var city = _cityWindow.City;
        var gameScreen = _cityWindow.CurrentGameScreen;

        var cities = gameScreen.Main.ActiveInterface.CityImages;
        var unitsSet = gameScreen.Main.ActiveInterface.UnitImages;
        var tileCache = gameScreen.TileCache;

        Map map = city.Location.Map;
        var dim = tileCache.GetDimensions(map, gameScreen.Zoom);
        var width = dim.TileWidth * 4;
        var height = dim.TileHeight * 4;
        var (xcentre, ycentre) = MapCentre(dim);
        var image = Image.GenColor(width, height, new Color(0, 0, 0, 0));

        var elements = new List<IViewElement>();
        var cityData = new List<Element>();
        var activeCiv = _cityWindow.CurrentGameScreen.Player.Civilization;
        foreach (var tile in city.Location.CityRadius())
        {
            // We use the active civ here not the city owner as we may be viewing other players cities
            if (tile.IsVisible(activeCiv.Id))
            {
                var tileImage = tileCache.GetTileDetails(tile, city.Owner.Id);
                var position = TilePosition(tile, city, dim, xcentre, ycentre);
                var locationX = (int)position.X;
                var locationY = (int)position.Y;
                var dstRec = new Rectangle(locationX,
                    locationY, dim.TileWidth, dim.TileHeight);
                
                // Take the whole composed tile, not a 64x32 window into it. Terrain is
                // composed at a scale that follows the zoom now, so sampling the classic
                // tile rectangle grabbed a corner of a much larger image and the city's
                // resource map came out mostly empty while the main map looked right.
                image.Draw(tileImage.Image,
                    new Rectangle(0, 0, tileImage.Image.Width, tileImage.Image.Height),
                    dstRec,
                    Color.White);
                if (tile.CityHere != null)
                {
                    var cityStyleIndex = tile.CityHere.Owner.CityStyle;
                    if (tile.CityHere.Owner.Epoch == (int)EpochType.Industrial)
                    {
                        cityStyleIndex = 4;
                    }
                    else if (tile.CityHere.Owner.Epoch == (int)EpochType.Modern)
                    {
                        cityStyleIndex = 5;
                    }
                    var sizeIncrement =
                        gameScreen.Main.ActiveInterface.GetCityIndexForStyle(cityStyleIndex,
                            tile.CityHere, tile.CityHere.Size);
                    cityData.Add(new Element
                    {
                        Image = Images.ExtractBitmap(cities.Sets[cityStyleIndex][sizeIncrement]
                            .Image, gameScreen.Main.ActiveInterface),
                        DestRec = dstRec
                    });
                }
                else if (tile.UnitsHere.Any(u => u.Owner != city.Owner))
                {
                    ImageUtils.GetUnitTextures(tile.GetTopUnit(), _active, _cityWindow.CurrentGameScreen.Game, elements,
                        new Vector2(locationX, locationY - (int)unitsSet.UnitRectangle.Height + dim.TileHeight), true);
                    // units.Add(new Element()
                    // {
                    //     Image = ImageUtils.GetUnitImage(gameScreen.Main.ActiveInterface, tile.GetTopUnit()),
                    //     X = locationX,
                    //     Y = locationY - (int)unitsSet.UnitRectangle.Height + dim.TileHeight
                    // });
                }

                if (tile.WorkedBy != null && tile.WorkedBy != city)
                {
                    // Take the whole marker, not a 64x32 window into it: the art is a
                    // full map tile now, so the classic rectangle sampled a corner.
                    var marker = Images.ExtractBitmap(
                        gameScreen.Main.ActiveInterface.MapImages.ViewPiece, _active);
                    image.Draw(marker, new Rectangle(0, 0, marker.Width, marker.Height),
                        dstRec, Color.Red);
                }
            }
        }

        //Cities must be drawn after terrain since they sometimes overdraw onto later tiles
        foreach (var cityDetails in cityData)
        {
            image.Draw(cityDetails.Image, cities.CityRectangle,
                cityDetails.DestRec,
                Color.White);
        }


        var resources =
            gameScreen.Main.ActiveInterface.ResourceImages.ToDictionary(k => k.Name,
                v => Images.ExtractBitmap(v.SmallImage, gameScreen.Main.ActiveInterface));

        var lowOrganisation = _organizationLevel == 0;
        var totalDrawWidth = dim.TileWidth - 20;
        var resourceXOffset = 8;
        var resourceWidth = resources.First().Value.Width;
        var resourceHeight = resources.First().Value.Height;

        // Sized against the tile rather than against the icon file. The icons used
        // to be 10 pixels square and were drawn at a fixed 1.45x, which both tied
        // the overlay to one particular piece of art and left it too small to read
        // on the city map. A third of the tile height is about as large as three
        // food plus two shields can get and still fit across one tile.
        var drawResourceHeight = Math.Max(8, (int)Math.Round(dim.TileHeight / 3.0));
        var drawResourceWidth = Math.Max(8,
            (int)Math.Round(drawResourceHeight * resourceWidth / (double)resourceHeight));
        var resourceYOffset = dim.HalfHeight - drawResourceHeight / 2;
        var resourceRect = new Rectangle(0, 0, resourceWidth, resourceHeight);
        foreach (var workedTile in city.WorkedTiles)
        {
            var food = workedTile.GetFood(lowOrganisation);
            var shields = workedTile.GetShields(lowOrganisation);
            var trade = workedTile.GetTrade(_organizationLevel);

            var totalResources = food + shields + trade;
            if (totalResources > 0)
            {
                var locationX = xcentre + (workedTile.X - city.Location.X) * dim.HalfWidth + resourceXOffset;
                var locationY = ycentre + (workedTile.Y - city.Location.Y) * dim.HalfHeight + resourceYOffset;
                var spacing = Math.Min(drawResourceWidth + 1, Math.Max(totalDrawWidth / totalResources, 1));
                var destRect = new Rectangle(locationX, locationY, drawResourceWidth, drawResourceHeight);
                for (var i = 0; i < food; i++)
                {
                    image.Draw(resources["Food"], resourceRect, destRect, Color.White);
                    destRect.X += spacing;
                }
                for (var i = 0; i < shields; i++)
                {
                    image.Draw(resources["Shields"], resourceRect, destRect, Color.White);
                    destRect.X += spacing;
                }
                for (var i = 0; i < trade; i++)
                {
                    image.Draw(resources["Trade"], resourceRect, destRect, Color.White);
                    destRect.X += spacing;
                }
            }
        }

        if (_texture.HasValue)
        {
            _texture.Value.Unload();
        }

        _viewElements = elements;

        _texture = Texture2D.LoadFromImage(image);
        _scaleFactor = Width / (float)_texture.Value.Width;
        
        _offset = new Vector2(0, (Height - height * _scaleFactor) / 2f);
        image.Unload();
    }
}

public struct Element
{
    public Image Image { get; init; }
    public Rectangle DestRec { get; init; }
}