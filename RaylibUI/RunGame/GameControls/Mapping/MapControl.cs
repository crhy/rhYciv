using System.Numerics;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using Raylib_CSharp.Transformations;
using Raylib_CSharp.Textures;
using RaylibUI.BasicTypes.Controls;
using RaylibUI.RunGame.GameControls.Mapping.Views;
using RaylibUI.RunGame.GameModes;
using Model;
using Model.Core;
using Model.Core.Mapping;
using Model.Interface;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Fonts;
using Raylib_CSharp.Interact;
using RaylibUI.Controls;
using Raylib_CSharp.Collision;
using Path = RhyCiv.Engine.Units.Path;

namespace RaylibUI.RunGame.GameControls.Mapping;

public class MapControl : BaseControl
{
    public override bool CanFocus => true;
    private readonly GameScreen _gameScreen;
    private readonly IGame _game;
    private Texture2D? _backgroundImage;
    private int _viewWidth,_viewHeight;
    private Padding _padding;
    private HeaderLabel? _headerLabel;
    private IUserInterface _active;
    private Button _zoomInButton, _zoomOutButton;
    private float _zoomBtnScale;
    private bool _middlePanning, _middleMoved, _middleReset;
    private Vector2 _middleDrag;
    private readonly List<CityData> _cityDetails = new();
    private PathPreviewKey? _pathPreviewKey;
    private Path? _pathPreview;

    private readonly Queue<IGameView> _animationQueue = new();
    private IGameView _currentView;
    
    public MapControl(GameScreen gameScreen, IGame game, Rectangle initialBounds, LocalPlayer player) : base(gameScreen)
    {
        Location = new(initialBounds.X, initialBounds.Y);
        Width = (int)initialBounds.Width;
        Height = (int)initialBounds.Height;
        _currentBounds = initialBounds;
        _gameScreen = gameScreen;
        _game = game;
        _active = gameScreen.MainWindow.ActiveInterface;
        
        _headerLabel = new HeaderLabel(gameScreen, _active.Look, $"{_game.GetPlayerCiv.Adjective} {Labels.For(LabelIndex.Map)}", 
            fontSize: _active.Look.HeaderLabelFontSizeNormal);

        _padding = _active.GetPadding(_headerLabel?.TextSize.Y ?? 0, false);

        _zoomBtnScale = _padding.Top > 30 ? 1.4f : 1.0f;   // MGE=1.4f, ToT=1.0f
        _zoomInButton = new Button(Controller, String.Empty, backgroundImage: _active.PicSources["zoomIn"][0], imageScale: _zoomBtnScale);
        _zoomOutButton = new Button(Controller, String.Empty, backgroundImage: _active.PicSources["zoomOut"][0], imageScale: _zoomBtnScale);
        _zoomInButton.Click += (_, _) =>
        {
            if (_gameScreen.Zoom < GameScreen.MaximumZoom)
                _gameScreen.TriggerMapEvent(new MapEventArgs(MapEventType.ZoomChange) { Zoom = _gameScreen.Zoom + 1 });
        };
        _zoomOutButton.Click += (_, _) =>
        {
            if (_gameScreen.Zoom > GameScreen.MinimumZoom)
                _gameScreen.TriggerMapEvent(new MapEventArgs(MapEventType.ZoomChange) { Zoom = _gameScreen.Zoom - 1 });
        };
        SetDimensions();
        Controls = [_headerLabel, _zoomInButton, _zoomOutButton];

        _currentView =
            _gameScreen.ActiveMode.GetDefaultView(gameScreen, null, _viewHeight, _viewWidth, ForceRedraw);

        gameScreen.OnMapEvent += MapEventTriggered;
        player.OnUnitEvent += UnitEventTriggered;
        Click += OnClick;
        MouseDown += OnMouseDown;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        var tile = GetTileAtMousePosition();
        if(tile == null) return;
        _gameScreen.ActiveMode.MouseDown(tile);
    }

    /// <summary>
    /// How many moves may be waiting to be watched at once.
    /// <para>
    /// Every move the player can see is composed into a small film the moment the
    /// engine reports it, and every rival civilisation now plays its whole turn on
    /// one press of End Turn. Without a limit, a turn in which a dozen visible
    /// units marched past would build a dozen films before the frame ended, and
    /// then hold the player watching them for several seconds. Past this many the
    /// move is simply not animated: the map is redrawn instead, so the units are
    /// still where they should be.
    /// </para>
    /// </summary>
    private const int MaxQueuedAnimations = 12;

    private void UnitEventTriggered(object sender, UnitEventArgs e)
    {
        switch (e.EventType)
        {
            // Unit movement animation event was raised
            case UnitEventType.MoveCommand:
            {
                if (e is MovementEventArgs mo)
                {
                    if (_animationQueue.Count >= MaxQueuedAnimations)
                    {
                        _gameScreen.ForceRedraw();
                        break;
                    }

                    _animationQueue.Enqueue(new MoveAnimation(_gameScreen, mo, _animationQueue.LastOrDefault(_currentView), _viewHeight, _viewWidth, ForceRedraw));
                }

                break;
            }
            case UnitEventType.Attack:
            {
                if (e is CombatEventArgs combatEventArgs)
                {
                    if (_animationQueue.Count >= MaxQueuedAnimations)
                    {
                        _gameScreen.ForceRedraw();
                        break;
                    }

                    _animationQueue.Enqueue(new AttackAnimation(_gameScreen, combatEventArgs, _animationQueue.LastOrDefault(_currentView), _viewHeight, _viewWidth, ForceRedraw));
                }
                break;
            }
            case UnitEventType.NewUnitActivated:
            {
                _gameScreen.SetViewAnchor(null);
                //animType = AnimationType.Waiting;
                //if (IsActiveSquareOutsideMapView) MapViewChange(Map.ActiveXY);
                //UpdateMap();
                break;
            }
        }
    }

    public override void OnResize()
    {
        if (Bounds.Equals(_currentBounds)) return;
        _currentBounds = Bounds;
        base.OnResize();

        SetDimensions();
        NextView();
        //ShowTile(_selectedTile);
    }

    private void SetDimensions()
    {
        _headerLabel.Visible = !_gameScreen.ToTPanelLayout;
        _zoomInButton.Visible = !_gameScreen.ToTPanelLayout;
        _zoomOutButton.Visible = !_gameScreen.ToTPanelLayout;

        _padding = _gameScreen.ToTPanelLayout ?
            _active.GetPadding(0, false) :
            _active.GetPadding(_headerLabel.TextSize.Y, false);

        _backgroundImage?.Unload();
        _backgroundImage = ImageUtils.PaintDialogBase(_active, Width, Height, _padding, noWallpaper:true);

        if (!_gameScreen.ToTPanelLayout)
        {
            _headerLabel.Location = new(100, 0);
            _headerLabel.Width = Width - 200;
            _headerLabel.Height = _padding.Top;
            _zoomInButton.Location = new(11, 7);
            _zoomInButton.Width = _zoomInButton.GetPreferredWidth();
            _zoomInButton.Height = _zoomInButton.GetPreferredHeight();
            _zoomOutButton.Location = new(11 + _zoomInButton.Width + 2, 7);
            _zoomOutButton.Width = _zoomOutButton.GetPreferredWidth();
            _zoomOutButton.Height = _zoomOutButton.GetPreferredHeight();
        }

        _viewWidth = Width - _padding.Left - _padding.Right;
        _viewHeight = Height - _padding.Top - _padding.Bottom;
    }

    public void RefreshResolution()
    {
        if (Math.Abs(_currentView.RenderScale - DisplayScale.Factor) <= 0.001f)
        {
            return;
        }

        ForceRedraw = true;
        NextView();
    }

    

    private void OnClick(object? sender, MouseEventArgs mouseEventArgs)
    {
        try
        {
            _gameScreen.Focused = this;
            var tile = GetTileAtMousePosition();
            if (tile == null)
            {
                return;
            }

            _gameScreen.SetViewAnchor(null);

            if (_gameScreen.ActiveMode.MapClicked(tile, mouseEventArgs.Button))
            {
                _gameScreen.ForceRedraw();
                MapViewChange(tile);
            }
        }
        finally
        {
            _gameScreen.ActiveMode.MouseClear();
        }
    }

    private Tile? GetTileAtMousePosition()
    {
        var clickPosition = GetRelativeMousePosition();
        if (clickPosition.X < _padding.Left + _padding.Right || clickPosition.X > _viewWidth + _padding.Left + _padding.Right || clickPosition.Y < _padding.Top || clickPosition.Y > _padding.Top + _viewHeight)
        {
            return null;
        }

        var map = _gameScreen.CurrentMap;
        var dim = _gameScreen.TileCache.GetDimensions(map, _gameScreen.Zoom);
        var clickedTilePosition = clickPosition - new Vector2(_padding.Left, _padding.Top) + _currentView.Offsets;
        var y = Math.DivRem((int)clickedTilePosition.Y, dim.HalfHeight, out var yRemainder);
        var odd = y % 2 == 1;
        var clickX = (int)(odd ? clickedTilePosition.X - dim.HalfWidth : clickedTilePosition.X);
        if (clickX < 0)
        {
            if (map.Flat)
            {
                clickX = 0;
            }
            else
            {
                clickX += dim.TotalWidth;
            }
        }
        else if (clickX > dim.TotalWidth)
        {
            if (map.Flat)
            {
                clickX = dim.TotalWidth - 1;
            }
            else
            {
                clickX -= dim.TotalWidth;
            }
        }

        var x = Math.DivRem(clickX, dim.TileWidth, out var xRemainder);

        if (xRemainder < dim.HalfWidth && y > 0)
        {
            if (yRemainder *  dim.HalfWidth + xRemainder *  dim.HalfHeight < dim.DiagonalCut)
            {
                y -= 1;
                if (!odd)
                {
                    x -= 1;
                    if (x < 0)
                    {
                        x = map.Flat ? 0 : map.Tile.GetLength(0) - 1;
                    }
                }
            }
        }
        else if (xRemainder > dim.HalfWidth)
        {
            if ((dim.TileWidth - xRemainder) *  dim.HalfHeight + yRemainder *  dim.HalfWidth < dim.DiagonalCut)
            {
                y -= 1;
                if (odd)
                {
                    x += 1;
                    if (x == map.Tile.GetLength(0))
                    {
                        if (map.Flat)
                        {
                            x -= 1;
                        }
                        else
                        {
                            x = 0;
                        }
                    }
                }
            }
        }

        if (0 <= y && y < map.Tile.GetLength(1))
        {
            x = Utils.WrapNumber(2 * x + _currentView.Xshift, 2 * map.XDim) / 2;
            return map.Tile[x, y];
        }

        return null;
    }

    private void MapViewChange(Tile tile)
    {
        if(_currentView.IsDefault && _currentView.Location != tile)
        {
            NextView();
        }
    }

    private void MapEventTriggered(object sender, MapEventArgs e)
    {
        switch (e.EventType)
        {
            case MapEventType.MinimapViewChanged:
                {
                    _gameScreen.SetViewAnchor(null);
                    ForceRedraw = true;
                    if (_currentView.IsDefault)
                    {
                        if (_gameScreen.ActiveMode != _gameScreen.ViewPiece)
                        {
                            _gameScreen.ActiveMode = _gameScreen.ViewPiece;
                        }

                        if (_gameScreen.Player.ActiveTile != _currentView.Location)
                        {
                            MapViewChange(_gameScreen.Player.ActiveTile);
                        }
                    }

                    break;
                }
            case MapEventType.ZoomChange:
                {
                    _gameScreen.Zoom = e.Zoom;
                    _gameScreen.ForceRedraw();
                    NextView();
                }
                break;
            default: break;
        }
    }

    // City name and population size on the map. The number is deliberately close to
    // the name rather than two thirds of it. Sized to be read at 1080p rather than
    // to match the tiles: a label is an annotation, not part of the scene, and one
    // that keeps pace with the zoom ends up wider than the city it names -- at a
    // close zoom "Carthago" was drawn three times the width of Carthage.
    //
    // So they grow only over the first few steps of zoom, where the tiles are small
    // enough that a fixed label would crowd them, and hold from there.
    private const int CityNameFontBase = 28;
    private const int CitySizeFontBase = 23;
    private const int MapLabelZoomCap = 2;

    private Rectangle _currentBounds;

    private DateTime _animationStart;
    public override void Draw(bool pulse)
    {
        if (_middlePanning && !Input.IsMouseButtonDown(MouseButton.Middle))
        {
            _middlePanning = false;
            _middleReset = false;
        }

        if (!Input.IsMouseButtonDown(MouseButton.Left))
        {
            _gameScreen.ActiveMode.MouseClear();
        }

        // A redraw that has been asked for is done at once rather than on the
        // animation clock. The static view of the map ticks once every two seconds,
        // so anything that changed it other than the player's own move -- ground
        // coming into view, a city founded, a unit lost, the grid switched on --
        // could sit unseen for that long. Waiting to be shown what you have just
        // done is most of what a laggy game feels like. A move being played out is
        // left alone: it is short, and cutting it off mid-step looks worse than the
        // wait it saves.
        var redrawNow = _forceRedraw && _currentView.IsDefault;
        if (redrawNow || _animationStart.AddMilliseconds(_currentView.Interval) < DateTime.Now)
        {
            if (redrawNow || _currentView.Finished())
            {
                NextView();
            }
            else
            {
                _currentView.Next();
            }

            _animationStart = DateTime.Now;
        }

        var paddedLoc = new Vector2(Location.X + _padding.Left, Location.Y + _padding.Top);
        Graphics.DrawTextureEx(_currentView.BaseImage, paddedLoc, 0f, 1f / _currentView.RenderScale,
            Color.White);

        _cityDetails.Clear();

        var zoom = _gameScreen.Zoom;
        foreach (var element in _currentView.Elements)
        {
            if (element is CityData data)
            {
                element.Draw(element.Location + paddedLoc, scale: ImageUtils.ZoomScale(zoom));
                _cityDetails.Add(data);

                // The population box is drawn with the name below, so the two can be
                // laid out as one label rather than colliding.
            }
            else if (element.IsTerrain || !_currentView.ActionTiles.Contains(element.Tile) || element.Tile.IsCityPresent)
            {
                element.Draw(element.Location + paddedLoc, isShaded: element.IsShaded, scale: ImageUtils.ZoomScale(zoom));
            }
        }

        foreach (var cityData in _cityDetails)
        {
            var name = cityData.Name;
            var fontSize = Math.Clamp(CityNameFontBase.ZoomScale(zoom),
                TextRendering.MinimumMapFontSize,
                CityNameFontBase.ZoomScale(Math.Clamp(zoom, 0, MapLabelZoomCap)));
            var textSize = TextRendering.Measure(_active.Look.DefaultFont, name, fontSize, 1);

            var size = cityData.Size.ToString();
            var sizeFontSize = Math.Clamp(CitySizeFontBase.ZoomScale(zoom),
                TextRendering.MinimumFittedFontSize,
                CitySizeFontBase.ZoomScale(Math.Clamp(zoom, 0, MapLabelZoomCap)));
            var sizeTextSize = TextRendering.Measure(Fonts.TnRbold, size, sizeFontSize, 0);
            var boxPadding = Math.Max(2f, sizeFontSize * 0.22f);
            var boxSize = sizeTextSize + new Vector2(boxPadding * 2f, 0f);
            var gap = Math.Max(2f, sizeFontSize * 0.25f);

            // Name and population are laid out as one label and centred together on
            // the city's logical footprint, rather than the number being dropped at a
            // fixed offset that landed on top of the name once both grew with zoom.
            var totalWidth = textSize.X + gap + boxSize.X;
            var anchor = paddedLoc + cityData.Location +
                         new Vector2(cityData.LogicalSize.X.ZoomScale(zoom) / 2f,
                             cityData.LogicalSize.Y.ZoomScale(zoom));
            var textPosition = anchor - new Vector2(totalWidth / 2f, textSize.Y / 2f);

            global::RaylibUI.TextRendering.DrawWithShadow(_active.Look.DefaultFont, name, textPosition, fontSize, 1, cityData.Color.TextColour, Color.Black, new Vector2(1, 1));

            var boxLocation = new Vector2(textPosition.X + textSize.X + gap,
                textPosition.Y + (textSize.Y - boxSize.Y) / 2f);
            Graphics.DrawRectangle((int)boxLocation.X, (int)boxLocation.Y, (int)boxSize.X, (int)boxSize.Y,
                cityData.Color.TextColour);
            Graphics.DrawRectangleLines((int)boxLocation.X - 1, (int)boxLocation.Y - 1,
                (int)boxSize.X + 2, (int)boxSize.Y + 2, Color.Black);
            global::RaylibUI.TextRendering.Draw(Fonts.TnRbold, size,
                boxLocation + new Vector2(boxPadding, 0f), sizeFontSize, 0, Color.Black);
        }

        foreach (var animation in _currentView.CurrentAnimations)
        {
            animation.Draw(animation.Location + paddedLoc, scale: ImageUtils.ZoomScale(zoom));
        }

        DrawPathPreview(paddedLoc);

        if (_backgroundImage != null)
            Graphics.DrawTextureEx(_backgroundImage.Value, Location, 0f, 1f, Color.White);

        base.Draw(pulse);
        DrawQuickInfo();
    }

    /// <summary>
    /// Whether the map still has movement or a battle to play out. A message that
    /// arrives while an enemy is crossing the screen waits for it: the point of
    /// showing the move at all is that the player gets to see what happened before
    /// being told about it.
    /// </summary>
    public bool IsPlayingBack => _animationQueue.Count > 0 || !_currentView.IsDefault;

    public override bool OnMouseWheel(float amount)
    {
        if (!IsControlDown())
        {
            return false;
        }

        var nextZoom = Math.Clamp(_gameScreen.Zoom + (amount > 0 ? 1 : -1),
            GameScreen.MinimumZoom, GameScreen.MaximumZoom);
        if (nextZoom != _gameScreen.Zoom)
        {
            _gameScreen.TriggerMapEvent(new MapEventArgs(MapEventType.ZoomChange) { Zoom = nextZoom });
        }
        return true;
    }

    public override void OnMouseMove(Vector2 moveAmount)
    {
        base.OnMouseMove(moveAmount);

        if (Input.IsMouseButtonPressed(MouseButton.Middle))
        {
            _middleReset = IsControlDown();
            _middlePanning = !_middleReset && GetTileAtMousePosition() != null;
            _middleMoved = false;
            _middleDrag = Vector2.Zero;
            if (_middleReset)
            {
                _gameScreen.TriggerMapEvent(new MapEventArgs(MapEventType.ZoomChange) { Zoom = 0 });
            }
        }

        if (_middlePanning && Input.IsMouseButtonDown(MouseButton.Middle))
        {
            _middleDrag += moveAmount;
            PanByAccumulatedDrag();
        }

        if (Input.IsMouseButtonReleased(MouseButton.Middle))
        {
            if (_middlePanning && !_middleMoved && GetTileAtMousePosition() is { } tile)
            {
                _gameScreen.SetViewAnchor(tile);
                NextView();
            }
            _middlePanning = false;
            _middleReset = false;
        }
    }

    private void PanByAccumulatedDrag()
    {
        var map = _gameScreen.CurrentMap;
        var dimensions = _gameScreen.TileCache.GetDimensions(map, _gameScreen.Zoom);
        var columnStep = (int)(_middleDrag.X / Math.Max(1, dimensions.TileWidth));
        var rowStep = (int)(_middleDrag.Y / Math.Max(1, dimensions.HalfHeight));
        if (columnStep == 0 && rowStep == 0)
        {
            return;
        }

        var anchor = _gameScreen.ViewAnchor ?? _currentView.Location;
        var column = anchor.XIndex - columnStep;
        column = map.Flat
            ? Math.Clamp(column, 0, map.XDim - 1)
            : Utils.WrapNumber(column, map.XDim);
        var row = Math.Clamp(anchor.Y - rowStep, 0, map.YDim - 1);
        var nextAnchor = map.Tile[column, row];

        _middleDrag.X -= columnStep * dimensions.TileWidth;
        _middleDrag.Y -= rowStep * dimensions.HalfHeight;
        _middleMoved = true;
        _gameScreen.SetViewAnchor(nextAnchor);
        NextView();
    }

    private void DrawQuickInfo()
    {
        if (!IsControlDown() || GetTileAtMousePosition() is not { } tile)
        {
            return;
        }

        var lines = new List<string> { tile.Name };
        if (tile.CityHere is { } city)
        {
            lines.Add($"{city.Name} — size {city.Size}");
            lines.Add($"Food {city.SurplusHunger:+#;-#;0}  Shields {city.Production}  Trade {city.Trade}");
        }
        else if (tile.UnitsHere.FirstOrDefault(unit => !unit.Dead) is { } unit)
        {
            lines.Add($"{unit.Owner.Adjective} {unit.Name}{(unit.Veteran ? " (Veteran)" : "")}");
            lines.Add($"HP {unit.RemainingHitpoints}/{unit.HitpointsBase}  Moves {unit.MovePoints}/{unit.MaxMovePoints}");
        }

        var fontSize = 16;
        var lineHeight = 20;
        var width = lines.Max(line => TextRendering.Measure(_active.Look.DefaultFont, line, fontSize, 1).X) + 18;
        var mouse = Input.GetMousePosition();
        var x = Math.Min(mouse.X + 14, DisplayScale.Width - width - 6);
        var y = Math.Min(mouse.Y + 14, DisplayScale.Height - lines.Count * lineHeight - 12);
        Graphics.DrawRectangle((int)x, (int)y, (int)width, lines.Count * lineHeight + 8,
            new Color(255, 255, 224, 245));
        Graphics.DrawRectangleLines((int)x, (int)y, (int)width, lines.Count * lineHeight + 8, Color.Black);
        for (var i = 0; i < lines.Count; i++)
        {
            global::RaylibUI.TextRendering.Draw(_active.Look.DefaultFont, lines[i],
                new Vector2(x + 8, y + 4 + i * lineHeight), fontSize, 1, Color.Black);
        }
    }

    private void DrawPathPreview(Vector2 paddedLocation)
    {
        // Shift shows a route on demand; a Go To press that has been held long
        // enough shows the route it is about to take, so the player can see where
        // the unit will go before letting go of the button.
        var armed = _gameScreen.ActiveMode == _gameScreen.Moving &&
                    _gameScreen.Moving is MovingPieces { GotoArmed: true };
        if ((!IsShiftDown() && !armed) || GetTileAtMousePosition() is not { } destination)
        {
            return;
        }

        if (_gameScreen.ActiveMode == _gameScreen.Moving && _gameScreen.Player.ActiveUnit is { } unit)
        {
            if (destination == unit.CurrentLocation)
            {
                return;
            }

            var unitPath = GetPreviewPath(new PathPreviewKey(unit.CurrentLocation, destination, unit.Domain,
                    unit.MaxMovePoints, unit.Owner.Id, unit.Alpine, unit.IgnoreZonesOfControl, true),
                () => Path.CalculatePathBetween(_gameScreen.Game, unit.CurrentLocation, destination,
                    unit.Domain, unit.MaxMovePoints, unit.Owner, unit.Alpine, unit.IgnoreZonesOfControl));
            DrawPath(unit.CurrentLocation, unitPath, paddedLocation, Color.White);
            return;
        }

        if (_gameScreen.Player.ActiveTile.CityHere is not { } city)
        {
            return;
        }

        if (destination == city.Location)
        {
            foreach (var route in city.TradeRoutes.Take(3))
            {
                if (route.Destination < 0 || route.Destination >= _gameScreen.Game.AllCities.Count)
                {
                    continue;
                }

                var partner = _gameScreen.Game.AllCities[route.Destination];
                if (partner.Location.Map != city.Location.Map)
                {
                    continue;
                }

                var tradePath = Path.CalculatePathBetween(_gameScreen.Game, city.Location, partner.Location,
                    UnitGas.Ground, _gameScreen.Game.Rules.Cosmic.MovementMultiplier, city.Owner,
                    alpine: false, ignoreZoc: true, mustBeVisible: false);
                DrawPath(city.Location, tradePath, paddedLocation, new Color(255, 223, 79, 255));
            }
            return;
        }

        var roadPath = GetPreviewPath(new PathPreviewKey(city.Location, destination, UnitGas.Ground,
                _gameScreen.Game.Rules.Cosmic.MovementMultiplier, city.Owner.Id, false, true, true),
            () => Path.CalculatePathBetween(_gameScreen.Game, city.Location, destination,
                UnitGas.Ground, _gameScreen.Game.Rules.Cosmic.MovementMultiplier, city.Owner,
                alpine: false, ignoreZoc: true));
        DrawPath(city.Location, roadPath, paddedLocation, new Color(79, 223, 255, 255));
    }

    private Path? GetPreviewPath(PathPreviewKey key, Func<Path?> factory)
    {
        if (_pathPreviewKey != key)
        {
            _pathPreviewKey = key;
            _pathPreview = factory();
        }
        return _pathPreview;
    }

    private void DrawPath(Tile start, Path? path, Vector2 paddedLocation, Color color)
    {
        if (path == null)
        {
            return;
        }

        var dimensions = _gameScreen.TileCache.GetDimensions(_gameScreen.CurrentMap, _gameScreen.Zoom);
        var previous = TileCenter(start, dimensions) + paddedLocation;
        foreach (var tile in path.Tiles)
        {
            var next = TileCenter(tile, dimensions) + paddedLocation;
            Graphics.DrawLineEx(previous, next, 3f, Color.Black);
            Graphics.DrawLineEx(previous, next, 1f, color);
            previous = next;
        }
    }

    private Vector2 TileCenter(Tile tile, MapDimensions dimensions)
    {
        var column = Utils.WrapNumber(tile.XIndex - _currentView.Xshift / 2, tile.Map.XDim);
        return new Vector2(
            column * dimensions.TileWidth + tile.Odd * dimensions.HalfWidth + dimensions.HalfWidth,
            tile.Y * dimensions.HalfHeight + dimensions.HalfHeight) - _currentView.Offsets;
    }

    private static bool IsControlDown() =>
        Input.IsKeyDown(KeyboardKey.LeftControl) || Input.IsKeyDown(KeyboardKey.RightControl);

    private static bool IsShiftDown() =>
        Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift);

    private void NextView()
    {
        _pathPreviewKey = null;
        _pathPreview = null;

        IGameView nextView;
        if (_animationQueue.Count > 0)
        {
            // An animation was composed before this redraw was asked for, so it
            // carries the old terrain. Taking it here used to drop the request on
            // the floor -- reading ForceRedraw clears it -- which left the base
            // image at the previous zoom while the units and cities drawn over it
            // scaled to the new one. Hand the request on to the next view instead.
            nextView = _animationQueue.Dequeue();
        }
        else
        {
            var force = ForceRedraw;
            nextView = _gameScreen.ViewAnchor is { } anchor
                ? new StaticView(_gameScreen, _currentView, _viewHeight, _viewWidth, force, anchor)
                : _gameScreen.ActiveMode.GetDefaultView(_gameScreen, _currentView, _viewHeight, _viewWidth, force);
        }

        if (nextView != _currentView)
        {
            _currentView.Dispose();
            _currentView = nextView;
        }
    }

    private readonly record struct PathPreviewKey(Tile Start, Tile Destination, UnitGas Domain,
        int Movement, int OwnerId, bool Alpine, bool IgnoreZoc, bool MustBeVisible);

    private bool _forceRedraw;

    internal bool ForceRedraw
    {
        private get
        {
            var s = _forceRedraw;
            _forceRedraw = false;
            return s;
        }
        set => _forceRedraw = value;
    }
}
