using RhyCiv.UI.Classic.ImageLoader;
using RhyCiv.Engine;
using RhyCiv.Engine.Diagnostics;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using Model;
using Model.Core;
using Model.Controls;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Units;
using Raylib_CSharp.Transformations;
using RaylibUI.RunGame.Commands;
using RaylibUI.RunGame.Commands.Orders;
using RaylibUI.RunGame.GameControls;
using RaylibUI.RunGame.GameControls.CityControls;
using RaylibUI.RunGame.GameControls.Mapping;
using RaylibUI.RunGame.GameControls.Menu;
using RaylibUI.RunGame.GameModes;
using RaylibUI.Initialization;
using Raylib_CSharp.Interact;

namespace RaylibUI.RunGame;

public class GameScreen : BaseScreen
{
    public const int MinimumZoom = -24;
    // Terrain is now composed at a scale that follows the zoom, so the map can go
    // well past the old 3x ceiling without being upscaled from a fixed grid.
    public const int MaximumZoom = 19;
    public Main Main { get; }
    public IGame Game { get; }
    public Sound Soundman { get; }

    private readonly MinimapPanel _minimapPanel;
    private readonly MapControl _mapControl;
    private readonly StatusPanel _statusPanel;
    private readonly LocalPlayer _player;
    private GameMenu _menu;
    private readonly IList<IGameCommand> _commands;
    private bool _ToTPanelLayout, _minimapGlobe, _showGrid;

    public IGameMode ActiveMode
    {
        get => _activeMode;
        set
        {
            if (value.Activate())
            {
                _activeMode = value;
            }
        }
    }

    public int MinimapHeight => _minimapGlobe ? MiniMapGlobeHeight : Math.Max(100, CurrentMap.YDim) + 38 + 11;
    public int MinimapWidth => _minimapGlobe ? MiniMapGlobeWidth : MiniMapNormalWidth;

    public int Zoom     // -24 (min) ... 19 (max), 0 = tiles at their authored size.
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, MinimumZoom, MaximumZoom);
            SyncTerrainDetail();
        }
    }

    /// <summary>
    /// Terrain reaches the screen as per-tile bitmaps composed ahead of time, so
    /// its detail is fixed when a tile is built rather than when it is drawn.
    /// Keeping the composition scale in step with the zoom means a zoomed-in map
    /// is composed at the size it will be drawn at instead of being stretched up
    /// from a fixed 64x32 grid, which is what limited how far the map could
    /// usefully zoom.
    /// </summary>
    public void SyncTerrainDetail()
    {
        if (_mapControl is null)
        {
            // Still constructing; the initial terrain load already picked a scale.
            return;
        }

        var active = Main.ActiveInterface;
        if (active.TileSets.Count == 0)
        {
            return;
        }

        var desired = TerrainRenderScaleFor(_zoom);
        if (active.TileSets[0].RenderScale == desired)
        {
            return;
        }

        TerrainLoader.UseTerrainScale(Main.ActiveRuleSet, active, desired);

        // Cached tiles were composed at the old scale and cannot be reused.
        TileCache.Clear();
        ForceRedraw();
    }

    /// <summary>
    /// Source pixels to compose per logical tile pixel: enough to cover the zoom
    /// and the display's own density, so nothing is ever upscaled on the way to
    /// the screen.
    /// </summary>
    private static int TerrainRenderScaleFor(int zoom)
    {
        var drawnScale = ImageUtils.ZoomScale(zoom) * DisplayScale.Factor;

        // Doubling bands rather than an exact match: rebuilding a terrain set
        // resizes every base texture and recomposes every overlay, so it must not
        // happen on each notch of the mouse wheel.
        var scale = 1;
        while (scale < drawnScale && scale < TerrainLoader.MaximumTerrainRenderScale)
        {
            scale *= 2;
        }

        return Math.Clamp(scale, 1, TerrainLoader.MaximumTerrainRenderScale);
    }
    public TileTextureCache TileCache { get; }
    
    public LocalPlayer Player => _player;

    public StatusPanel StatusPanel => _statusPanel;
    public bool ToTPanelLayout => _ToTPanelLayout;
    public bool MinimapGlobe => _minimapGlobe;
    public bool ShowGrid => _showGrid;
    public GameMenu MenuBar => _menu;
    public IGameMode Moving { get; }
    public IGameMode ViewPiece { get; }

    private const int MiniMapNormalWidth = 262;
    private const int MiniMapGlobeWidth = 134;
    private const int MiniMapGlobeHeight = 142;
    private IGameMode _activeMode = null!;
    
    private CivDialog? _currentPopupDialog;
    private Action<string,int,IList<bool>?,IDictionary<string,string>?>? _popupClicked;
    private readonly Queue<Action> _queuedPopups = new();

    private int _zoom, _width, _height;

    public event EventHandler<MapEventArgs>? OnMapEvent = null;

    public GameScreen(Main main, IGame game, Sound soundman, IDictionary<string, string?>? viewData) : base(main)
    {
        TileCache = new TileTextureCache(this);
        Main = main;
        Game = game;
        Soundman = soundman;

        if (viewData != null && viewData.TryGetValue("Zoom", out var value) && int.TryParse(value, out var zoom))
        {
            //Use the property to ensure range validation is run
            Zoom = zoom;
        }
        
        Moving = new MovingPieces(this);
        ViewPiece = new ViewPiece(this);
        Processing = new ProcessingMode(this);

        var civ = game.GetPlayerCiv;
        _player = new LocalPlayer(this, civ);
        VisibleCivId = _player.Civilization.Id;
        game.ConnectPlayer(_player);

        _ToTPanelLayout = false;

        var commands = SetupCommands(game);
        _commands = commands;
        _menu = new GameMenu(this, BuildMenus());
        _menu.GetPreferredWidth();

        if (Game.GetActiveCiv == Game.GetPlayerCiv)
        {
            ActiveMode = _player.ActiveUnit is not {MovePoints: > 0} ? ViewPiece : Moving;
        }
        else
        {
            ActiveMode = Processing;
        }
        
        _width = DisplayScale.Width;
        _height = DisplayScale.Height;
        
        var menuHeight = _menu.GetPreferredHeight();
        
        _statusPanel = new StatusPanel(this, game);
        _minimapPanel = new MinimapPanel(this, game, _player);

        _ToTPanelLayout = commands.Any(c => c.Id == CommandIds.MapLayoutToggle && c.Command is not null);   // Command for map layout change only in ToT
        _minimapGlobe = _ToTPanelLayout;
        var mapWidth = _width - MinimapWidth;
        var mapRect = new Rectangle(0, menuHeight, mapWidth, _height - menuHeight);
        if (_ToTPanelLayout)
        {
            mapWidth = _width;
            mapRect = new Rectangle(0, menuHeight + MinimapHeight, mapWidth, _height - menuHeight - MinimapHeight);
        }
        _mapControl = new MapControl(this, game, mapRect, _player);

        // The order of these is important as MapControl can overdraw so must be drawn first
        Controls.Add(_mapControl);
        Controls.Add(_menu);
        Controls.Add(_minimapPanel);
        Controls.Add(_statusPanel);

        var lookup = new Dictionary<Shortcut, IList<IGameCommand>>();
        foreach (var command in commands)
        {
            foreach (var shortcut in command.ActivationKeys)
            {
                if (shortcut.Equals(Shortcut.None)) continue;
                
                if (lookup.ContainsKey(shortcut))
                {
                        lookup[shortcut].Add(command);
                }
                else
                {
                    lookup.Add(shortcut, new List<IGameCommand> { command});
                }
            }
        }

        GameCommands = lookup;
    }

    public override int Width => _width;
    public override int Height => _height;

    private Dictionary<Shortcut, IList<IGameCommand>> GameCommands { get; }

    private void TryExecuteCommand(IList<IGameCommand> commands)
    {
        foreach (var command in commands)
        {
            command.Update();
        }

        var activeCommand = commands.MinBy(c => c.Status);

        if (activeCommand == null || activeCommand.Status == CommandStatus.Invalid) return;

        if (activeCommand.Status <= CommandStatus.Default)
        {
            SessionLog.Record($"command {activeCommand.Id}");
            activeCommand.Action();
        }
        else
        {
            ShowPopup(activeCommand.ErrorDialog, dialogImage: activeCommand.ErrorImage);
        }
    }

    public ProcessingMode Processing { get; }
    public Map CurrentMap => Player.ActiveTile.Map;
    public Tile? ViewAnchor { get; private set; }

    public void SetViewAnchor(Tile? tile)
    {
        ViewAnchor = tile;
        ForceRedraw();
    }

    /// <summary>
    /// The CivId of the currently displayed map normally the same as the player civId but can be changed via reveal map
    /// </summary>
    public int VisibleCivId { get; set; }

    public MapControl MapControl => _mapControl;

    public override void OnKeyPress(KeyboardKey key)
    {
        // Alt used to move focus into the menu bar, but it only ever opened the
        // menus and never closed them again, so the key was a one-way trip that
        // had to be undone with the mouse. The menus are reachable by clicking
        // them, which is how they are used in practice.
        var command = new Shortcut(key.ToModelKey(), Input.IsKeyDown(KeyboardKey.RightShift) ||
                                        Input.IsKeyDown(KeyboardKey.LeftShift)
            , Input.IsKeyDown(KeyboardKey.LeftControl) ||
              Input.IsKeyDown(KeyboardKey.RightControl)
        );

        if (key is KeyboardKey.Enter or KeyboardKey.KpEnter && GameCommands.ContainsKey(command))
        {
            TryExecuteCommand(GameCommands[command]);
            return;
        }

        if (!ActiveMode.HandleKeyPress(command) && GameCommands.ContainsKey(command))
        {
            TryExecuteCommand(GameCommands[command]);
        }
        
    }

    public override void InterfaceChanged(Sound man)
    {
        //Some of the initialization logic should be here.... not sure exactly what currently this shouldn't ce called
    }

    public override void Resize(int width, int height)
    {
        _width = width;
        _height = height;
        GetPanelBounds(width, height);
        base.Resize(width, height);

        // A different display density changes how many source pixels a tile needs.
        SyncTerrainDetail();
        _mapControl.RefreshResolution();
    }

    private void GetPanelBounds(int width, int height)
    {
        _menu.GetPreferredWidth();
        var menuHeight = _menu.GetPreferredHeight();
        var mapWidth = width - MinimapWidth;
        var mapControlRect = new Rectangle(0, menuHeight, mapWidth, height - menuHeight);
        var minimapRect = new Rectangle(mapWidth, menuHeight, MinimapWidth, MinimapHeight);
        var statusRect = new Rectangle(mapWidth, MinimapHeight + menuHeight, MinimapWidth, height - MinimapHeight - menuHeight);
        if (_ToTPanelLayout)
        {
            mapWidth = width;
            mapControlRect = new Rectangle(0, menuHeight + MiniMapGlobeHeight, mapWidth, height - menuHeight - MiniMapGlobeHeight);
            minimapRect = new Rectangle(mapWidth - MinimapWidth, menuHeight, MinimapWidth, MinimapHeight);
            statusRect = new Rectangle(0, menuHeight, mapWidth - MinimapWidth, MiniMapGlobeHeight);
        }
        _menu.Location = new(0, 0);
        _menu.Width = width;
        _menu.Height = height;
        _mapControl.Location = new(mapControlRect.X, mapControlRect.Y);
        _mapControl.Width = (int)mapControlRect.Width;
        _mapControl.Height = (int)mapControlRect.Height;
        _minimapPanel.Location = new(minimapRect.X, minimapRect.Y);
        _minimapPanel.Width = (int)minimapRect.Width;
        _minimapPanel.Height = (int)minimapRect.Height;
        _statusPanel.Location = new(statusRect.X, statusRect.Y);
        _statusPanel.Width = (int)statusRect.Width;
        _statusPanel.Height = (int)statusRect.Height;
    }

    /// <summary>
    /// One city's news for this turn, gathered so it arrives as a single message.
    /// </summary>
    private sealed record CityNews(City City, List<(string Dialog, IList<string>? Strings, IList<int>? Numbers)> Items);

    /// <summary>
    /// News reported since the last frame, in the order the cities were processed.
    /// </summary>
    private readonly List<CityNews> _pendingCityNews = [];

    /// <summary>
    /// Something a city has to say, held back until the turn's processing has
    /// finished so everything one city has to report arrives together.
    /// <para>
    /// A turn is processed inside a single frame, one city at a time, and each
    /// thing that happened used to put up its own message as it happened. A city
    /// that came out of disorder and finished a unit in the same turn therefore
    /// asked twice, and answering "zoom to city" on the first put the city window
    /// up with the second message still waiting behind it -- so the news arrived
    /// after the player had already looked at the city it was about.
    /// </para>
    /// </summary>
    public void ShowCityDialog(string dialog, City city, IList<string>? replaceStrings = null,
        IList<int>? replaceNumbers = null)
    {
        var news = _pendingCityNews.FirstOrDefault(entry => entry.City == city);
        if (news == null)
        {
            news = new CityNews(city, []);
            _pendingCityNews.Add(news);
        }

        news.Items.Add((dialog, replaceStrings, replaceNumbers));
    }

    /// <summary>
    /// Puts up everything the cities reported while the turn was being processed,
    /// one message per city.
    /// </summary>
    private void ReleaseCityNews()
    {
        if (_pendingCityNews.Count == 0)
        {
            return;
        }

        var pending = _pendingCityNews.ToList();
        _pendingCityNews.Clear();

        foreach (var news in pending)
        {
            ShowOneCityMessage(news);
        }
    }

    private void ShowOneCityMessage(CityNews news)
    {
        var city = news.City;
        var options = new List<string> { Labels.For(LabelIndex.ZoomToCity), Labels.For(LabelIndex.Continue) };

        void Answered(string _, int index, IList<bool>? __, IDictionary<string, string>? ___)
        {
            if (index == 0)
            {
                ShowCityWindow(city);
            }
        }

        if (news.Items.Count == 1)
        {
            // On its own it keeps its own dialog, so a single piece of news looks
            // exactly as it always has -- its own title, its own layout.
            var (dialog, strings, numbers) = news.Items[0];
            ShowPopup(dialog, handleButtonClick: Answered, replaceNumbers: numbers,
                options: options, replaceStrings: strings ?? DefaultCityStrings(city));
            return;
        }

        var sentences = news.Items
            .Select(item => CityDialogSentence(item.Dialog, item.Strings ?? DefaultCityStrings(city), item.Numbers))
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToList();

        if (sentences.Count == 0)
        {
            return;
        }

        if (!ShowPopup("CITYNEWS", handleButtonClick: Answered, options: options,
                replaceStrings: [city.Name, string.Join(" ", sentences)]))
        {
            // No combined dialog in this ruleset's text. Rather than swallow the
            // news, fall back to reporting each piece as it used to be reported.
            foreach (var (dialog, strings, numbers) in news.Items)
            {
                ShowPopup(dialog, handleButtonClick: Answered, replaceNumbers: numbers,
                    options: options, replaceStrings: strings ?? DefaultCityStrings(city));
            }
        }
    }

    private static List<string> DefaultCityStrings(City city) =>
        [city.Name, city.ItemInProduction.GetDescription(), city.Owner.Adjective, Labels.For(LabelIndex.builds)];

    /// <summary>
    /// One city message's body, as a single run of prose: the dialog's own lines
    /// from the game's text, joined and with its placeholders filled in.
    /// </summary>
    private string CityDialogSentence(string dialogName, IList<string> strings, IList<int>? numbers)
    {
        var popupBox = MainWindow.ActiveInterface.GetDialog(dialogName);
        if (popupBox?.Text is not { Count: > 0 } lines)
        {
            return string.Empty;
        }

        var body = string.Join(" ", lines
            .Select(line => line.TrimStart('^'))
            .Where(line => !string.IsNullOrWhiteSpace(line)));
        return DialogUtils.ReplacePlaceholders(body, strings, numbers) ?? string.Empty;
    }

    public CityWindow ShowCityWindow(City city, bool viewOnly = false)
    {
        SessionLog.Record($"city window for {city.Name} (size {city.Size})" +
                          (viewOnly ? " (report)" : string.Empty));
        var cityDialog = new CityWindow(this, city, viewOnly);
        ShowDialog(cityDialog, stack: viewOnly);
        return cityDialog;
    }

    public void TriggerMapEvent(MapEventArgs args)
    {
        OnMapEvent?.Invoke(this, args);
    }
    
    public bool ActivateUnits(Tile tile)
    {
        var unitsHere = tile.UnitsHere.Where(u => !u.Dead).ToList();
        if (unitsHere.Count == 0)
        {
            Game.ActivePlayer.ActiveTile = tile;
            return true;
        }

        var friendlyUnits = unitsHere.Where(u => u.Owner == _player.Civilization).ToList();
        var unit = friendlyUnits.FirstOrDefault() ?? unitsHere[0];
        if (friendlyUnits.Count > 1)
        {
            ShowUnitSelection(tile, friendlyUnits);
            return false;
        }

        return ActivateUnit(tile, unit);
    }

    private void ShowUnitSelection(Tile tile, IList<Unit> units)
    {
        var movementMultiplier = Math.Max(1, Game.Rules.Cosmic.MovementMultiplier);
        var listbox = new ListboxDefinition
        {
            Rows = Math.Min(9, units.Count),
            VerticalScrollbar = true,
            ImageShift = false,
            Groups = units.Select(unit => new ListboxGroup
            {
                Elements =
                [
                    new() { Unit = unit, Game = Game, Width = 64, ScaleIcon = 0.75f },
                    new()
                    {
                        Text = $"{unit.Name}{(unit.Veteran ? " (Veteran)" : "")}  " +
                               $"HP {unit.RemainingHitpoints}/{unit.HitpointsBase}  " +
                               $"Moves {unit.MovePoints / (decimal)movementMultiplier:0.##}/" +
                               $"{unit.MaxMovePoints / (decimal)movementMultiplier:0.##}  " +
                               $"Order: {(unit.Order == (int)OrderType.NoOrders ? "None" : ((OrderType)unit.Order).ToString())}",
                        VerticalAlignment = VerticalAlignment.Center
                    }
                ],
                Height = 40
            }).ToList()
        };

        var elements = new DialogElements
        {
            Name = "ACTIVATE_UNITS_DYNAMIC",
            Title = "Activate Unit",
            Width = 420,
            Button = [Labels.Ok, Labels.Cancel],
            Text = ["Choose the unit to activate."],
            LineStyles = [TextStyles.Left],
            Listbox = listbox
        };

        CivDialog? dialog = null;
        dialog = new CivDialog(Main, elements, (button, selectedIndex, _, _) =>
        {
            CloseDialog(dialog);
            if (button == Labels.Ok && selectedIndex >= 0 && selectedIndex < units.Count)
            {
                ActivateUnit(tile, units[selectedIndex]);
            }
        });
        ShowDialog(dialog, stack: true);
    }

    private bool ActivateUnit(Tile tile, Unit unit)
    {
        SetViewAnchor(null);

        Game.ActivePlayer.ActiveTile = tile;

        if (unit.Owner == _player.Civilization)
        {
            ClearManualSelectionOrders(unit);
            _player.WaitingList.Remove(unit);

            if (!unit.TurnEnded)
            {
                _player.ActiveUnit = unit;
                ActiveMode = Moving;
            }
            else
            {
                ActiveMode = ViewPiece;
            }
        }
        else
        {
            ActiveMode = ViewPiece;
        }

        ForceRedraw();
        return true;
    }

    private static void ClearManualSelectionOrders(Unit unit)
    {
        unit.Order = (int)OrderType.NoOrders;
        unit.WaitOrder = false;
        unit.Building = 0;
        unit.GoToX = unit.X;
        unit.GoToY = unit.Y;
        unit.GoToMapIndex = unit.MapIndex;
    }

    public void ForceRedraw()
    {
        _mapControl.ForceRedraw = true;
    }

    /// <summary>
    /// The menus for the bar, less the ones the player has not asked for. The cheat
    /// and editor menus are development tools and are off unless they have been
    /// turned on in the advanced settings.
    /// </summary>
    private IList<DropdownMenuContents> BuildMenus()
    {
        return Main.ActiveInterface.ConfigureGameCommands(_commands)
            .Where(menu => menu.Key switch
            {
                "CHEAT" => Settings.CheatMenuEnabled,
                "EDITOR" or "MAP" => Settings.EditorMenuEnabled,
                _ => true
            })
            .ToList();
    }

    /// <summary>
    /// Puts the menu bar back together, for when a setting has changed which menus
    /// belong on it. The commands themselves are the ones already in play, so
    /// anything holding one -- a keyboard shortcut, a unit's order -- keeps working
    /// across the rebuild.
    /// </summary>
    public void RebuildMenus()
    {
        var replacement = new GameMenu(this, BuildMenus());
        replacement.GetPreferredWidth();

        var position = Controls.IndexOf(_menu);
        if (position < 0)
        {
            return;
        }

        Controls[position] = replacement;
        _menu = replacement;
        Resize(Width, Height);
    }

    private IList<IGameCommand> SetupCommands(IGame game)
    {
        var commandInterface = typeof(IGameCommand);
        var improvementCommand = typeof(ImprovementOrder);
        var args = new object[] { this };
        var improvements = game.TerrainImprovements.Values;
        var commands = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t != commandInterface && commandInterface.IsAssignableFrom(t) && !t.IsAbstract &&
                        t != improvementCommand)
            .Select(t => Activator.CreateInstance(t, args: args)).OfType<IGameCommand>()
            .Concat(improvements.Select(i => new ImprovementOrder(i, this, game))).ToList();

        return commands;
    }
    
    

    public void QueueAfterCurrentPopup(Action action)
    {
        _queuedPopups.Enqueue(action);
    }

    /// <summary>
    /// Puts a dialog from the game's text up on screen, and reports whether it
    /// went up -- or was queued behind one that is already there, which comes to
    /// the same thing for a caller waiting on an answer. False means the dialog is
    /// not in the game's text at all and no answer is ever coming, which a caller
    /// that has set itself to wait for one needs to know.
    /// </summary>
    public bool ShowPopup(string dialogName,
        Action<string, int, IList<bool>?, IDictionary<string, string>?>? handleButtonClick = null,
        IList<int>? replaceNumbers = null,
        IList<string>? replaceStrings = null,
        IList<bool>? checkboxStates = null,
        List<string>? options = null,
        List<TextBoxDefinition>? textBoxes = null,
        DialogImageElements? dialogImage = null,
        ListboxDefinition? listBox = null,
        IList<string>? buttons = null)
    {
        SessionLog.Record($"popup {dialogName}");

        // Queue behind a message already up, and behind anything the map is still
        // playing out. Deliberately not behind an open window: a dialog raised while
        // the city window is open is one the player has just asked for -- the price
        // of buying production, say -- and holding that back until they closed the
        // window would look like the button had done nothing.
        if (_currentPopupDialog != null || _mapControl?.IsPlayingBack == true)
        {
            _queuedPopups.Enqueue(() => ShowPopup(dialogName, handleButtonClick, replaceNumbers, replaceStrings,
                checkboxStates, options, textBoxes, dialogImage, listBox, buttons));
            return true;
        }

        var popupBox = MainWindow.ActiveInterface.GetDialog(dialogName);
        if (popupBox != null)
        {
            var dialog = new DialogElements(popupBox);
            if (buttons is { Count: > 0 })
            {
                // A fresh list rather than the caller's, and never the one the
                // cached dialog definition holds: writing to that would change the
                // game's own copy of the dialog for every later use of it.
                dialog.Button = buttons.ToList();
            }
            if (options != null)
            {
                dialog.Options = new()
                {
                    Texts = options
                };
            }
            if (checkboxStates != null)
            {
                dialog.Options ??= new OptionsDefinition();
                dialog.Options.CheckboxStates = checkboxStates;
            }
            if (replaceNumbers != null)
            {
                dialog.ReplaceNumbers = replaceNumbers;
            }
            if (replaceStrings != null)
            {
                dialog.ReplaceStrings = replaceStrings;
            }
            if (textBoxes != null)
            {
                dialog.TextBoxes = textBoxes;
            }
            if (dialogImage != null)
            {
                dialog.Image = dialogImage;
            }
            if (listBox != null)
            {
                dialog.Listbox = listBox;
            }
            _popupClicked = handleButtonClick;
            _currentPopupDialog = new CivDialog(MainWindow, dialog, ClosePopup);
            ShowDialog(_currentPopupDialog, stack: true);
            return true;
        }

        // Nothing to show, and the caller's handler will never run. This is a
        // packaging fault rather than a game state -- the code asked for a dialog by
        // a name the game's text does not define -- and it has hidden whole features
        // more than once: the Diplomat shipped for two releases doing nothing at all
        // because every dialog it asked for was missing. Say so rather than
        // returning quietly.
        SessionLog.Record($"MISSING DIALOG: {dialogName} is not in the game's text");
        Console.Error.WriteLine(
            $"rhYciv: dialog '{dialogName}' is not defined in the ruleset's text; nothing was shown.");
        return false;
    }

    private void ClosePopup(string arg1, int arg2, IList<bool>? arg3, IDictionary<string, string>? arg4)
    {
        // The answer, not just the question. A record that ends at "popup RESEARCH"
        // cannot say whether the player pressed OK or Info, and those run entirely
        // different code -- which is the difference between a reproducible report
        // and a guess.
        SessionLog.Record($"popup answered: {arg1} (selection {arg2})");

        var closedPopup = _currentPopupDialog;
        var popupClicked = _popupClicked;

        _currentPopupDialog = null;
        _popupClicked = null;

        if (closedPopup != null)
        {
            CloseDialog(closedPopup);
        }

        popupClicked?.Invoke(arg1, arg2, arg3, arg4);
    }

    /// <summary>
    /// Whether there is nothing in the player's way: no window open, and nothing
    /// still being played out on the map.
    /// </summary>
    private bool ScreenIsClear => _currentPopupDialog == null && !HasOpenDialog && !_mapControl.IsPlayingBack;

    /// <summary>
    /// Lets the next message through, one at a time, and only once the player has
    /// finished with whatever is in front of them.
    /// <para>
    /// Messages used to be released the moment the one before it closed, without
    /// looking at what that message had opened. Answering "zoom to city" on a
    /// disorder report put the city window up and the next report immediately on
    /// top of it, so the city you had asked to look at was covered before you could
    /// look at it. They also arrived over the top of a battle or a move that was
    /// still being shown, which is the half of the turn worth watching.
    /// </para>
    /// </summary>
    private void ReleaseQueuedPopup()
    {
        if (!ScreenIsClear || _queuedPopups.Count == 0)
        {
            return;
        }

        _queuedPopups.Dequeue()();
    }

    public override void Draw(bool pulse)
    {
        // The turn is processed inside a single frame, so by the time a frame is
        // drawn every city has finished reporting and the news can be grouped.
        ReleaseCityNews();
        ReleaseQueuedPopup();
        base.Draw(pulse);
    }

    public void ToggleMapLayout()
    {
        _ToTPanelLayout = !_ToTPanelLayout;
        _minimapGlobe = _ToTPanelLayout;
        Resize(DisplayScale.Width, DisplayScale.Height);
    }

    public void RemoveGlobe()
    {
        _minimapGlobe = false;
        Resize(DisplayScale.Width, DisplayScale.Height);
    }

    public void ShowGlobe()
    {
        _minimapGlobe = true;
        Resize(DisplayScale.Width, DisplayScale.Height);
    }

    public void ShowMapGrid()
    {
        _showGrid = !_showGrid;
        ForceRedraw();
    }

    public void TurnStarting(int turnNumber)
    {
        _statusPanel.Update();
    }
}
