using RaylibUtils;
using RhyCiv.UI.Classic.Dialogs;
using RhyCiv.UI.Classic.Dialogs.NewGame;
using RhyCiv.UI.Classic.Menu;
using RhyCiv.UI.Classic.Rules;
using RhyCiv.Engine;
using RhyCiv.Engine.NewGame;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using Model;
using Model.Constants;
using Model.Controls;
using Model.Core;
using Model.Core.Advances;
using Model.Images;
using Model.ImageSets;
using Model.Input;
using Model.InterfaceActions;
using Model.Utils;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Images;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Transformations;
using System.Numerics;
using System.Text.RegularExpressions;
using Model.Controls.Civilopedia;
using Model.Core.Cities;
using Model.Core.GameRules;

namespace RhyCiv.UI.Classic;

public abstract class ClassicInterface(IMain main) : IUserInterface
{
    public virtual bool CanDisplay(string? title)
    {
        return title != null && title.Contains(Title);
    }

    public abstract InterfaceStyle Look { get; }

    public abstract string Title { get; }

    public virtual void Initialize()
    {
        Dialogs = PopupBoxReader.LoadPopupBoxes(MainApp.ActiveRuleSet.Paths, "game.txt");
        Labels.UpdateLabels(MainApp.ActiveRuleSet);
        CivilopediaLoader.UpdateMapping(MainApp.ActiveRuleSet);
        BuiltInDialogs.AddFallbacks(Dialogs);
        //foreach (var value in Dialogs.Values)
        //{
        //    value.Width = (int)(value.Width * 1.5m); // update this in CivDialog class so that you don't skip advisor, scenario and other popups
        //}
        var handlerInterface = typeof(ICivDialogHandler);
        var handlers = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t != handlerInterface && handlerInterface.IsAssignableFrom(t) && !t.IsAbstract)
            .Select(Activator.CreateInstance)
            .OfType<ICivDialogHandler>()
            .ToArray();

        foreach (var handler in handlers)
        {
            Dialogs.TryAdd(handler.Name, BuiltInDialogs.Generic(handler.Name));
        }

        DialogHandlers = handlers.Select(h => h.UpdatePopupData(Dialogs))
            .ToDictionary(k => k.Name);
    }

    protected Dictionary<string, ICivDialogHandler> DialogHandlers { get; private set; }

    public IInterfaceAction ProcessDialog(string dialogName, DialogResult dialogResult)
    {
        if (!DialogHandlers.ContainsKey(dialogName))
        {
            throw new NotImplementedException(dialogName);
        }

        return DialogHandlers[dialogName].HandleDialogResult(dialogResult, DialogHandlers, this);
    }

    public abstract string InitialMenu { get; }

    public IInterfaceAction GetInitialAction()
    {
        return DialogHandlers[InitialMenu].Show(this);
    }

    public IImageSource? ScenTitleImage { get; set; } = null;
    
    public int GetCityStyleIndexFromEpoch(int cityStyle, int epoch)
    {
        int style = cityStyle;
        if (epoch == (int)EpochType.Industrial)
        {
            style = 4;
        }
        else if (epoch == (int)EpochType.Modern)
        {
            style = 5;
        }
        return style;
    }

    public int GetCityIndexForStyle(int cityStyleIndex, City city, int citySize)
    {
        var index = cityStyleIndex switch
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

        if (index < 3 && city.Improvements.Any(i => i.Effects.ContainsKey(Effects.Capital)))
        {
            index++;
        }

        if (city.Improvements.Any(i => i.Effects.ContainsKey(Effects.Walled)))
        {
            index += 4;
        }

        return index;
    }
    public List<TerrainSet> TileSets { get; } = [];

    public CityImageSet CityImages { get; } = new();

    public UnitSet UnitImages { get; } = new();

    public Dictionary<string, PopupBox> Dialogs { get; set; }
    public abstract void LoadPlayerColours();
    public PlayerColour[] PlayerColours { get; set; }
    public int ExpectedMaps { get; set; } = 1;
    public CommonMapImageSet MapImages { get; } = new();
    public int DefaultDialogWidth => 660; // 660=440*1.5

    public abstract Padding DialogPadding { get; }
    
    private CityWindowLayout? _cityWindowLayout;

    protected abstract List<MenuDetails> MenuMap { get; }

    public IList<DropdownMenuContents> ConfigureGameCommands(IList<IGameCommand> commands)
    {
        MenuLoader.LoadMenus(MainApp.ActiveRuleSet);

        var menus = new List<DropdownMenuContents>();
        
        foreach (var menu in MenuMap)
        {
            var loaded = MenuLoader.For(menu.Key);
            var entries = new List<MenuCommand>();

            // Where the rules between groups of entries go. They are counted against
            // the entries that actually make it into the menu, and counted as the menu
            // is built, because an entry can be left out here -- a command the ruleset
            // doesn't provide -- or turn into several, and the row numbers were
            // previously worked out from the unfiltered list, which slid every rule
            // below such an entry out of place.
            var separatorRows = new List<int>();

            for (var i = 1; i < menu.Defaults.Count; i++)
            {
                var baseCommand = menu.Defaults[i];

                if (baseCommand.MenuText == "-")
                {
                    // A rule above the first entry, or two in a row, separates nothing.
                    if (entries.Count > 0 && !separatorRows.Contains(entries.Count))
                    {
                        separatorRows.Add(entries.Count);
                    }

                    continue;
                }

                var content = loaded.Count > i ? loaded[i] : baseCommand;

                if (baseCommand.Repeat)
                {
                    // One entry per command sharing the template's prefix, less the
                    // ones the menu already names in full.
                    var repeated = commands.Where(c =>
                        c.Id.StartsWith(baseCommand.CommandId) && menu.Defaults.All(d => d.CommandId != c.Id));

                    foreach (var gameCommand in repeated)
                    {
                        entries.Add(new MenuCommand(
                            baseCommand.MenuText.Replace("%STRING0", gameCommand.Name), Key.None,
                            gameCommand.ActivationKeys.FirstOrDefault(), gameCommand));
                    }

                    continue;
                }

                var command = commands.FirstOrDefault(c => c.Id == baseCommand.CommandId);
                if (command == null && baseCommand.OmitIfNoCommand)
                {
                    continue;
                }

                entries.Add(new MenuCommand(content.MenuText, content.Hotkey, content.Shortcut, command));
            }

            // A rule under the last entry has nothing beneath it to separate.
            separatorRows.Remove(entries.Count);

            // A menu whose every entry turned out to be unimplemented is not put on
            // the bar: an empty dropdown is worse than no dropdown.
            if (entries.Count == 0)
            {
                continue;
            }

            var menuContent = new DropdownMenuContents
            {
                Key = menu.Key,
                Commands = entries,
                SeparatorRows = separatorRows.ToArray(),
                Title = loaded.Count > 0 ? loaded[0].MenuText : menu.Defaults[0].MenuText,
                HotKey = loaded.Count > 0 ? loaded[0].Hotkey : menu.Defaults[0].Hotkey
            };

            menus.Add(menuContent);
        }

        return menus;
    }

    public abstract ListboxLooks GetListboxLooks(ListboxType? type);

    public CityWindowLayout GetCityWindowDefinition()
    {
        if (_cityWindowLayout != null) return _cityWindowLayout;

        float buttonHeight = 24;

        const float buyButtonWidth = 68;
        const int infoButtonWidth = 57;

        _cityWindowLayout = new CityWindowLayout(new BitmapStorage("city", new Rectangle(0, 0, 640, 421)))
        {
            Height = 421, Width = 636,
            InfoPanel = new InfoPanel
            {
                Box = new Rectangle(193, 215, 242, 198),
                UnitsPresent = new UnitBox
                {
                    // Below the panel's heading, not on top of it: the box shared its
                    // origin with the "Units Present" label, so the first row of units
                    // was drawn over the words.
                    Box = new Rectangle(0, 15, 232, 84),
                    Rows = 2,
                    Columns = 5
                }
            },
            TileMap = new Rectangle(7, 65, 188, 137),
            FoodStorage = new Rectangle(437, 0, 195, 163),
            CitizensBox = new Rectangle(3, 2, 433, 44),
            Production = new()
            {
                Type = "Box",
                Box = new Rectangle(437, 165, 195, 191),
                IconLocation = new(97.5f, 18)
            },
            Improvements = new ImprovementsBox()
            {
                Box = new Rectangle(5, 306, 170, 108),
                Rows = 9,
                LabelColor = Color.White,
                LabelColorShadow = Color.Black,
                ShadowOffset = new Vector2(1, 0)
            },
            Resources = new ResourceProduction
            {
                TitlePosition = new Rectangle(199, 46, 238, 15),
                Resources = new List<ResourceArea>
                {
                    new ConsumableResourceArea(name: "Food",
                        bounds: new Rectangle(203, 75, 230, 13),
                        getDisplayDetails: (val, type) =>
                        {
                            return type switch
                            {
                                OutputType.Consumption => Labels.For(LabelIndex.Food),
                                OutputType.Loss => Labels.For(LabelIndex.Hunger),
                                OutputType.Surplus => Labels.For(LabelIndex.Surplus),
                                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
                            } + ":" + val;
                        })
                    {
                        BarTop = new Color(154, 220, 88, 255),
                        BarBottom = new Color(28, 74, 16, 255)
                    },
                    new ConsumableResourceArea(name: "Trade",
                        bounds: new Rectangle(206, 116, 224, 16),
                        getDisplayDetails: (val, type) =>
                        {
                            return type switch
                            {
                                OutputType.Consumption => Labels.For(LabelIndex.Trade),
                                OutputType.Loss => Labels.For(LabelIndex.Corruption),
                                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
                            } + ":" + val;
                        }, noSurplus: true)
                    {
                        BarTop = new Color(255, 214, 96, 255),
                        BarBottom = new Color(132, 76, 4, 255)
                    },
                    new ConsumableResourceArea(name: "Shields",
                        bounds: new Rectangle(199, 181, 238, 16),
                        getDisplayDetails: (val, type) =>
                        {
                            return type switch
                            {
                                OutputType.Consumption => Labels.For(LabelIndex.Support),
                                OutputType.Loss => val > 0
                                    ? Labels.For(LabelIndex.Waste)
                                    : Labels.For(LabelIndex.Shortage),
                                OutputType.Surplus => Labels.For(LabelIndex.Production),
                                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
                            } + ":" + Math.Abs(val);
                        },
                        labelBelow: true
                    )
                    {
                        BarTop = new Color(118, 148, 240, 255),
                        BarBottom = new Color(14, 26, 88, 255)
                    },
                    new SharedResourceArea(new Rectangle(206, 140, 224, 16), true)
                    {
                        BarTop = new Color(252, 150, 72, 255),
                        BarBottom = new Color(112, 34, 6, 255),
                        Resources =
                        [
                            new()
                            {
                                Name = "Tax",
                                GetResourceLabel = (val, city) =>
                                    city.Owner.TaxRate + "% " + Labels.For(LabelIndex.Tax) + ":" + val,
                                Icon = ResourceIcon("gold")
                            },

                            new()
                            {
                                Name = "Lux",
                                GetResourceLabel = (val, city) =>
                                    city.Owner.LuxRate + "% " + Labels.For(LabelIndex.Lux) + ":" + val,
                                Icon = ResourceIcon("lux")
                            },

                            new()
                            {
                                Name = "Science",
                                GetResourceLabel = (val, city) =>
                                    city.Owner.LuxRate + "% " + Labels.For(LabelIndex.Sci) + ":" + val,
                                Icon = ResourceIcon("science")
                            }
                        ]
                    }
                }
            },
            UnitSupport = new UnitBox
            {
                // Likewise clear of its own heading, and one row rather than two: the
                // panel is 58 tall, so two rows left each unit half the height of one
                // in the Units Present box beside it.
                Box = new Rectangle(7, 229, 184, 54),
                Rows = 1,
                Columns = 4
            },
        };

        _cityWindowLayout.Buttons.Add("Buy", new(Labels.For(LabelIndex.Buy), new(5, 16, buyButtonWidth, buttonHeight)));
        _cityWindowLayout.Buttons.Add("Change", new(Labels.For(LabelIndex.Change), new(120, 16, buyButtonWidth, buttonHeight)));
        _cityWindowLayout.Buttons.Add("Info", new(Labels.For(LabelIndex.Info), new(459, 364, infoButtonWidth, buttonHeight)));
        _cityWindowLayout.Buttons.Add("Map", new(Labels.For(LabelIndex.Map), new(517, 364, infoButtonWidth, buttonHeight)));
        _cityWindowLayout.Buttons.Add("Rename", new(Labels.For(LabelIndex.Rename), new(575, 364, infoButtonWidth, buttonHeight)));
        _cityWindowLayout.Buttons.Add("Happy", new(Labels.For(LabelIndex.Happy), new(459, 389, infoButtonWidth, buttonHeight)));
        _cityWindowLayout.Buttons.Add("View", new(Labels.For(LabelIndex.View), new(517, 389, infoButtonWidth, buttonHeight)));
        _cityWindowLayout.Buttons.Add("Exit", new("Exit", new(575, 389, infoButtonWidth, buttonHeight)));

        // The classic city dialog was dark, so its section headings were gold on
        // near-black. This build's panel is light stone and gold on it is all but
        // invisible, so the headings are dark with a light shadow instead.
        var headingColour = new Color(46, 34, 16, 255);
        var headingShadow = new Color(245, 240, 226, 200);

        _cityWindowLayout.Labels.Add("FoodStorage", new(Labels.For(LabelIndex.FoodStorage), new(437, 0, 195, 12), new Color(75, 155, 35, 255), Color.Black));
        _cityWindowLayout.Labels.Add("CityImprovements", new(Labels.For(LabelIndex.CityImprovements), new(3, 291, 189, 12), headingColour, headingShadow));
        _cityWindowLayout.Labels.Add("UnitsPresent", new(Labels.For(LabelIndex.UnitsPresent), new(0, 0, 232, 12), headingColour, headingShadow));
        _cityWindowLayout.Labels.Add("UnitsSupported", new(Labels.For(LabelIndex.UnitsSupported), new(3, 215, 189, 12), headingColour, headingShadow));
        _cityWindowLayout.Labels.Add("ItemInProduction", new("", new(0, 4, 195, 12), new Color(63, 79, 167, 255), Color.Black));
        // Inset by 5 on both sides: these two sit at the bottom of the Units
        // Present panel, and at x=0 the text rendered hard against its border.
        _cityWindowLayout.Labels.Add("Supplies", new(Labels.For(LabelIndex.Supplies), new(5, 130, 222, 12), new Color(227, 83, 15, 255), new Color(67, 67, 67, 255)));
        _cityWindowLayout.Labels.Add("Demands", new(Labels.For(LabelIndex.Demands), new(5, 143, 222, 12), new Color(227, 83, 15, 255), new Color(67, 67, 67, 255)));
        _cityWindowLayout.Labels.Add("ResourceMap", new(Labels.For(LabelIndex.ResourceMap), new(0, 125, 189, 12), headingColour, headingShadow));
        _cityWindowLayout.Labels.Add("Citizens", new(Labels.For(LabelIndex.Citizens), new(0, 46, 189, 12), headingColour, headingShadow));


        return _cityWindowLayout;
    }

    /// <summary>
    /// The resource medallions. These used to be 14x14 and 10x10 patches of the
    /// ICONS sheet, which is all the original artwork was, and they were far too
    /// coarse for a window that now scales well past 1:1. They are single files at
    /// <see cref="ResourceIconSize"/> square instead, so every place that draws one
    /// fits it to the space it has rather than taking its native size.
    /// </summary>
    public const int ResourceIconSize = 96;

    private static BitmapStorage ResourceIcon(string name) =>
        new(System.IO.Path.Combine("Icons", "Resources", $"{name}.png"));

    public IList<ResourceImage> ResourceImages { get; } = new List<ResourceImage>
    {
        new(name: "Food",
            largeImage: ResourceIcon("food"),
            smallImage: ResourceIcon("food"),
            lossImage: ResourceIcon("food_loss")),
        new(name: "Shields",
            largeImage: ResourceIcon("production"),
            smallImage: ResourceIcon("production"),
            lossImage: ResourceIcon("production_loss")),
        new(name: "Trade",
            largeImage: ResourceIcon("trade"),
            smallImage: ResourceIcon("trade"),
            lossImage: ResourceIcon("trade_loss"))
    };

    public PopupBox? GetDialog(string dialogName)
    {
        return Dialogs.GetValueOrDefault(dialogName);
    }

    public abstract int UnitsRows { get; }
    public abstract int UnitsPxHeight { get; }
    public abstract Dictionary<string, IImageSource[]> PicSources { get; }
    public abstract List<CityViewTiles> GetCityViewTiles();
    public abstract List<BinaryStorage> GetCityViewAltTiles();
    public abstract void GetShieldImages();
    public abstract UnitShield UnitShield(int unitType);
    public abstract void DrawBorderWallpaper(Wallpaper wallpaper, ref Image destination, int height, int width, Padding padding, bool statusPanel);
    public abstract void DrawBorderLines(ref Image destination, int height, int width, Padding padding, bool statusPanel);
    public abstract void DrawButton(Texture2D texture, Rectangle bounds);
    public IList<Ruleset> FindRuleSets(string[] searchPaths)
    {
        var sets = new List<Ruleset>();
        foreach (var path in searchPaths)
        {
            var gameTxt = Path.Combine(path, "Game.txt");
            if (!File.Exists(gameTxt)) continue;
            
            var title = File.ReadLines(gameTxt)
                .Where(l => l.StartsWith("@title"))
                .Select(l => l.Split("=", 2)[1])
                .FirstOrDefault();
            if (title != null && CanDisplay(title))
            {
                sets.AddRange(GenerateRulesets(path, title));
            }   
        }

        return sets;
    }

    public IMain MainApp { get; } = main;

    protected abstract IEnumerable<Ruleset> GenerateRulesets(string path, string title);
    
    public abstract Padding GetPadding(float headerLabelHeight, bool footer);
    public abstract bool IsButtonInOuterPanel { get; }
    
    public int InterfaceIndex { get; set; }

    public IInterfaceAction HandleLoadScenario(IGame game, string scnName, Ruleset ruleset)
    {
        ExpectedMaps = game.NoMaps;
        Initialization.LoadGraphicsAssets(this);

        var config = Initialization.ConfigObject;
        config.TechParadigm = game.ScenarioData.TechParadigm;
        config.ScenarioName = game.ScenarioData.Name;
        config.CivNames = game.AllCivilizations.Select(c => c.TribeName).ToArray();
        config.CivGenders = game.AllCivilizations.Select(c => c.LeaderGender).ToArray();
        config.LeaderNames = game.AllCivilizations.Select(c => c.LeaderName).ToArray();
        config.StartingYear = game.ScenarioData.StartingYear;
        config.TurnYearIncrement = game.ScenarioData.TurnYearIncrement;
        config.DifficultyLevel = game.DifficultyLevel;
        config.MaxTurns = game.ScenarioData.MaxTurns;
        config.CivsInPlay = game.AllCivilizations.Select(c => c.Alive).ToArray();
        config.ObjectivesProtagonist = game.ScenarioData.ObjectiveProtagonist;
        config.ScenPlayerCivId = game.AllCivilizations.FindIndex(c => c.PlayerType == PlayerType.Local);
        config.ActiveUnitType = game.ScenarioData.ActiveUnitType;

        Initialization.Start(game);

        var titleImgPath = FileUtilities.GetFile(ruleset.FolderPath, "title.gif");
        if (titleImgPath != null)
        {
            ScenTitleImage = new BitmapStorage(titleImgPath);
        }

        var labelsPath = FileUtilities.GetFile(ruleset.FolderPath, "labels.txt");
        if (labelsPath != null)
        {
            Labels.UpdateLabels(ruleset);
        }

        var describePath = FileUtilities.GetFile(ruleset.FolderPath, "describe.txt");
        if (describePath != null)
        {
            CivilopediaLoader.UpdateMapping(ruleset);
        }
        
        // Load custom intro if it exists in txt file
        var introFile = Regex.Replace(scnName, ".scn", ".txt", RegexOptions.IgnoreCase);
        var introPath = FileUtilities.GetFile(ruleset.FolderPath, introFile);
        if (introPath != null)
        {
            var boxes = new Dictionary<string, PopupBox>();
            TextFileParser.ParseFile(Path.Combine(ruleset.FolderPath, introPath), new PopupBoxReader (boxes), true);
            if (boxes.TryGetValue("SCENARIO", out var dialogInfo))
            {
                DialogHandlers[ScenCustomIntro.Title].UpdatePopupData(new()
                {
                    { ScenCustomIntro.Title, dialogInfo }
                });

                return DialogHandlers[ScenCustomIntro.Title].Show(this);
            }
        }

        // Load default intro
        return DialogHandlers[ScenarioLoadedDialog.Title].Show(this);
    }

    public IInterfaceAction HandleLoadGame(IGame game, Model.Core.GameRules.Rules rules, Ruleset ruleset,
        Dictionary<string, string?> viewData)
    {
        ExpectedMaps = game.NoMaps;
        Initialization.LoadGraphicsAssets(this);
        Initialization.ViewData = viewData;
        Initialization.Start(game);
        return DialogHandlers[LoadOk.Title].Show(this);
    }

    public IInterfaceAction InitNewGame(bool quickStart)
    {
        Initialization.LoadGraphicsAssets(this);
        
        if (quickStart)
        {
            Initialization.ConfigObject.QuickStart = true;
            Initialization.ConfigObject.WorldSize = [50, 80];
            Initialization.ConfigObject.NumberOfCivs = this.PlayerColours.Length - 1;
            Initialization.ConfigObject.BarbarianActivity = Initialization.ConfigObject.Random.Next(5);
            
            Initialization.ConfigObject.MapTask = MapGenerator.GenerateMap(Initialization.ConfigObject);
            return DialogHandlers[DifficultyHandler.Title].Show(this);
        }

        return DialogHandlers[WorldSizeHandler.Title].Show(this);
    }

    /// <summary>
    /// The quick start: a fixed, deliberately hard setup that skips every setup
    /// dialog and drops the player straight onto the map with their opening
    /// settlers. Deity, a large world, eight civilisations, playing the Celts.
    /// </summary>
    public IInterfaceAction StartInstantGame()
    {
        Initialization.LoadGraphicsAssets(this);

        var config = Initialization.ConfigObject;
        config.QuickStart = true;
        config.WorldSize = [75, 120];      // the Large option in SIZEOFMAP
        config.DifficultyLevel = 5;        // Deity, the last DIFFICULTY option
        config.BarbarianActivity = 1;

        // Eight civilisations in total, or as many as the ruleset has colours for.
        config.NumberOfCivs = Math.Min(8, PlayerColours.Length - 1);

        var celts = config.Rules.Leaders.FirstOrDefault(l =>
                        string.Equals(l.Plural, "Celts", StringComparison.OrdinalIgnoreCase))
                    ?? config.Random.ChooseFrom(config.Rules.Leaders);
        config.Gender = celts.Female ? 1 : 0;
        config.PlayerCiv = Initialization.MakeCivilization(config, celts, true, celts.Color);

        Initialization.CompleteConfig();

        // Init.Show does this before starting; the quick start bypasses that
        // dialog, so the same correction has to happen here or the player ends up
        // holding a colour slot that no civilisation occupies.
        if (config.PlayerCiv.Id >= config.Civilizations.Count)
        {
            var correctIndex = config.Civilizations.Count - 1;
            (PlayerColours[config.PlayerCiv.Id], PlayerColours[correctIndex]) =
                (PlayerColours[correctIndex], PlayerColours[config.PlayerCiv.Id]);
            config.PlayerCiv.Id = correctIndex;
        }

        var maps = MapGenerator.GenerateMap(config).GetAwaiter().GetResult();
        var game = NewGameInitialisation.StartNewGame(config, maps,
            config.Civilizations.OrderBy(c => c.Id).ToList(), MainApp.ActiveRuleSet.Paths);
        Initialization.GameInstance = game;

        return new StartGame(game, Initialization.ViewData);
    }

    public IImageSource? GetImprovementImage(Improvement improvement, int firstWonderIndex)
    {
        var fossArtImage = GetFossArtIcon(improvement.IsWonder ? "Wonders" : "Improvements", improvement.Name);
        if (fossArtImage != null)
        {
            return fossArtImage;
        }

        var y = 1;
        var x = 343;
        var index = improvement.Type;
        var columns = 8;
        if (improvement.IsWonder)
        {
            y += 105;
            index -= firstWonderIndex;
            columns = 7;
        }
        else
        {
            index -= 1; //Remove nothing as it has no image
        }

        var (addRows, addColumns) = Math.DivRem(index, columns);

        y += addRows * 21;
        x += addColumns * 37;
        
        return new BitmapStorage("icons", x, y, 36, 20);
    }

    public IImageSource? GetAdvanceImage(Advance advance)
    {
        var fossArtImage = GetFossArtIcon("Advances", advance.Name);
        if (fossArtImage != null)
        {
            return fossArtImage;
        }

        var x = 343 + (advance.KnowledgeCategory) * 37;
        var y = 211 + (advance.Epoch) * 21;
        
        return new BitmapStorage("icons", x, y, 36, 20);
    }

    public IImageSource? GetFossArtUnitImage(int unitType)
    {
        foreach (var candidateName in GetFossArtUnitNames(unitType))
        {
            var image = GetFossArtImage("Units", candidateName);
            if (image != null)
            {
                return image;
            }
        }

        return null;
    }

    /// <summary>
    /// City style rows in the classic CITIES sheet, in the order the renderer
    /// indexes them: four per-civilisation ancient/classical/medieval styles,
    /// then the shared industrial and modern styles.
    /// </summary>
    private static readonly string[] FossArtCityStyleFolders =
        ["Aztec", "German", "Greek", "Japanese", "London", "USA"];

    /// <summary>
    /// Native high-resolution city art for a style row and size/walled column.
    /// Columns 0-3 are the unwalled size steps and 4-7 their walled variants,
    /// matching <see cref="GetCityIndexForStyle"/>.
    /// </summary>
    public IImageSource? GetFossArtCityImage(int cityStyleIndex, int sizeIndex)
    {
        if (cityStyleIndex < 0 || cityStyleIndex >= FossArtCityStyleFolders.Length ||
            sizeIndex < 0 || sizeIndex > 7)
        {
            return null;
        }

        return GetFossArtImage(Path.Combine("Cities", FossArtCityStyleFolders[cityStyleIndex]),
            $"city_{sizeIndex + 1:00}");
    }

    private static IEnumerable<string> GetFossArtUnitNames(int unitType)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Enum.IsDefined(typeof(UnitType), unitType))
        {
            var civ2UnitType = (UnitType)unitType;

            if (FossArtUnitAliases.TryGetValue(civ2UnitType, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (seen.Add(NormalizeFossArtName(alias)))
                    {
                        yield return alias;
                    }
                }
            }

            var enumName = civ2UnitType.ToString();
            if (seen.Add(NormalizeFossArtName(enumName)))
            {
                yield return enumName;
            }
        }
    }

    private static readonly Dictionary<UnitType, string[]> FossArtUnitAliases = new()
    {
        [UnitType.Settlers] = ["settlers"],
        [UnitType.Engineers] = ["engineers"],
        [UnitType.Warriors] = ["warriors"],
        [UnitType.Phalanx] = ["phalanx"],
        [UnitType.Archers] = ["archers", "archer"],
        [UnitType.Legions] = ["legions", "legion"],
        [UnitType.Pikemen] = ["pikemen"],
        [UnitType.Musketeers] = ["musketeers"],
        [UnitType.Fanatics] = ["fanatics"],
        [UnitType.Partisans] = ["partisans"],
        [UnitType.AlpineTroops] = ["alpinetroops", "alpine troops"],
        [UnitType.Riflemen] = ["riflemen"],
        [UnitType.Marines] = ["marines"],
        [UnitType.Paratroopers] = ["paratroopers"],
        [UnitType.MechInf] = ["mechanized infantry", "mechinfantry", "mech inf"],
        [UnitType.Horsemen] = ["horsemen"],
        [UnitType.Chariot] = ["chariot"],
        [UnitType.Elephant] = ["elephant"],
        [UnitType.Crusaders] = ["crusaders", "crusader"],
        [UnitType.Knights] = ["knight", "knights"],
        [UnitType.Dragoons] = ["dragoons"],
        [UnitType.Cavalry] = ["cavalry"],
        [UnitType.Armor] = ["armor", "armour"],
        [UnitType.Catapult] = ["catapult"],
        [UnitType.Cannon] = ["cannon"],
        [UnitType.Artillery] = ["artillery"],
        [UnitType.Howitzer] = ["howitzer"],
        [UnitType.Fighter] = ["fighters", "fighter"],
        [UnitType.Bomber] = ["bombers", "bomber"],
        [UnitType.Helicopter] = ["helicopter"],
        [UnitType.StlthFtr] = ["stealthfighter", "stealth fighter"],
        [UnitType.StlthBmbr] = ["stealthbomber", "stealth bomber"],
        [UnitType.Trireme] = ["trireme"],
        [UnitType.Caravel] = ["caravel"],
        [UnitType.Galleon] = ["galleon"],
        [UnitType.Frigate] = ["frigate"],
        [UnitType.Ironclad] = ["ironclad"],
        [UnitType.Destroyer] = ["destroyer"],
        [UnitType.Cruiser] = ["cruiser"],
        [UnitType.AegisCruiser] = ["aegiscruiser", "aegis cruiser", "cruiser"],
        [UnitType.Battleship] = ["battleship"],
        [UnitType.Submarine] = ["submarine"],
        [UnitType.Carrier] = ["carrier"],
        [UnitType.Transport] = ["transport"],
        [UnitType.CruiseMsl] = ["cruisemissile", "cruise missile"],
        [UnitType.NuclearMsl] = ["nuclearmissile", "nuclear missile"],
        [UnitType.Diplomat] = ["diplomat"],
        [UnitType.Spy] = ["spy"],
        [UnitType.Caravan] = ["caravan"],
        [UnitType.Freight] = ["freight"],
        [UnitType.Explorer] = ["explorer"],
    };

    /// <summary>
    /// The bundled high-resolution flags, and the colour of the cloth in each.
    /// Built once; the sheet is nine colours in two shades.
    /// </summary>
    private static List<(IImageSource Source, Vector3 Colour)>? _fossArtFlags;

    /// <summary>
    /// The bundled flag whose cloth is closest to a civilisation's colour.
    /// <para>
    /// The art is matched by colour rather than by position, because nothing ties
    /// the order the flags were cut in to the order the ruleset lists its
    /// civilisations. Matching on the colour the game already uses for a
    /// civilisation cannot put the wrong flag over a city, whatever order the
    /// files happen to be in.
    /// </para>
    /// </summary>
    protected static IImageSource? GetFossArtFlagImage(Color civilisationColour)
    {
        _fossArtFlags ??= LoadFossArtFlags();
        if (_fossArtFlags.Count == 0)
        {
            return null;
        }

        var wanted = new Vector3(civilisationColour.R, civilisationColour.G, civilisationColour.B);
        return _fossArtFlags
            .MinBy(flag => Vector3.DistanceSquared(flag.Colour, wanted))
            .Source;
    }

    private static List<(IImageSource Source, Vector3 Colour)> LoadFossArtFlags()
    {
        var flags = new List<(IImageSource, Vector3)>();
        foreach (var categoryPath in GetFossArtCategoryPaths("Flags"))
        {
            foreach (var file in GetFossArtFileIndex(categoryPath).Values.OrderBy(f => f, StringComparer.Ordinal))
            {
                var source = new BitmapStorage(file);
                if (MeasureFlagColour(source) is { } colour)
                {
                    flags.Add((source, colour));
                }
            }

            if (flags.Count > 0)
            {
                break;
            }
        }

        return flags;
    }

    /// <summary>
    /// The average colour of a flag's cloth. The pole and the near-white highlights
    /// on the folds are left out, so a white flag is not mistaken for a grey one and
    /// a dark flag is not dragged towards its own shadows.
    /// </summary>
    private static Vector3? MeasureFlagColour(IImageSource source)
    {
        var image = Images.ExtractBitmap(source);
        if (image.Width <= 1 || image.Height <= 1)
        {
            return null;
        }

        var pixels = image.LoadColors();
        try
        {
            var total = Vector3.Zero;
            var counted = 0;
            foreach (var pixel in pixels)
            {
                if (pixel.A < 200)
                {
                    continue;
                }

                var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                // Skip the pole and the specular streaks: near-neutral and bright.
                if (maximum - minimum < 24 && maximum > 150)
                {
                    continue;
                }

                total += new Vector3(pixel.R, pixel.G, pixel.B);
                counted++;
            }

            return counted == 0 ? null : total / counted;
        }
        finally
        {
            Image.UnloadColors(pixels);
        }
    }

    private static IImageSource? GetFossArtImage(string category, string title)
    {
        var directName = NormalizeFossArtName(title);

        foreach (var categoryPath in GetFossArtCategoryPaths(category))
        {
            if (GetFossArtFileIndex(categoryPath).TryGetValue(directName, out var matchingFile))
            {
                return new BitmapStorage(matchingFile);
            }
        }

        return null;
    }

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> FossArtFileIndexes =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object FossArtFileIndexLock = new();

    private static IReadOnlyDictionary<string, string> GetFossArtFileIndex(string categoryPath)
    {
        lock (FossArtFileIndexLock)
        {
            if (FossArtFileIndexes.TryGetValue(categoryPath, out var cached))
            {
                return cached;
            }

            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(categoryPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                index.TryAdd(NormalizeFossArtName(Path.GetFileNameWithoutExtension(file)), file);
            }

            FossArtFileIndexes[categoryPath] = index;
            return index;
        }
    }

    private static IImageSource? GetFossArtIcon(string category, string title)
    {
        // Civ II's gameplay icons are compact 36x20 cards. Keep those separate from
        // the square, high-resolution FOSS paintings used by the Civilopedia.
        return GetFossArtImage(Path.Combine("Icons", category), title)
               ?? GetFossArtImage(category, title);
    }

    private static IEnumerable<string> GetFossArtCategoryPaths(string category)
    {
        var roots = Settings.SearchPaths
            .Concat([
                Environment.CurrentDirectory,
                AppContext.BaseDirectory,
                Path.Combine(Environment.CurrentDirectory, "RaylibUI")
            ]);

        foreach (var rootPath in roots.Distinct())
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(rootPath, category),
                         Path.Combine(rootPath, "FOSSart", category),
                         Path.Combine(rootPath, "FOSSart", "FOSSart", category),
                         Path.Combine(rootPath, "FOSS art", category),
                         Path.Combine(rootPath, "RaylibUI", "FOSSart", category),
                         Path.Combine(rootPath, "RaylibUI", "FOSSart", "FOSSart", category),
                         Path.Combine(rootPath, "RaylibUI", "FOSS art", category)
                     })
            {
                if (Directory.Exists(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static string NormalizeFossArtName(string value)
    {
        return Regex.Replace(value, "[^A-Za-z0-9]", string.Empty).ToLowerInvariant();
    }

    public string GetScientistName(int epoch)
    {
        return Labels.For(epoch < 3 ? LabelIndex.wisemen : LabelIndex.scientists);
    }

    public CivilopediaProperties GetCivilopediaProperties(CivilopediaEntry civilopediaEntry)
    {
        return new()
        {
            Listbox = new()
            {
                Rows = 14,
                Columns = 2,
                RowHeight = 33,
                VerticalScrollbar = false,
                IconScale = civilopediaEntry.InfoType switch
                {
                    CivilopediaInfoType.Advances or CivilopediaInfoType.Improvements or CivilopediaInfoType.Wonders => 1.5f,
                    _ => 1.0f
                },
            },
            Buttons = civilopediaEntry switch
            {
                var c when c.WindowType == CivilopediaWindowType.Description || c.WindowType == CivilopediaWindowType.Tree 
                    => ["Go Back", "Close"],
                var c when c.WindowType == CivilopediaWindowType.Listbox && c.InfoType == CivilopediaInfoType.Advances
                    => ["Info", "Tree", "Close"],
                var c when c.WindowType == CivilopediaWindowType.Listbox && c.InfoType != CivilopediaInfoType.Advances
                    => ["Info", "Close"],
                var c when c.WindowType == CivilopediaWindowType.Info && c.InfoType == CivilopediaInfoType.Advances
                    => ["Go Back", "Tree", "Close"],
                var c when c.WindowType == CivilopediaWindowType.Info &&
                    (c.InfoType == CivilopediaInfoType.Governments || c.InfoType == CivilopediaInfoType.Concepts)
                    => ["Go Back", "Close"],
                // A city improvement's info page already shows its description, so
                // the Description button only navigated to what was on screen.
                var c when c.WindowType == CivilopediaWindowType.Info && c.InfoType == CivilopediaInfoType.Improvements
                    => ["Go Back", "Close"],
                _ => ["Go Back", "Description", "Close"],
            }
        };
    }
}
