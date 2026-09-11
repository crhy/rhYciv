using System.Diagnostics;
using System.Numerics;
using System.Xml.Serialization;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.MapObjects;
using Model;
using Model.ImageSets;
using Model.Interface;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Fonts;
using Raylib_CSharp.Images;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Transformations;
using RaylibUI.RunGame.GameControls.Mapping.Views.ViewElements;
using RaylibUtils;
using ExtensionMethods;
using Model.Core.Mapping;

namespace RaylibUI.RunGame.GameControls.Mapping.Views;

public abstract class BaseGameView : IGameView
{
    private readonly GameScreen _gameScreen;
    private readonly IList<Tile> _actionTiles;
    private int _currentIndex;

    private IList<IList<IViewElement>> _animations = new List<IList<IViewElement>>();
    private bool _preserve;
    
    protected readonly Vector2 ActivePos;
    protected MapDimensions Dimensions;

    /// <summary>
    /// Offset of view area from map start (in px)
    /// <0 ... view area shows whole map
    /// >0 ... a fraction of the map shown
    /// </summary>
    private Vector2 _offsets = Vector2.Zero;
    public Vector2 Offsets => _offsets;

    /// <summary>
    /// Shift of map start in x-direction (in tiles, =0 for flat)
    /// </summary>
    private int _xShift;
    public int Xshift => _xShift;

    /// <summary>
    /// How much larger than the classic sprite the flag over a city is drawn.
    /// <para>
    /// The classic flag is about a dozen pixels tall, which was the right size
    /// against a 64x32 tile on a 640x480 screen. On a map composed several times
    /// that size, beside city art drawn from 300px sources, it disappeared: 0.1.3
    /// made it sharp but left it that size, so it was a sharp speck.
    /// </para>
    /// </summary>
    private const float FlagScale = 3f;

    /// <summary>
    /// How far left of its anchor the flag is drawn, as a share of its own width.
    /// </summary>
    private const float FlagLeftShift = 0.6f;

    public bool IsDefault { get; }
    public int Interval { get; }
    public IList<Tile> ActionTiles => _actionTiles;

    protected BaseGameView(GameScreen gameScreen, Tile location, IGameView? previousView, int viewHeight, int viewWidth, bool isDefault, int interval, IList<Tile> actionTiles, bool forceRedraw)
    {
        IsDefault = isDefault;
        Interval = interval;
        _gameScreen = gameScreen;
        _actionTiles = actionTiles;

        Location = location;
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
        RenderScale = DisplayScale.Factor;

        var map = location.Map;
        Dimensions = _gameScreen.TileCache.GetDimensions(map, gameScreen.Zoom);
        
        var activeInterface = _gameScreen.Main.ActiveInterface;
        
        var cities = activeInterface.CityImages;
        var civilizationId = _gameScreen.VisibleCivId;
        // Force redraw should be checked last as IsSameArea will set offsets 
        if (previousView != null && IsInSameArea(previousView, location, Dimensions, forceRedraw) && !forceRedraw)
        {
            ActivePos = GetPosForTile(location);
            BaseImage = previousView.BaseImage;
            
            //Update elements where action just happened if any
            var previousAction = previousView.ActionTiles.Where(t=>!_actionTiles.Contains(t)).ToList();
            var newElements = new List<IViewElement>();
            foreach (var tile in previousAction)
            {
                var pos = GetPosForTile(tile);
                
                var tileDetails = _gameScreen.TileCache.GetTileDetails(tile, civilizationId);
                CalculateElementsAtTile(gameScreen, tile, newElements, activeInterface,cities,pos,tileDetails, civilizationId);
            }
            Elements = previousView.Elements.Where(e=> !previousAction.Contains(e.Tile)).Concat(newElements).ToArray();
            
            previousView.Preserve();
        }
        else
        {
            var elements = new List<IViewElement>();
            if (_offsets == Vector2.Zero)
            {
                CalculateOffsets(null, location, Dimensions, force: true);
            }

            // Tiles drawn into this view must survive until it is finished with them.
            _gameScreen.TileCache.BeginViewBuild();

            var image = Image.GenColor(
                Math.Max(1, (int)MathF.Ceiling(ViewWidth * RenderScale)),
                Math.Max(1, (int)MathF.Ceiling(ViewHeight * RenderScale)),
                Color.Black);
            var dim = _gameScreen.TileCache.GetDimensions(map, gameScreen.Zoom);
            var ypos = -_offsets.Y;

            // Every tile in this picture is drawn into a square of the same size, so
            // the size is worked out once rather than per tile, and the grid overlay
            // is looked up once rather than being re-extracted for every square it is
            // drawn over.
            var drawnWidth = (int)(dim.TileWidth * RenderScale);
            var drawnHeight = (int)(dim.TileHeight * RenderScale);
            var fastCompose = TileCompositor.CanCompose(image) && drawnWidth > 0 && drawnHeight > 0;
            var gridOverlay = _gameScreen.ShowGrid
                ? Images.ExtractBitmap(activeInterface.PicSources["gridlines"][0])
                : (Image?)null;

            for (var row = 0; row < map.YDim; row++)
            {
                if (ypos >= -dim.TileHeight)
                {
                    var xpos = -_offsets.X + (row % 2 * dim.HalfWidth);
                    if (!map.Flat && xpos + dim.TotalWidth < ViewWidth)
                    {
                        if (ViewWidth >= dim.TotalWidth)
                        {
                            xpos = -_offsets.X + (row % 2 * dim.HalfWidth);
                        }
                        else
                        {
                            xpos += dim.TotalWidth;
                        }
                    }

                    for (var col = _xShift / 2; col < _xShift / 2 + map.XDim; col++)
                    {
                        if (xpos >= -dim.TileWidth)
                        {
                            if (xpos >= ViewWidth)
                            {
                                if (!map.Flat)
                                {
                                    xpos -= dim.TotalWidth;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            var tile = map.Tile[col % map.XDim, row];
                            if (tile == location)
                            {
                                ActivePos = new Vector2(xpos, ypos);
                            }

                            if (tile.IsVisible(civilizationId) || map.MapRevealed)
                            {
                                var tileDetails = _gameScreen.TileCache.GetTileDetails(tile, civilizationId);
                                var target = ScaleRectangle(new Rectangle(xpos, ypos, dim.TileWidth, dim.TileHeight));
                                DrawTileInto(image, tileDetails.Image, target, drawnWidth, drawnHeight,
                                    fastCompose, _gameScreen.TileCache, tileDetails);

                                if (gridOverlay is { } grid)
                                {
                                    image.Draw(grid, new Rectangle(0, 0, grid.Width, grid.Height),
                                        target, Color.White);
                                }

                                var posVector = new Vector2(xpos, ypos);
                                CalculateElementsAtTile(gameScreen, tile, elements, activeInterface, cities, posVector, tileDetails, civilizationId);
                            }
                        }

                        xpos += dim.TileWidth;
                    }
                }

                ypos += dim.HalfHeight;
                if (ypos > ViewHeight)
                {
                    break;
                }
            }

            this.BaseImage = Texture2D.LoadFromImage(image);
            this.BaseImage.SetFilter(TextureFilter.Bilinear);
            this.Elements = elements.ToArray();

            image.Unload();

            gameScreen.TriggerMapEvent(new MapEventArgs(MapEventType.MapViewChanged)
            {
                MapStartXy = [Utils.WrapNumber(Math.Max(0, (int)_offsets.X / dim.HalfWidth) + _xShift, 2 * map.XDim), 
                    (int)_offsets.Y / dim.HalfHeight],
                MapDrawSq = [ViewWidth / dim.HalfWidth, ViewHeight / dim.HalfHeight],
                Xshift = _xShift
            });
        }
    }


    /// <summary>
    /// Puts one map tile into the picture being composed.
    /// <para>
    /// Both steps this replaces were done inside raylib's own image drawing, once
    /// per tile per redraw: resampling the tile to the size it is drawn at, and
    /// blending it a pixel at a time through a general-purpose path. Between them
    /// they accounted for practically the whole cost of redrawing the map. The
    /// resampled tile is now kept by the tile cache and the blend is a straight run
    /// over rows, and raylib is still used for anything the fast path cannot
    /// handle.
    /// </para>
    /// </summary>
    private static void DrawTileInto(Image destination, Image tile, Rectangle target,
        int drawnWidth, int drawnHeight, bool fastCompose, TileTextureCache cache, TileDetails details)
    {
        if (!fastCompose)
        {
            destination.Draw(tile, new Rectangle(0, 0, tile.Width, tile.Height), target, Color.White);
            return;
        }

        var drawable = cache.DrawableImage(details, drawnWidth, drawnHeight);
        if (!TileCompositor.CanCompose(drawable))
        {
            destination.Draw(tile, new Rectangle(0, 0, tile.Width, tile.Height), target, Color.White);
            return;
        }

        TileCompositor.Blend(destination, drawable, (int)target.X, (int)target.Y);
    }

    private static int GetCitySizeIndexForStyle(int cityStyleIndex, int citySize)
    {
        return cityStyleIndex switch
        {
            4 => citySize switch
            {
                <= 4 => 0,
                <= 7 => 1,
                <= 10 => 2,
                _ => 3
            },
            5 => citySize switch
            {
                <= 4 => 0,
                <= 10 => 1,
                <= 18 => 2,
                _ => 3
            },
            _ => citySize switch
            {
                <= 3 => 0,
                <= 5 => 1,
                <= 7 => 2,
                _ => 3
            }
        };
    }

    /// <summary>
    /// The Civ2 logical footprint a city sprite occupies. High-resolution art is
    /// fitted into this box so it does not change map layout.
    /// </summary>
    private static Vector2 GetCityLogicalSize(CityImage cityImage, CityImageSet cities, Texture2D texture)
    {
        if (cityImage.LogicalSize.X > 0 && cityImage.LogicalSize.Y > 0)
        {
            return cityImage.LogicalSize;
        }

        if (cities.CityRectangle.Width > 0 && cities.CityRectangle.Height > 0)
        {
            return new Vector2(cities.CityRectangle.Width, cities.CityRectangle.Height);
        }

        return new Vector2(texture.Width, texture.Height);
    }

    /// <summary>
    /// Scale that fits a source texture inside a logical box without cropping.
    /// Textures already at or below the logical size are drawn unscaled.
    /// </summary>
    private static float GetContainedRenderScale(Texture2D texture, Vector2 logicalSize)
    {
        if (texture.Width <= 0 || texture.Height <= 0 || logicalSize.X <= 0 || logicalSize.Y <= 0)
        {
            return 1f;
        }

        if (texture.Width <= logicalSize.X && texture.Height <= logicalSize.Y)
        {
            return 1f;
        }

        return MathF.Max(0.01f,
            MathF.Min(logicalSize.X / texture.Width, logicalSize.Y / texture.Height));
    }

    /// <summary>
    /// Centres a contained texture horizontally and sits it on the bottom of the
    /// logical box, matching how the classic sprites are laid out.
    /// </summary>
    private static Vector2 GetContainedDrawOffset(Texture2D texture, Vector2 logicalSize, float renderScale)
    {
        var drawWidth = texture.Width * renderScale;
        var drawHeight = texture.Height * renderScale;
        return new Vector2(
            MathF.Max(0f, (logicalSize.X - drawWidth) / 2f),
            MathF.Max(0f, logicalSize.Y - drawHeight));
    }

    private void CalculateElementsAtTile(GameScreen gameScreen, Tile tile, List<IViewElement> elements,
        IUserInterface activeInterface,
        CityImageSet cities,
        Vector2 posVector, TileDetails tileDetails, int civilizationId)
    {
        if (tile.PlayerKnowledge == null || tile.PlayerKnowledge.Length <= civilizationId ||
            tile.PlayerKnowledge[civilizationId] == null)
        {
            return; //We know nothing of this tile 
        }

        var playerKnowledge = tile.PlayerKnowledge[civilizationId]!;
        var cityHere = playerKnowledge.CityHere;
        if (cityHere != null)
        {
            var apparentOwner = _gameScreen.Game.Players[cityHere.OwnerId].Civilization;
            var cityStyleIndex = _gameScreen.Main.ActiveInterface.GetCityStyleIndexFromEpoch(apparentOwner.CityStyle, apparentOwner.Epoch);
            var sizeIncrement = tile.CityHere is { } city
                ? _gameScreen.Main.ActiveInterface.GetCityIndexForStyle(cityStyleIndex, city, cityHere.Size)
                : GetCitySizeIndexForStyle(cityStyleIndex, cityHere.Size);
            
            var cityImage = cities.Sets[cityStyleIndex][sizeIncrement];

            // Prefer the native 300x300 FOSS city art. It is drawn into the
            // classic sprite's logical footprint, so the map layout, flag anchor
            // and size marker are unchanged while zoomed-in views render from
            // the high-resolution source.
            var cityTexture = TextureCache.GetImage(cityImage.MapImage ?? cityImage.Image);
            var cityLogicalSize = GetCityLogicalSize(cityImage, cities, cityTexture);
            var cityRenderScale = GetContainedRenderScale(cityTexture, cityLogicalSize);
            var cityDrawOffset = GetContainedDrawOffset(cityTexture, cityLogicalSize, cityRenderScale);

            var cityPos = posVector with { Y = posVector.Y + Dimensions.TileHeight - cityLogicalSize.Y.ZoomScale(gameScreen.Zoom) };
            elements.Add(new CityData(
                color: activeInterface.PlayerColours[cityHere.OwnerId],
                name: cityHere.Name,
                size: cityHere.Size,
                sizeRectLoc: cityImage.SizeLoc,
                texture: cityTexture,
                location: cityPos, tile: tile,
                logicalSize: cityLogicalSize,
                offset: cityDrawOffset,
                renderScale: cityRenderScale));
            if (tile.UnitsHere.Count > 0)
            {
                // Prefer the high-resolution flag, fitted into the classic sprite's
                // footprint so the pole still lands on the city's flag anchor.
                var colours = activeInterface.PlayerColours[cityHere.OwnerId];
                var flagTexture = TextureCache.GetImage(colours.MapImage ?? colours.Image);
                var flagLogicalSize = colours.MapImage != null && colours.LogicalSize.Y > 0
                    ? colours.LogicalSize * FlagScale
                    : new Vector2(flagTexture.Width, flagTexture.Height);
                var flagRenderScale = GetContainedRenderScale(flagTexture, flagLogicalSize);
                // The anchor marks where the classic flag's pole stood, and the cloth
                // hangs to the right of it. Drawing the art three times that size
                // therefore pushed three times as much cloth across the buildings,
                // so the flag is carried back to the left by most of the extra
                // width it gained.
                var flagShift = flagLogicalSize.X * FlagLeftShift;
                var flagOffset = cityImage.FlagLoc - new Vector2(flagShift, flagLogicalSize.Y - 5)
                                 + GetContainedDrawOffset(flagTexture, flagLogicalSize, flagRenderScale);
                elements.Add(new TextureElement(texture: flagTexture,
                    tile: tile, location: cityPos, offset: flagOffset,
                    renderScale: flagRenderScale, maxDrawSize: flagLogicalSize)
                );
            }
        }
        else if ((tile.Map.MapRevealed || tile.Map.IsCurrentlyVisible(tile, civilizationId)) && tile.UnitsHere.Count > 0)
        {
            var unit = tile.GetTopUnit();

            if (tileDetails.ForegroundElement is UnitHidingImprovement unitImp)
            {
                if (unitImp.UnitDomain == unit.Domain)
                {
                    var impImage = ImageUtils.GetImpImage(activeInterface, unitImp.UnitImage,
                        tile.Owner);
                    elements.Add(new TextureElement(
                        texture: impImage, location: posVector with { Y = posVector.Y + Dimensions.TileHeight - impImage.Height.ZoomScale(gameScreen.Zoom) }, tile: tile, isTerrain: true));
                }
                else
                {
                    var impImage = ImageUtils.GetImpImage(activeInterface, unitImp.Image,
                        tile.Owner);
                    elements.Add(new TextureElement(
                        texture: impImage,
                        location: posVector with { Y = posVector.Y + Dimensions.TileHeight - impImage.Height.ZoomScale(gameScreen.Zoom) },
                        tile: tile, isTerrain: true));

                    
                    ImageUtils.GetUnitTextures(unit, activeInterface, gameScreen.Game, elements,
                        posVector with
                        {
                            Y = posVector.Y + Dimensions.TileHeight -
                                activeInterface.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom)
                        }, useMapArt: true);
                }
            }
            else
            {
                ImageUtils.GetUnitTextures(unit, activeInterface, gameScreen.Game, elements,
                    posVector with
                    {
                        Y = posVector.Y + Dimensions.TileHeight -
                            activeInterface.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom)
                    }, useMapArt: true);
                if (tileDetails.ForegroundElement != null)
                {
                    var impImage = ImageUtils.GetImpImage(activeInterface,
                        tileDetails.ForegroundElement.Image, tile.Owner);
                    elements.Add(new TextureElement(
                        texture: impImage,
                        location: posVector with{ Y = posVector.Y + Dimensions.TileHeight - impImage.Height.ZoomScale(gameScreen.Zoom)
                        }, tile: tile, isTerrain: true));
                }
            }
        }
        else if (tileDetails.ForegroundElement != null)
        {
            var impImage = ImageUtils.GetImpImage(activeInterface,
                tileDetails.ForegroundElement.Image, tile.Owner);
            elements.Add(new TextureElement(
                texture: impImage,
                location: posVector with { Y = posVector.Y + Dimensions.TileHeight - impImage.Height.ZoomScale(gameScreen.Zoom) },
                tile: tile, isTerrain: true));
        }
    }

    protected Vector2 GetPosForTile(Tile tile)
    {
        return new Vector2(Utils.WrapNumber(tile.XIndex - _xShift / 2, Location.Map.XDim) * Dimensions.TileWidth + tile.Odd * Dimensions.HalfWidth,
                   tile.Y * Dimensions.HalfHeight) - _offsets;
    }

    public Texture2D BaseImage { get; set; }

    private Rectangle ScaleRectangle(Rectangle rectangle) => new(
        rectangle.X * RenderScale,
        rectangle.Y * RenderScale,
        rectangle.Width * RenderScale,
        rectangle.Height * RenderScale);

    private bool IsInSameArea(IGameView previousView, Tile location, MapDimensions dimensions, bool force = false)
    {
        if (previousView.Location.Map != location.Map) return false;
        if (previousView.ViewHeight != ViewHeight || previousView.ViewWidth != ViewWidth) return false;
        if (Math.Abs(previousView.RenderScale - RenderScale) > 0.001f) return false;

        return !CalculateOffsets(previousView, location, dimensions, force);
    }

    private bool CalculateOffsets(IGameView? previousView, Tile location, MapDimensions dimensions, bool force = false)
    {
        if (previousView != null)
        {
            _offsets = previousView.Offsets;
            _xShift = previousView.Xshift;
        }
        bool setOffsetX, setOffsetY;
        int offsetX, offsetY, xShift;

        // Whole map in y-dir shown
        if (ViewHeight >= dimensions.TotalHeight)
        {
            offsetY = (dimensions.TotalHeight - ViewHeight) /2;
            setOffsetY = offsetY != (int)_offsets.Y;
        }
        // Part of map in y-dir shown
        else
        {
            // Get candidates for new offsets
            var tileTop = location.Y * dimensions.HalfHeight;    // map start to active tile
            offsetY = tileTop + dimensions.HalfHeight - ViewHeight / 2;  // center active tile on screen
            offsetY = (int)(Math.Round((double)offsetY / dimensions.HalfHeight, 0) * dimensions.HalfHeight);  // round offset to a half tile value

            var currentOffsetYPos = tileTop - _offsets.Y;   // Distance between active tile and start of view area in y-dir

            // Set offset if active tile is beyond view area
            setOffsetY = currentOffsetYPos < 0 || currentOffsetYPos + dimensions.TileHeight > ViewHeight;

            // Limit view to edges of map (if active tile is beyond view or when starting game or when moving with mouse)
            if (offsetY < 0)
            {
                offsetY = 0;
                setOffsetY = offsetY != (int)_offsets.Y;
            }
            else if (offsetY + ViewHeight > dimensions.TotalHeight)
            {
                offsetY = dimensions.TotalHeight - ViewHeight;
                setOffsetY = offsetY != (int)_offsets.Y;
            }
        }

        xShift = location.Map.Flat ? 0 : Utils.WrapNumber(location.X - 2 * location.Map.XDim / 2, 2 * location.Map.XDim);
        if (previousView == null)
        {
            _xShift = xShift;
        }

        // Whole map in x-dir shown
        var wholeMapAcross = ViewWidth >= dimensions.TotalWidth;
        if (wholeMapAcross)
        {
            offsetX = (dimensions.TotalWidth - ViewWidth) /2;
            setOffsetX = offsetX != (int)_offsets.X;
        }
        // Part of map in x-dir shown
        else
        {
            // Get candidates for new offsets
            var tileLeft = Utils.WrapNumber(location.X - _xShift, 2 * location.Map.XDim) * dimensions.HalfWidth;    // map start to active tile
            offsetX = tileLeft + dimensions.HalfWidth - ViewWidth / 2;  // center active tile on screen
            offsetX = (int)(Math.Round((double)offsetX / dimensions.HalfWidth, 0) * dimensions.HalfWidth);  // round offset to a half tile value

            var currentOffsetXPos = tileLeft - _offsets.X;  // Distance between active tile and start of view area in x-dir
            setOffsetX = currentOffsetXPos < 0 || currentOffsetXPos + dimensions.TileWidth > ViewWidth;   // Active tile is beyond view area

            if (location.Map.Flat)
            {
                if (offsetX < 0)
                {
                    offsetX = 0;
                    setOffsetX = offsetX != (int)_offsets.X;
                }
                else if (offsetX + ViewWidth > dimensions.TotalWidth)
                {
                    offsetX = dimensions.TotalWidth - ViewWidth;
                    setOffsetX = offsetX != (int)_offsets.X;
                }
            }
            else
            {
                if (setOffsetY || setOffsetX || force)
                {
                    // Recalculate offset with new xShift
                    tileLeft = Utils.WrapNumber(location.X - xShift, 2 * location.Map.XDim) * dimensions.HalfWidth;    // map start to active tile
                    offsetX = tileLeft + dimensions.HalfWidth - ViewWidth / 2;  // center active tile on screen
                    offsetX = (int)(Math.Round((double)offsetX / dimensions.HalfWidth, 0) * dimensions.HalfWidth);  // round offset to a half tile value
                    setOffsetX = true;
                }
            }
        }

        if (force || setOffsetX || setOffsetY)
        {
            _offsets.X = offsetX;
            _offsets.Y = offsetY;

            // The shift decides which column of a round world the drawing starts
            // from, and it only means anything alongside an offset computed from
            // it. When the whole map is across the screen the offset above just
            // centres it and the shift is not consulted at all -- but it was being
            // overwritten with a value derived from the active square anyway, which
            // rotated the world sideways with nothing on screen to show it. The
            // rotation surfaced on the next step of zoom, the moment the view
            // narrowed enough to start drawing from the moved shift: "after a
            // certain zoom threshold the map jumps all over from right to left".
            if (!wholeMapAcross)
            {
                _xShift = xShift;
            }
        }

        return setOffsetX || setOffsetY;
    }

    public Tile Location { get; }

    public IViewElement[] Elements { get; }
    public IEnumerable<IViewElement> CurrentAnimations => _animations[_currentIndex];
    public int ViewHeight { get; }
    public int ViewWidth { get; set; }
    public float RenderScale { get; }

    public bool Finished()
    {
        return _currentIndex == _animations.Count - 1;
    }

    public void Reset()
    {
        _currentIndex = 0;
    }

    public void Next()
    {
        _currentIndex++;
    }

    public void Preserve()
    {
        _preserve = true;
    }

    public void Dispose()
    {
        if (!_preserve)
        {
            BaseImage.Unload();
        }
    }

    protected void SetAnimation(IList<IViewElement> frameSet)
    {
        _animations.Add(frameSet);
    }
}
