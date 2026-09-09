using RhyCiv.UI.Classic;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using JetBrains.Annotations;
using Model;
using Model.Images;
using Model.ImageSets;
using Model.Input;
using Model.Interface;
using Model.Controls;
using Model.Core.GameRules;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Transformations;
using Model.Utils;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Images;
using Raylib_CSharp.Rendering;
using RaylibUtils;
using static Model.Controls.CommandIds;
using System.IO;
using System.Numerics;

namespace RhyCiv.UI.Compact;

[UsedImplicitly]
public class CompactInterface(IMain main) : ClassicInterface(main)
{
    public override string Title => "Civilization II Multiplayer Gold";

    public override bool CanDisplay(string? title) =>
        title != null && (title.Contains(Title) || title.Contains("rhYciv Standalone"));

    public override string InitialMenu => "MAINMENU";

    public override InterfaceStyle Look { get; } = new()
    {
        Outer = new BitmapStorage("ICONS", new Rectangle(199, 322, 64, 32)),
        Inner = [new BitmapStorage("ICONS", new Rectangle(298, 190, 32, 32))],

        RadioButtons = [new BitmapStorage("buttons.png", 0, 0, 32), new BitmapStorage("buttons.png", 32, 0, 32)],
        CheckBoxes = [new BitmapStorage("buttons.png", 0, 32, 32), new BitmapStorage("buttons.png", 32, 32, 32)],
        DiskIcons = [new BitmapStorage("explorer_icons.png", 0, 0, 32), new BitmapStorage("explorer_icons.png", 32, 0, 32),
          new BitmapStorage("explorer_icons.png", 64, 0, 32), new BitmapStorage("explorer_icons.png", 0, 32, 32),
          new BitmapStorage("explorer_icons.png", 32, 32, 32), new BitmapStorage("explorer_icons.png", 64, 32, 32)],

        DefaultFont = Fonts.Arial,
        ButtonFont = Fonts.Arial,
        ButtonFontSize = 18,
        ButtonColour = Color.Black,
        HeaderLabelFont = Fonts.TnRbold,
        HeaderLabelFontSizeNormal = 28,
        HeaderLabelFontSizeLarge = 34,
        CityHeaderLabelFontSizeNormal = 18,
        CityHeaderLabelFontSizeLarge = 28,
        CityHeaderLabelFontSizeSmall = 16,
        HeaderLabelShadow = true,
        HeaderLabelColour = new Color(135, 135, 135, 255),
        LabelFont = Fonts.Arial,
        LabelFontSize = 18,
        LabelColour = Color.Black,
        LabelShadowColour = new Color(182, 182, 182, 255),
        CityWindowFont = Fonts.Arial,
        CityWindowFontSize = 14,  // small=6, normal=14, large=20
        MenuFont = Fonts.Arial,
        MenuFontSize = 14,
        CivilopediaFontSize = 22,
        StatusPanelLabelFont = Fonts.Arial,
        StatusPanelLabelColor = new Color(36, 36, 36, 255),
        StatusPanelLabelColorShadow = new Color(204, 204, 204, 255),
        MovingUnitsViewingPiecesLabelColor = Color.White,
        MovingUnitsViewingPiecesLabelColorShadow = Color.Black,
        EndOfTurnColors = [new Color(135, 135, 135, 255), Color.White],
    };

    public override bool IsButtonInOuterPanel => true;
    
    public override Padding GetPadding(float headerLabelHeight, bool footer)
    {
        var paddingTop = headerLabelHeight != 0 ? 7 + Math.Max((int)(-3 + 2 / 9f * headerLabelHeight + headerLabelHeight), (int)headerLabelHeight) : 11;
        var paddingBtm = footer ? 46 : 10;

        return new Padding(paddingTop, bottom:paddingBtm, left:11, right:11);
    }

    public override Padding DialogPadding => new(11);

    public override void Initialize()
    {
        base.Initialize();

        PicSources.Add("unit",
            Enumerable.Range(0, 9 * UnitsRows).Select(i => new BitmapStorage("UNITS",
                new Rectangle(1 + 65 * (i % 9), 1 + (UnitsPxHeight + 1) * (i / 9), 64, UnitsPxHeight),
                searchFlagLoc: true)).ToArray<IImageSource>());
        PicSources.Add("HPshield", [new BitmapStorage("UNITS", new Rectangle(597, 30, 12, 20))]);
        PicSources.Add("backShield1", [new BitmapStorage("UNITS", new Rectangle(586, 1, 12, 20))]);
        PicSources.Add("backShield2", [new BitmapStorage("UNITS", new Rectangle(599, 1, 12, 20))]);
        PicSources.Add("textColours", Enumerable.Range(0, 9).Select(col =>
            new BitmapStorage("CITIES", new Rectangle(1 + 15 * col, 423, 14, 1))).ToArray<IImageSource>());
        PicSources.Add("flags", Enumerable.Range(0, 2 * 9).Select(i =>
                new BitmapStorage("CITIES", new Rectangle(1 + 15 * (i % 9), 425 + 23 * (i / 9), 14, 22)))
            .ToArray<IImageSource>());
        PicSources.Add("fortify", [new BitmapStorage("CITIES", new Rectangle(143, 423, 64, 48))]);
        PicSources.Add("fortress", [new BitmapStorage("CITIES", new Rectangle(208, 423, 64, 48))]);
        PicSources.Add("airbase,empty", [new BitmapStorage("CITIES", new Rectangle(273, 423, 64, 48))]);
        PicSources.Add("airbase,full", [new BitmapStorage("CITIES", new Rectangle(338, 423, 64, 48))]);
        PicSources.Add("base1", Enumerable.Range(0, 11).Select(row =>
            new BitmapStorage("TERRAIN1", new Rectangle(1, 1 + 33 * row, 64, 32))).ToArray<IImageSource>());
        PicSources.Add("base2", Enumerable.Range(0, 11).Select(row =>
            new BitmapStorage("TERRAIN1", new Rectangle(66, 1 + 33 * row, 64, 32))).ToArray<IImageSource>());
        PicSources.Add("special1", Enumerable.Range(0, 11).Select(row =>
            new BitmapStorage("TERRAIN1", new Rectangle(131, 1 + 33 * row, 64, 32))).ToArray<IImageSource>());
        PicSources.Add("special2", Enumerable.Range(0, 11).Select(row =>
            new BitmapStorage("TERRAIN1", new Rectangle(196, 1 + 33 * row, 64, 32))).ToArray<IImageSource>());
        PicSources.Add("road", Enumerable.Range(0, 9).Select(col =>
            new BitmapStorage("TERRAIN1", new Rectangle(1 + 65 * col, 363, 64, 32))).ToArray<IImageSource>());
        PicSources.Add("railroad", Enumerable.Range(0, 9).Select(col =>
            new BitmapStorage("TERRAIN1", new Rectangle(1 + 65 * col, 397, 64, 32))).ToArray<IImageSource>());
        PicSources.Add("irrigation", [new BitmapStorage("TERRAIN1", new Rectangle(456, 100, 64, 32))]);
        PicSources.Add("farmland", [new BitmapStorage("TERRAIN1", new Rectangle(456, 133, 64, 32))]);
        PicSources.Add("mine", [new BitmapStorage("TERRAIN1", new Rectangle(456, 166, 64, 32))]);
        PicSources.Add("pollution", [new BitmapStorage("TERRAIN1", new Rectangle(456, 199, 64, 32))]);
        PicSources.Add("shield", [new BitmapStorage("TERRAIN1", new Rectangle(456, 232, 64, 32))]);
        PicSources.Add("hut", [new BitmapStorage("TERRAIN1", new Rectangle(456, 265, 64, 32))]);
        PicSources.Add("dither", [new BitmapStorage("TERRAIN1", new Rectangle(1, 447, 64, 32))]);
        PicSources.Add("blank", [new BitmapStorage("TERRAIN1", new Rectangle(131, 447, 64, 32))]);
        PicSources.Add("connection", Enumerable.Range(0, 2 * 8).Select(i =>
                new BitmapStorage("TERRAIN2", new Rectangle(1 + 65 * (i % 8), 1 + 33 * (i / 8), 64, 32)))
            .ToArray<IImageSource>());
        PicSources.Add("river", Enumerable.Range(0, 2 * 8).Select(i =>
                new BitmapStorage("TERRAIN2", new Rectangle(1 + 65 * (i % 8), 67 + 33 * (i / 8), 64, 32)))
            .ToArray<IImageSource>());
        PicSources.Add("forest", Enumerable.Range(0, 2 * 8).Select(i =>
                new BitmapStorage("TERRAIN2", new Rectangle(1 + 65 * (i % 8), 133 + 33 * (i / 8), 64, 32)))
            .ToArray<IImageSource>());
        PicSources.Add("mountain", Enumerable.Range(0, 2 * 8).Select(i =>
                new BitmapStorage("TERRAIN2", new Rectangle(1 + 65 * (i % 8), 199 + 33 * (i / 8), 64, 32)))
            .ToArray<IImageSource>());
        PicSources.Add("hill", Enumerable.Range(0, 2 * 8).Select(i =>
                new BitmapStorage("TERRAIN2", new Rectangle(1 + 65 * (i % 8), 265 + 33 * (i / 8), 64, 32)))
            .ToArray<IImageSource>());
        PicSources.Add("riverMouth", Enumerable.Range(0, 4).Select(col =>
            new BitmapStorage("TERRAIN2", new Rectangle(1 + 65 * col, 331, 64, 32))).ToArray<IImageSource>());
        // The tile marker at map-art resolution rather than a 64x32 slot of the icon
        // sheet, which at this build's zoom was a five-times magnification of a
        // stair-stepped outline inside an opaque square (#57).
        PicSources.Add("viewPiece", [new BitmapStorage("VIEWPIECE")]);
        PicSources.Add("gridlines", [new BitmapStorage("ICONS", new Rectangle(183, 430, 64, 32))]);
        PicSources.Add("gridlines,visible", [new BitmapStorage("ICONS", new Rectangle(248, 430, 64, 32))]);
        PicSources.Add("battleAnim", Enumerable.Range(0, 8).Select(col =>
            new BitmapStorage("ICONS", new Rectangle(1 + 33 * col, 356, 32, 32))).ToArray<IImageSource>());
        PicSources.Add("researchProgress", Enumerable.Range(0, 4).Select(col =>
            new BitmapStorage("ICONS", new Rectangle(49 + 15 * col, 290, 14, 14))).ToArray<IImageSource>());
        PicSources.Add("globalWarming", Enumerable.Range(0, 4).Select(col =>
            new BitmapStorage("ICONS", new Rectangle(49 + 15 * col, 305, 14, 14))).ToArray<IImageSource>());
        PicSources.Add("advanceCategories", Enumerable.Range(0, 5 * 4).Select(i =>
            new BitmapStorage("ICONS", new Rectangle(343 + 37 * (i % 5), 211 + 21 * (i / 5), 36, 20))).ToArray<IImageSource>());
        PicSources.Add("close", [new BitmapStorage("ICONS", new Rectangle(1, 389, 16, 16))]);
        PicSources.Add("zoomIn", [new BitmapStorage("ICONS", new Rectangle(18, 389, 16, 16))]);
        PicSources.Add("zoomOut", [new BitmapStorage("ICONS", new Rectangle(35, 389, 16, 16))]);
        PicSources.Add("gold,large", [new BitmapStorage("ICONS", new Rectangle(16, 320, 14, 14))]);
        PicSources.Add("science,large", [new BitmapStorage("ICONS", new Rectangle(31, 320, 14, 14))]);
        PicSources.Add("trade,small", [ new BitmapStorage("ICONS", new Rectangle(71, 334, 10, 10))]);
        PicSources.Add("backgroundImage", [new BitmapStorage(Path.Combine("Backgrounds", "NewCartographerBackground.png"))]);
        PicSources.Add("backgroundImageSmall1", [new BitmapStorage(Path.Combine("Backgrounds", "panel.jpg"))]);
        PicSources.Add("backgroundImageSmall2", [new BitmapStorage(Path.Combine("Backgrounds", "panel.jpg"))]);
        PicSources.Add("victoryConquest",
            [new BitmapStorage(Path.Combine("Backgrounds", "victory_conquest.png"))]);
        PicSources.Add("cityBuiltAncient", [new BitmapStorage(Path.Combine("Cities", "Aztec", "city_01.png"))]);
        PicSources.Add("cityBuiltModern", [new BitmapStorage(Path.Combine("Cities", "USA", "city_04.png"))]);
        foreach (var panelName in new[]
                 {
                     "taxRateBack", "cityReport", "defenseMinister", "attitudeAdvisor", "tradeAdvisor",
                     "scienceAdvisor", "worldWonders"
                 })
        {
            PicSources.Add(panelName, [new BitmapStorage(Path.Combine("Backgrounds", "panel.jpg"))]);
        }
        string[] citizenEras = ["ancient", "renaissance", "industrial", "modern"];
        string[] citizenRoles =
        [
            "happy_1", "happy_2", "content_1", "content_2", "unhappy_1", "unhappy_2",
            "angry_1", "angry_2", "entertainer", "taxman", "scientist"
        ];
        PicSources.Add("people", citizenEras.SelectMany(era => citizenRoles.Select(role =>
            new BitmapStorage(Path.Combine("People", $"{era}_{role}.png")))).ToArray<IImageSource>());

        var src = new IImageSource[6 * 8];
        for (var row = 0; row < 6; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                src[8 * row + col] = new BitmapStorage("CITIES", new Rectangle(1 + 65 * col, 39 + 49 * row, 64, 48),
                    searchFlagLoc: true); // Open cities
                src[8 * row + 4 + col] = new BitmapStorage("CITIES",
                    new Rectangle(334 + 65 * col, 39 + 49 * row, 64, 48), searchFlagLoc: true); // Walled cities
            }
        }

        PicSources.Add("city", src);

        src = new IImageSource[4 * 8];
        for (var i = 0; i < 8; i++)
        {
            src[4 * i + 0] = new BitmapStorage("TERRAIN2", new Rectangle(1 + 66 * i, 429, 32, 16));
            src[4 * i + 1] = new BitmapStorage("TERRAIN2", new Rectangle(1 + 66 * i, 446, 32, 16));
            src[4 * i + 2] = new BitmapStorage("TERRAIN2", new Rectangle(1 + 66 * i, 463, 32, 16));
            src[4 * i + 3] = new BitmapStorage("TERRAIN2", new Rectangle(34 + 66 * i, 463, 32, 16));
        }

        PicSources.Add("coastline", src);

        PicSources.Add("cvOcean", Enumerable.Repeat<IImageSource>(
            new BitmapStorage(Path.Combine("Backgrounds", "city_ocean.jpg")), 4).ToArray());
        PicSources.Add("cvRiver", Enumerable.Repeat<IImageSource>(
            new BitmapStorage(Path.Combine("Backgrounds", "city_river.jpg")), 4).ToArray());
        PicSources.Add("cvContinent", Enumerable.Repeat<IImageSource>(
            new BitmapStorage(Path.Combine("Backgrounds", "city_land.jpg")), 4).ToArray());
    }

    protected override List<MenuDetails> MenuMap { get; } =
    [
        new()
        {
            Key = "GAME", Defaults = new List<MenuElement>
            {
                new("&Game", Shortcut.None, Key.G),
                new("Game &Options|Ctrl+O", new Shortcut(Key.O, ctrl: true), Key.O,
                    commandId: GameOptions),
                new("Graphic O&ptions|Ctrl+P", new Shortcut(Key.P, ctrl: true),
                    Key.P, commandId: GraphicOptions),
                new("&City Report Options|Ctrl+E", new Shortcut(Key.E, ctrl: true),
                    Key.C, commandId: CityReportOptions),
                new("Ad&vanced Settings|Ctrl+A", new Shortcut(Key.A, ctrl: true),
                    Key.V, commandId: AdvancedSettings),
                new("M&ultiplayer Options|Ctrl+Y", new Shortcut(Key.Y, ctrl: true),
                    Key.U, omitIfNoCommand: true),
                new("&Game Profile", Shortcut.None, Key.G, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("Pick &Music", Shortcut.None, Key.M, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("&Save Game|Ctrl+S", new Shortcut(Key.S, ctrl: true), Key.S,
                    commandId: SaveGame),
                new("&Load Game|Ctrl+L", new Shortcut(Key.L, ctrl: true), Key.L,
                    commandId: LoadGame),
                new("&Join Game|Ctrl+J", new Shortcut(Key.J, ctrl: true), Key.J, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("Set Pass&word|Ctrl+W", new Shortcut(Key.W, ctrl: true), Key.W, omitIfNoCommand: true),
                new("Change &Timer|Ctrl+T", new Shortcut(Key.T, ctrl: true), Key.T, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("&Retire|Ctrl+R", new Shortcut(Key.R, ctrl: true), Key.R, omitIfNoCommand: true),
                new("&Quit|Ctrl+Q", new Shortcut(Key.Q, ctrl: true), Key.Q,
                    commandId: QuitGame)
            },
        },

        new()
        {
            Key = "KINGDOM", Defaults = new List<MenuElement>
            {
                new("&Kingdom", Shortcut.None, Key.K),
                new("&Tax Rate|Shift+T", new Shortcut(Key.T, shift: true), Key.T, commandId: ChangeTaxRate),
                new("Find &City|Shift+C", new Shortcut(Key.C, shift: true), Key.C, commandId: FindCity),
                new("-", Shortcut.None, Key.None),
                new("&REVOLUTION|Shift+R", new Shortcut(Key.R, shift: true), Key.R, commandId: Revolution)
            },
        },


        new()
        {
            Key = "VIEW", Defaults = new List<MenuElement>
            {
                new("&View", Shortcut.None, Key.V),
                new("&Move Pieces|v", new Shortcut(Key.V), Key.M, MovePieces),
                new("&View Pieces|v", new Shortcut(Key.V), Key.V, ViewPieces),
                new("-", Shortcut.None, Key.None),
                new("Zoom &In|z", new Shortcut(Key.Z), Key.I, commandId: ZoomIn),
                new("Zoom &Out|X", new Shortcut(Key.X), Key.O, commandId: ZoomOut),
                new("-", Shortcut.None, Key.None),
                new("Max Zoom In|Ctrl+Z", new Shortcut(Key.Z, ctrl: true),
                    Key.None, commandId: MaxZoomIn),
                new("Standard Zoom|Shift+Z", new Shortcut(Key.Z, shift: true),
                    Key.None, commandId: StandardZoom),
                new("Medium Zoom Out|Shift+X", new Shortcut(Key.X, shift: true),
                    Key.None, commandId: MediumZoomOut),
                new("Max Zoom Out|Ctrl+X", new Shortcut(Key.X, ctrl: true),
                    Key.None, commandId: MaxZoomOut),
                new("-", Shortcut.None, Key.None),
                new("Show Map Grid|Ctrl+G", new Shortcut(Key.G, ctrl: true),
                    Key.None, commandId: ShowMapGrid),
                new("Arrange Windows", Shortcut.None, Key.None, omitIfNoCommand: true),
                new("Show Hidden Terrain|t", new Shortcut(Key.T), Key.T, omitIfNoCommand: true),
                new("&Center View|c", new Shortcut(Key.C), Key.C, CenterView)
            },
        },


        new()
        {
            Key = "@ORDERS", Defaults = new List<MenuElement>
            {
                new("&Orders", Shortcut.None, Key.O),
                new("&Build New City|b", new Shortcut(Key.B), Key.B, BuildCityOrder, true),
                new("Build &Road|r", new Shortcut(Key.R), Key.R, BuildRoadOrder,
                    omitIfNoCommand: true),
                new("Build &Irrigation|i", new Shortcut(Key.I), Key.I, BuildIrrigationOrder,
                    omitIfNoCommand: true),
                new("%STRING0", Shortcut.None, Key.None, BuildImprovementOrderBase,
                    omitIfNoCommand: true, repeat: true),
                new("Transform to ...|o", new Shortcut(Key.O), Key.T, omitIfNoCommand: true),
                new("Automate Settler|k", new Shortcut(Key.K), Key.None, AutomateSettlerOrder),
                new("&Pillage|Shift+P", new Shortcut(Key.P, shift: true), Key.P, PillageOrder),
                new("&Unload|u", new Shortcut(Key.U), Key.U, UnloadOrder),
                new("&Go To|g", new Shortcut(Key.G), Key.G, GotoOrder),
                new("&Paradrop|p", new Shortcut(Key.P), Key.P, ParadropOrder, omitIfNoCommand: true),
                new("Air&lift|l", new Shortcut(Key.L), Key.L, omitIfNoCommand: true),
                new("Set &Home City|h", new Shortcut(Key.H), Key.H, SetHomeCityOrder),
                new("&Fortify|f", new Shortcut(Key.F), Key.F, FortifyOrder),
                new("&Sleep|s", new Shortcut(Key.S), Key.S, SleepOrder),
                new("&Disband|Shift+D", new Shortcut(Key.D, shift: true), Key.D, DisbandOrder),
                new("&Activate Unit|a", new Shortcut(Key.A), Key.A, ActivateUnitOrder),
                new("&Wait|w", new Shortcut(Key.W), Key.W, WaitOrder),
                new("S&kip Turn|SPACE", new Shortcut(Key.Space), Key.K, SkipOrder),
                new("End Player Tur&n|Ctrl+N", new Shortcut(Key.T, shift: true),
                    Key.N, EndTurn)
            },
        },

        new()
        {
            Key = "WORLD", Defaults = new List<MenuElement>
            {
                new("&World", Shortcut.None, Key.W),
                new("&Wonders of the World|F7", new Shortcut(Key.F7), Key.W, commandId: WorldWonders),
                new("&Top 5 Cities|F8", new Shortcut(Key.F8), Key.T, omitIfNoCommand: true),
                new("&Civilization Score|F9", new Shortcut(Key.F9), Key.C, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("&Demographics|F11", new Shortcut(Key.F11), Key.D, omitIfNoCommand: true)
            },
        },


        new()
        {
            Key = "CHEAT", Defaults = new List<MenuElement>
            {
                new("&Cheat", Shortcut.None, Key.C),
                new("Toggle Cheat Mode|Ctrl+K", new Shortcut(Key.K, ctrl: true),
                    Key.None, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("Create &Unit|Shift+F1", new Shortcut(Key.F1, shift: true),
                    Key.U, omitIfNoCommand: true),
                new("Reveal &Map|Shift+F2", new Shortcut(Key.F2, shift: true),
                    Key.M, CheatRevealMapCommand),
                new("Set &Human Player|Shift+F3", new Shortcut(Key.F3, shift: true),
                    Key.H, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("Set Game Year|Shift+F4", new Shortcut(Key.F4, shift: true),
                    Key.None, omitIfNoCommand: true),
                new("&Kill Civilization|Shift+F5", new Shortcut(Key.F5, shift: true),
                    Key.K, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("Te&chnology Advance|Shift+F6", new Shortcut(Key.F6, shift: true),
                    Key.C, CheatTechnologyAdvance),
                new("&Edit Technologies|Ctrl+Shift+F6",
                    new Shortcut(Key.F6, ctrl: true, shift: true), Key.E, omitIfNoCommand: true),
                new("Force &Government|Shift+F7", new Shortcut(Key.F7, shift: true),
                    Key.G, CheatForceGovernment),
                new("Change &Terrain At Cursor|Shift+F8", new Shortcut(Key.F8, shift: true),
                    Key.T, omitIfNoCommand: true),
                new("Destro&Y All Units At Cursor|Ctrl+Shift+D",
                    new Shortcut(Key.D, ctrl: true, shift: true), Key.Y, omitIfNoCommand: true),
                new("Change Money|Shift+F9", new Shortcut(Key.F9, shift: true),
                    Key.None, CheatChangeMoneyCommand),
                new("-", Shortcut.None, Key.None),
                new("Edit Unit|Ctrl+Shift+U", new Shortcut(Key.U, ctrl: true, shift: true),
                    Key.None, omitIfNoCommand: true),
                new("Edit City|Ctrl+Shift+C", new Shortcut(Key.C, ctrl: true, shift: true),
                    Key.None, omitIfNoCommand: true),
                new("Edit King|Ctrl+Shift+K", new Shortcut(Key.K, ctrl: true, shift: true),
                    Key.None, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("Scenario Parameters|Ctrl+Shift+P",
                    new Shortcut(Key.P, ctrl: true, shift: true), Key.None, omitIfNoCommand: true),
                new("Save As Scenario|Ctrl+Shift+S",
                    new Shortcut(Key.S, ctrl: true, shift: true), Key.None, omitIfNoCommand: true)
            },
        },


        new()
        {
            Key = "EDITOR", Defaults = new List<MenuElement>
            {
                new("&Editor", Shortcut.None, Key.E),
                new("Toggle &Scenario Flag|Ctrl+F", new Shortcut(Key.F, ctrl: true),
                    Key.S, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("&Advances Editor|Ctrl+Shift+1",
                    new Shortcut(Key.D1, ctrl: true, shift: true), Key.A, omitIfNoCommand: true),
                new("&Cities Editor|Ctrl+Shift+2",
                    new Shortcut(Key.D2, ctrl: true, shift: true), Key.C, omitIfNoCommand: true),
                new("E&ffects Editor|Ctrl+Shift+3",
                    new Shortcut(Key.D3, ctrl: true, shift: true), Key.F, omitIfNoCommand: true),
                new("&Improvements Editor|Ctrl+Shift+4",
                    new Shortcut(Key.D4, ctrl: true, shift: true), Key.I, omitIfNoCommand: true),
                new("&Terrain Editor|Ctrl+Shift+5",
                    new Shortcut(Key.D5, ctrl: true, shift: true), Key.T, omitIfNoCommand: true),
                new("T&ribe Editor|Ctrl+Shift+6",
                    new Shortcut(Key.D6, ctrl: true, shift: true), Key.R, omitIfNoCommand: true),
                new("&Units Editor|Ctrl+Shift+7",
                    new Shortcut(Key.D7, ctrl: true, shift: true), Key.U, omitIfNoCommand: true),
                new("&Events Editor|Ctrl+Shift+8",
                    new Shortcut(Key.D8, ctrl: true, shift: true), Key.E, omitIfNoCommand: true),
                new("-", Shortcut.None, Key.None),
                new("Lua Console|Ctrl+Shift+9", new Shortcut(Key.D9, true, true), Key.L,
                    omitIfNoCommand: true, commandId: OpenLuaConsole)
            },
        },


        new()
        {
            Key = "PEDIA", Defaults = new List<MenuElement>
            {
                new("&Civilopedia", Shortcut.None, Key.C),
                new("Civilization &Advances", Shortcut.None, Key.A, commandId: CivilopediaAdvances),
                new("City &Improvements", Shortcut.None, Key.I, commandId: CivilopediaImprovements),
                new("&Wonders of the World", Shortcut.None, Key.W, commandId: CivilopediaWonders),
                new("Military &Units", Shortcut.None, Key.U, commandId: CivilopediaUnits),
                new("-", Shortcut.None, Key.None),
                new("&Governments", Shortcut.None, Key.G, commandId: CivilopediaGovernments),
                new("&Terrain Types", Shortcut.None, Key.T, commandId: CivilopediaTerrain),
                new("-", Shortcut.None, Key.None),
                new("Game &Concepts", Shortcut.None, Key.C, commandId: CivilopediaConcepts),
                new("-", Shortcut.None, Key.None),
                new("&About rhYciv", Shortcut.None, Key.A, commandId: AboutGame)
            },
        }
    ];

    public override int UnitsRows => 7;
    public override int UnitsPxHeight => 48;
    public override Dictionary<string, IImageSource[]> PicSources { get; } = new();

    public override ListboxLooks GetListboxLooks(ListboxType? type)
    {
        return type switch
        {
            ListboxType.Default => new ListboxLooks
            {
                BoxBackgroundColor = new Color(207, 207, 207, 255),
                BoxLineColor = new Color(67, 67, 67, 255),
                Font = Look.DefaultFont,
                FontSize = 21,
                TextColorFront = Color.Black,
                TextColorShadow = Color.Blank,
                SelectedTextFont = Fonts.TnRbold,
                SelectedTextBackgroundColor = new Color(107, 107, 107, 255),
                SelectedTextColorFront = Color.White,
                SelectedTextColorShadow = Color.Black
            },
            ListboxType.Civilopedia => new ListboxLooks
            {
                BoxBackgroundColor = new Color(240, 240, 240, 255),
                BoxLineColor = new Color(100, 100, 100, 255),
                Font = Look.DefaultFont,
                FontSize = Look.CivilopediaFontSize,
                TextColorShadow = Color.Blank
            },
            _ => new ListboxLooks(),
        };
    }

    public override List<CityViewTiles> GetCityViewTiles()
    {
#if LEGACY_CIV2_CITY_VIEW
        return
        [
            new(0, 62, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(160, 116, 158, 114)), new(4, 0), 2),   // manhattan project
            new(1, 24, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(497, 84, 123, 82)), new(165, 47), 15),   // supermarket
            new(2, 9, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 1, 123, 82)), new(267, 10), 2),   // aqueduct
            new(3, 49, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(319, 116, 158, 114)), new(401, 10), 5),   // michel. chapel
            new(4, 5, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(125, 84, 123, 82)), new(556, 5), 15),   // marketplace
            new(5, 26, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(373, 84, 123, 82)), new(653, 1), 14),   // research lab
            new(6, 50, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(478, 231, 158, 114)), new(728, 9), 2),   // copernicus obs.
            new(7, 54, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(319, 346, 158, 114)), new(850, 9), 2),   // j.s.bach's
            new(8, 7, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(125, 1, 123, 82)), new(980, 7), 1),   // courthouse
            new(9, 32, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 167, 123, 82)), new(1116, 7), 0),   // airport
            new(10, 16, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 84, 123, 82)), new(60, 110), 13),   // mfg plant
            new(11, 18, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 416, 123, 82)), new(170, 100), 12),   // recycl. center
            new(12, 17, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(497, 167, 123, 82)), new(268, 100), 15),   // sdi defense
            new(13, 27, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(125, 250, 123, 82)), new(370, 100), 13),   // sam battery
            new(14, 3, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(373, 1, 123, 82)), new(514, 63), 18),   // granary
            new(15, 4, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 333, 123, 82)), new(620, 67), 15),   // temple
            new(16, 39, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 576, 158, 114)), new(460, 120), 20),   // pyramids
            new(17, 56, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(160, 576, 158, 114)), new(586, 100), 15),   // adam smith's
            new(18, 1, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(373, 333, 123, 82)), new(676, 114), 14),   // palace
            new(19, 11, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(373, 250, 123, 82)), new(775, 105), 12),   // cathedral
            new(20, 14, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(249, 167, 123, 82)), new(870, 100), 13),   // colosseum
            new(21, 21, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(249, 84, 123, 82)), new(960, 114), 15),   // nucl. plant
            new(22, 66, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(478, 576, 158, 114)), new(1062, 105), 14),   // cure cancer
            new(23, 40, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 116, 158, 114)), new(1162, 100), 12),   // hang. gardens
            new(24, 53, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 461, 158, 114)), new(95, 150), 15),   // leonardo's wrk.
            new(25, 57, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 346, 158, 114)), new(192, 150), 15),   // darwin's voy.
            new(26, 10, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(125, 167, 123, 82)), new(290, 150), 14),   // bank
            new(27, 2, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(249, 250, 123, 82)), new(537, 153), 19),   // barracks
            new(28, 12, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(373, 167, 123, 82)), new(533, 200), 19),   // university
            new(29, 47, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(478, 461, 158, 114)), new(658, 156), 4),   // king richard
            new(30, 61, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(319, 1, 158, 114)), new(780, 156), 2),   // hoover dam
            new(31, 15, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(249, 1, 123, 82)), new(916, 167), 15),   // factory
            new(32, 60, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(160, 1, 158, 114)), new(1036, 175), 0),   // women suffrage
            new(33, 28, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 250, 123, 82)), new(1160, 213), 14),   // coastal fort.
            new(34, 22, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(125, 416, 123, 82)), new(0, 213), 2),   // stock exch.
            new(35, 23, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(497, 250, 123, 82)), new(110, 260), 14),   // sewer system
            new(36, 29, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 618, 123, 82)), new(210, 226), 3),   // solar plant
            new(37, 43, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(478, 1, 158, 114)), new(332, 226), 2),   // great library
            new(38, 44, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(478, 116, 158, 114)), new(450, 256), 7),   // oracle
            new(39, 52, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 231, 158, 114)), new(572, 256), 7),   // shakespeare th.
            new(40, 6, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(497, 1, 123, 82)), new(735, 250), 20),   // library
            new(41, 13, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(125, 618, 123, 82)), new(845, 259), 15),   // mass transit
            new(42, 59, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(319, 576, 158, 114)), new(10, 295), 3),   // eiffel tower
            new(43, 19, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(125, 333, 123, 82)), new(167, 287), 3),   // power plant
            new(44, 20, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(249, 333, 123, 82)), new(293, 287), 1),   // hydro plant
            new(45, 55, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(160, 346, 158, 114)), new(0, 364), 2),   // Is. Newton's
            new(46, 63, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(160, 231, 158, 114)), new(129, 356), 1),  // untd. nations
            new(47, 51, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(478, 346, 158, 114)), new(250, 356), 2),  // magellan exp.
            new(48, 48, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(319, 461, 158, 114)), new(411, 324), 4),  // m.polo embassy
            new(49, 64, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 1, 158, 114)), new(533, 324), 1),   // apollo program
            new(50, 65, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(319, 231, 158, 114)), new(680, 284), 3),   // seti program
            // Draw altern. tile where ocean/river is (continental only):
            new(51, -1, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(0, 0, 0, 0)), new(928, 273), 12),
            new(52, -1, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(0, 0, 0, 0)), new(1020, 256), 15),
            new(53, -1, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(0, 0, 0, 0)), new(1156, 286), 2),
            new(54, -1, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(0, 0, 0, 0)), new(1043, 328), 2),
            // Draw altern. tile where ocean is (continental & river):
            new(55, -1, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(0, 0, 0, 0)), new(1155, 396), 13),
            new(56, 8, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(249, 416, 357, 78)), new(368, 390), 0),    // city walls
            new(57, 31, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(590, 499, 105, 105)), new(926, 366), 0),   // offshore platf.
            new(58, 30, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(1, 499, 220, 100)), new(907, 274), 0),   // harbor
            new(59, 34, new BinaryStorage("cv.dll", 0x1E6E0, 0x24C0F, new(222, 499, 367, 118)), new(907, 296), 0),   // port facility
            new(60, 41, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(407, 852, 94, 160)), new(1070, 319), 0),   // colossus (sea)
            new(61, 41, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(502, 852, 94, 160)), new(1070, 319), 0),   // colossus (sea)
            new(62, 42, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(253, 852, 76, 133)), new(1184, 305), 0),   // lighthouse (sea)
            new(63, 42, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(330, 852, 76, 133)), new(1184, 305), 0),   // lighthouse (land)
            new(64, 45, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 691, 304, 160)), new(0, 0), 0),   // great wall
            new(65, 45, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(306, 691, 304, 160)), new(0, 0), 0),   // great wall (alt.)
            new(66, 58, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(1, 852, 125, 253)), new(0, 0), 0),   // statue liberty (sea)
            new(67, 58, new BinaryStorage("cv.dll", 0x432F0, 0x35C79, new(127, 852, 125, 253)), new(0, 0), 0),   // statue liberty (land)
        ];
#else
        // The standalone city panorama keeps the terrain backdrop and normal
        // city controls without loading proprietary cv.dll scenery layers.
        return [];
#endif
    }

    public override List<BinaryStorage> GetCityViewAltTiles()
    {
#if LEGACY_CIV2_CITY_VIEW
        return
        [
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(1, 1, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(160, 1, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(319, 1, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(478, 1, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(1, 116, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(160, 116, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(319, 116, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(478, 116, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(1, 231, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(160, 231, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(319, 231, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(478, 231, 158, 114)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(1, 346, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(125, 346, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(249, 346, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(373, 346, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(497, 346, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(1, 429, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(125, 429, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(249, 429, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(373, 429, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(497, 429, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(1, 512, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(125, 512, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(249, 512, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(373, 512, 123, 82)),
            new BinaryStorage("cv.dll", 0x78F6C, 0x242E4, new(497, 512, 123, 82))
        ];
#else
        return [];
#endif
    }

    public override void GetShieldImages()
    {
        Color shadowColour = new(51, 51, 51, 255);
        Color replacementColour = new(255, 0, 0, 255);

        // Keep the original Civ II silhouette as the logical layout source.  The
        // Raylib renderer supersamples the complete front/back/shadow set, so
        // decoding a 1254px external outline here only increased startup memory
        // and produced a less faithful shape.
        var shield = Images.ExtractBitmap(PicSources["backShield1"][0], this).Copy();

        if (shield.Width < 64 && shield.Height < 64 && shield.Width >= 12 && shield.Height >= 15)
        {
            shield.Crop(new Rectangle(1, 0, Math.Min(11, shield.Width - 1), Math.Min(15, shield.Height)));
        }

        const int targetWidth = 18;
        const int targetHeight = 24;
        if (shield.Width != targetWidth || shield.Height != targetHeight)
        {
            shield.Resize(targetWidth, targetHeight);
        }

        var shieldFront = shield.Copy();
        var shieldBack = shield.Copy();
        var shieldShadow = shield.Copy();
        shield.Unload();

        // Give transparent outline-only PNGs a Civ2-style player-colour body,
        // while keeping the upper band clean for the HP bar.
        shieldFront.DrawRectangle(3, 9, targetWidth - 6, targetHeight - 12, replacementColour);
        shieldFront.DrawRectangle(2, 2, targetWidth - 4, 7, new Color(16, 16, 16, 255));
        shieldBack.DrawRectangle(3, 5, targetWidth - 6, targetHeight - 8, replacementColour);

        shieldShadow.ReplaceColor(replacementColour, shadowColour);

        UnitImages.Shields = new MemoryStorage(shieldFront, "Unit-Shield", replacementColour);
        UnitImages.ShieldBack = new MemoryStorage(shieldBack, "Unit-Shield-Back", replacementColour, true);
        UnitImages.ShieldShadow = new MemoryStorage(shieldShadow, "Unit-Shield-Shadow", replacementColour);
    }

    /// <summary>
    /// The most common opaque colour in an image, which for a flag is its cloth.
    /// </summary>
    private Color DominantColour(IImageSource source, Color fallback)
    {
        var colours = Images.ExtractBitmap(source, this).LoadColors();
        var counts = new Dictionary<int, int>();
        foreach (var colour in colours)
        {
            if (colour.A < 160)
            {
                continue;
            }

            var key = (colour.R << 16) | (colour.G << 8) | colour.B;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        Image.UnloadColors(colours);

        if (counts.Count == 0)
        {
            return fallback;
        }

        var best = counts.MaxBy(pair => pair.Value).Key;
        return new Color((byte)(best >> 16), (byte)((best >> 8) & 0xFF), (byte)(best & 0xFF), (byte)255);
    }

    private static Color Darken(Color colour) =>
        new((byte)(colour.R * 0.55f), (byte)(colour.G * 0.55f), (byte)(colour.B * 0.55f), (byte)255);

    public override void LoadPlayerColours()
    {
        var playerColours = new PlayerColour[9];
        for (var col = 0; col < 9; col++)
        {
            var imageColours = Images.ExtractBitmap(PicSources["textColours"][col], this).LoadColors();
            var textColour = imageColours[0];

            // Take the flag's own dominant colour rather than one fixed pixel. The
            // classic sheet had the cloth filling its box, so row three column eight
            // was always on it; this art set is scaled and bottom-anchored inside a
            // 14x22 slot, so that pixel is transparent for most flags and every unit
            // shield came out black.
            var lightColour = DominantColour(PicSources["flags"][col], textColour);
            var darkColour = DominantColour(PicSources["flags"][9 + col], Darken(lightColour));

            var classicFlag = Images.ExtractBitmap(PicSources["flags"][col], this);
            playerColours[col] = new PlayerColour
            {
                Image = PicSources["flags"][col],
                MapImage = GetFossArtFlagImage(textColour),
                LogicalSize = new Vector2(classicFlag.Width, classicFlag.Height),
                TextColour = textColour,
                LightColour = lightColour,
                DarkColour = darkColour
            };
        }
        PlayerColours = playerColours;
    }

    public override UnitShield UnitShield(int unitType) => new()
    {
        ShieldInFrontOfUnit = false,
        // Up and to the left of the unit. The flag anchor sits at the foot of the
        // 64x48 unit box, so hanging the 18x24 shield off it put the shield below
        // the box entirely, over whatever the tile beneath was showing. Everything
        // else - the health bar, the orders letter, the stack and shadow copies -
        // is placed relative to this, so they all move with it.
        Offset = new Vector2(2, 4),
        StackingOffset = new(UnitImages.Units[unitType].FlagLoc.X < UnitImages.UnitRectangle.Width / 2 ? -3 : 5, 0),
        ShadowOffset = new(UnitImages.Units[unitType].FlagLoc.X < UnitImages.UnitRectangle.Width / 2 ? 0 : 1, 1),
        DrawShadow = true,
        HPbarOffset = new(3, 3),
        HPbarSize = new(12, 5),
        HPbarColours = [new Color(243, 0, 0, 255), new Color(255, 223, 79, 255), new Color(87, 171, 39, 255)],
        HPbarSizeForColours = [4, 9],
        OrderOffset = new(9f, 11),
        OrderTextHeight = 13,
    };

    /// <summary>
    /// Draw outer border wallpaper around panel
    /// </summary>
    /// <param name="wallpaper">Wallpaper image to tile onto the border</param>
    /// <param name="destination">the Image we're rendering to</param>
    /// <param name="height">final image Height</param>
    /// <param name="width">final image Width</param>
    /// <param name="padding">padding of borders</param>
    /// <param name="statusPanel">is this status panel?</param>
    public override void DrawBorderWallpaper(Wallpaper wallpaper, ref Image destination, int height, int width, Padding padding, bool statusPanel)
    {
        DrawUtils.TileFill([wallpaper.Outer], ref destination, new Rectangle(0, 0, width, padding.Top));
        DrawUtils.TileFill([wallpaper.Outer], ref destination, new Rectangle(0, padding.Top, padding.Left, height - padding.Top - padding.Bottom));
        DrawUtils.TileFill([wallpaper.Outer], ref destination, new Rectangle(width - padding.Right, padding.Top, padding.Right, height - padding.Top - padding.Bottom));
        DrawUtils.TileFill([wallpaper.Outer], ref destination, new Rectangle(0, height - padding.Bottom, width, padding.Bottom));

        if (statusPanel)
        {
            var columns = (width - padding.Left - padding.Right) / wallpaper.Outer.Width + 1;
            var sourceRec = new Rectangle { Height = 4, Width = wallpaper.Outer.Width };
            for (var col = 0; col < columns; col++)
            {
                destination.Draw(wallpaper.Outer, sourceRec,
                    new Rectangle(col * wallpaper.Outer.Width, padding.Top + 62, wallpaper.Outer.Width, 4), Color.White);
            }
        }
    }

    public override void DrawBorderLines(ref Image destination, int height, int width, Padding padding, bool statusPanel)
    {
        // Outer border
        var pen1 = new Color(227, 227, 227, 255);
        var pen2 = new Color(105, 105, 105, 255);
        var pen3 = new Color(255, 255, 255, 255);
        var pen4 = new Color(160, 160, 160, 255);
        var pen5 = new Color(240, 240, 240, 255);
        var pen6 = new Color(223, 223, 223, 255);
        var pen7 = new Color(67, 67, 67, 255);
        destination.DrawLine(0, 0, width - 2, 0, pen1); // 1st layer of border
        destination.DrawLine(0, 0, width - 2, 0, pen1);
        destination.DrawLine(0, 0, 0, height - 2, pen1);
        destination.DrawLine(width - 1, 0, width - 1, height - 1, pen2);
        destination.DrawLine(0, height - 1, width - 1, height - 1, pen2);
        destination.DrawLine(1, 1, width - 3, 1, pen3); // 2nd layer of border
        destination.DrawLine(1, 1, 1, height - 3, pen3);
        destination.DrawLine(width - 2, 1, width - 2, height - 2, pen4);
        destination.DrawLine(1, height - 2, width - 2, height - 2, pen4);
        destination.DrawLine(2, 2, width - 4, 2, pen5); // 3rd layer of border
        destination.DrawLine(2, 2, 2, height - 4, pen5);
        destination.DrawLine(width - 3, 2, width - 3, height - 3, pen5);
        destination.DrawLine(2, height - 3, width - 3, height - 3, pen5);
        destination.DrawLine(3, 3, width - 5, 3, pen6); // 4th layer of border
        destination.DrawLine(3, 3, 3, height - 5, pen6);
        destination.DrawLine(width - 4, 3, width - 4, height - 4, pen7);
        destination.DrawLine(3, height - 4, width - 4, height - 4, pen7);
        destination.DrawLine(4, 4, width - 6, 4, pen6); // 5th layer of border
        destination.DrawLine(4, 4, 4, height - 6, pen6);
        destination.DrawLine(width - 5, 4, width - 5, height - 5, pen7);
        destination.DrawLine(4, height - 5, width - 5, height - 5, pen7);

        // Inner panel
        destination.DrawLine(9, padding.Top - 1, 9 + (width - 18 - 1), padding.Top - 1, pen7); // 1st layer of border
        if (!statusPanel)
        {
            // 1st layer of border
            destination.DrawLine(10, padding.Top - 1, 10, height - padding.Bottom - 1, pen7);
            destination.DrawLine(width - 11, padding.Top - 1, width - 11, height - padding.Bottom - 1, pen6);
            destination.DrawLine(9, height - padding.Bottom, width - 9 - 1, height - padding.Bottom, pen6);
            destination.DrawLine(10, padding.Top - 2, 9 + (width - 18 - 2), padding.Top - 2, pen7); // 2nd layer of border
            destination.DrawLine(9, padding.Top - 2, 9, height - padding.Bottom, pen7);
            destination.DrawLine(width - 10, padding.Top - 2, width - 10, height - padding.Bottom, pen6);
            destination.DrawLine(9, height - padding.Bottom + 1, width - 9 - 1, height - padding.Bottom + 1, pen6);
        }
        else
        {
            // 1st layer of border
            destination.DrawLine(9, padding.Top + 67, 9 + (width - 18 - 1), padding.Top + 67, pen7);
            destination.DrawLine(10, padding.Top - 1, 10, padding.Top + 59, pen7);
            destination.DrawLine(10, padding.Top + 66, 10, height - padding.Bottom - 1, pen7);
            destination.DrawLine(width - 11, padding.Top - 1, width - 11, padding.Top + 61, pen6);
            destination.DrawLine(width - 11, padding.Top + 67, width - 11, height - padding.Bottom - 1, pen6);
            destination.DrawLine(10, height - padding.Bottom, width - 9 - 1, height - padding.Bottom, pen6);
            destination.DrawLine(10, padding.Top + 60, width - 9 - 1, padding.Top + 60, pen6);
            destination.DrawLine(10, padding.Top - 2, 9 + (width - 18 - 2), padding.Top - 2, pen7); // 2nd layer of border
            destination.DrawLine(10, padding.Top + 66, 9 + (width - 18 - 2), padding.Top + 66, pen7);
            destination.DrawLine(9, padding.Top - 2, 9, padding.Top + 60, pen7);
            destination.DrawLine(9, padding.Top + 66, 9, height - padding.Bottom, pen7);
            destination.DrawLine(width - 10, padding.Top - 2, width - 10, padding.Top + 59, pen6);
            destination.DrawLine(width - 10, padding.Top + 66, width - 10, height - padding.Bottom - 1, pen6);
            destination.DrawLine(9, height - padding.Bottom + 1, width - 9 - 1, height - padding.Bottom + 1, pen6);
            destination.DrawLine(9, padding.Top + 61, width - 9 - 1, padding.Top + 61, pen6);
        }
    }

    public override void DrawButton(Texture2D texture, Rectangle bounds)
    {
        var x = (int)bounds.X;
        var y = (int)bounds.Y;
        var w = (int)bounds.Width;
        var h = (int)bounds.Height;

        Graphics.DrawRectangleLinesEx(bounds, 1.0f, new Color(100, 100, 100, 255));
        Graphics.DrawRectangleRec(new Rectangle(x + 1, y + 1, w - 2, h - 2), Color.White);
        Graphics.DrawRectangleRec(new Rectangle(x + 3, y + 3, w - 6, h - 6), new Color(192, 192, 192, 255));
        Graphics.DrawLine(x + 2, y + h - 2, x + w - 2, y + h - 2, new Color(128, 128, 128, 255));
        Graphics.DrawLine(x + 3, y + h - 3, x + w - 2, y + h - 3, new Color(128, 128, 128, 255));
        Graphics.DrawLine(x + w - 1, y + 2, x + w - 1, y + h - 1, new Color(128, 128, 128, 255));
        Graphics.DrawLine(x + w - 2, y + 3, x + w - 2, y + h - 1, new Color(128, 128, 128, 255));
    }
    
    /// <summary>
    /// Identifies rulesets served by this interface. It is written into save files
    /// and matched back on load, so <see cref="LegacyRulesetMetadataKey"/> has to
    /// keep resolving for saves written before the defork.
    /// </summary>
    public const string RulesetMetadataKey = "Compact";

    /// <summary>Pre-defork name of <see cref="RulesetMetadataKey"/>.</summary>
    public const string LegacyRulesetMetadataKey = "Civ2Gold";

    protected override IEnumerable<Ruleset> GenerateRulesets(string path, string title)
    {
        var rules = FileUtilities.GetFile(path, "rules.txt");
        if (rules != null)
        {
            yield return new Ruleset(title, new Dictionary<string, string>
            {
                { RulesetMetadataKey, "Standard" }
            }, path);

            foreach (var subdirectory in Directory.EnumerateDirectories(path))
            {
                var scnRules = FileUtilities.GetFile(subdirectory, "rules.txt");
                if (scnRules != null)
                {

                    var game = Utils.GetFilePath("game.txt", [subdirectory]);
                    var name = "";
                    if (File.Exists(game))
                    {
                        foreach (var line in File.ReadLines(game))
                        {
                            if (!line.StartsWith("@title")) continue;
                            name = line[7..];
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = Path.GetFileName(subdirectory);
                    }

                    yield return new Ruleset(name, new Dictionary<string, string>
                    {

                        { RulesetMetadataKey, "Scenario-" + name }
                    }, subdirectory, path);
                }
            }
        }
    }
}
