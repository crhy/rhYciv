using System.Numerics;
using Model.Controls;
using Model.Images;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Transformations;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;

namespace RhyCiv.UI.Classic.CityWindow;

/// <summary>
/// The city window's layout, stated as plain numbers in the window's own
/// coordinates instead of as positions read off Civ II's 640x421 "city" sheet.
/// <para>
/// The classic layout anchored every label, button and panel to a backdrop image
/// the standalone art set does not ship. With the picture missing there was nothing
/// for the labels to sit clear of, and the regions it was positioned against were
/// recorded nowhere else in the code, so the numbers were meaningless on their own:
/// "Citizens", "Resource Map", "Units Supported", "Units Present" and the orange
/// "Supplies"/"Demands" pair landed on top of each other and on top of the boxes
/// they were naming (issue #45).
/// </para>
/// <para>
/// Here every rectangle is declared in one coordinate space -- the window's, origin
/// at (0,0), the space the city screen already paints its stone panels in -- and
/// each heading is placed clear of the box it heads. The invariants that make the
/// reported overlap impossible (nothing outside the window, no label over another
/// label or over the body of its own box, the panels pairwise disjoint, and the
/// key set the client reads by name intact) are asserted by
/// RhyCiv.Tests/Cities/CityWindowLayoutTests.cs.
/// </para>
/// </summary>
public static class StandaloneCityWindowLayout
{
    // The window is the size the classic sheet was, because the inset panels the
    // city screen paints (CityWindow.Panels) are cut for that sheet and they are
    // the part of the old layout that was right. Only the labels were anchored to
    // the picture that is not there.
    private const int WindowWidth = 636;
    private const int WindowHeight = 421;

    // Section headings. The classic city dialog was dark, so its headings were
    // gold on near-black. This build's panel is light stone and gold on it is all
    // but invisible, so the headings are dark with a light shadow instead.
    private static readonly Color HeadingColour = new(46, 34, 16, 255);
    private static readonly Color HeadingShadow = new(245, 240, 226, 200);

    // The two caravan lines are orange in Civ II and stay orange here; their
    // shadow is the grey the classic dialog used.
    private static readonly Color CaravanColour = new(227, 83, 15, 255);
    private static readonly Color CaravanShadow = new(67, 67, 67, 255);

    public static CityWindowLayout Create()
    {
        var layout = new CityWindowLayout(NoBackdrop())
        {
            Width = WindowWidth,
            Height = WindowHeight,

            // The strip of citizens along the top, inside the panel painted at
            // (3, 0, 433, 44).
            CitizensBox = new Rectangle(5, 2, 429, 40),

            // The resource map: four tiles across, inside the panel painted at
            // (5, 62, 192, 126). It stops short of the panel's bottom so the
            // "Resource Map" heading under it sits on the stone, not on the map.
            TileMap = new Rectangle(7, 65, 188, 120),

            InfoPanel = new InfoPanel
            {
                // The panel on the right of the units supported box, and the
                // "Info"/"Map"/"Rename" buttons below it.
                Box = new Rectangle(193, 227, 242, 186),
                UnitsPresent = new UnitBox
                {
                    // Below the panel's heading, not on top of it: the box used to
                    // share its origin with the "Units Present" label, so the first
                    // row of units was drawn over the words.
                    Box = new Rectangle(196, 242, 236, 84),
                    Rows = 2,
                    Columns = 5
                }
            },

            UnitSupport = new UnitBox
            {
                // Likewise clear of its own heading, and one row rather than two:
                // the panel is 58 tall, so two rows left each unit half the height
                // of one in the Units Present box beside it.
                Box = new Rectangle(7, 229, 184, 54),
                Rows = 1,
                Columns = 4
            },

            Improvements = new ImprovementsBox
            {
                Box = new Rectangle(7, 306, 170, 108),
                Rows = 9,
                LabelColor = Color.White,
                LabelColorShadow = Color.Black,
                ShadowOffset = new Vector2(1, 0)
            },

            // The granary: the panel painted at (437, 13, 195, 150), with its
            // heading above the panel's top edge rather than inside it.
            FoodStorage = new Rectangle(437, 13, 195, 150),

            Production = new()
            {
                Type = "Box",
                // The panel painted at (437, 207, 195, 145).
                Box = new Rectangle(437, 207, 195, 145),
                // Relative to the production box, which is how the client places
                // the item being built inside the panel it has already positioned.
                IconLocation = new(97.5f, 18)
            },

            Resources = new ResourceProduction
            {
                // The four accounts down the middle column, inside the panel painted
                // at (196, 44, 240, 166). Spaced by their own height so the bands
                // never touch, and clear of the caravan lines below them.
                TitlePosition = new Rectangle(199, 46, 238, 12),
                Resources =
                [
                    new ConsumableResourceArea(name: "Food",
                        bounds: new Rectangle(203, 60, 230, 13),
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
                        bounds: new Rectangle(206, 92, 224, 16),
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
                        bounds: new Rectangle(199, 124, 238, 16),
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
                        labelBelow: true)
                    {
                        BarTop = new Color(118, 148, 240, 255),
                        BarBottom = new Color(14, 26, 88, 255)
                    },
                    new SharedResourceArea(new Rectangle(206, 156, 224, 16), true)
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
                                Icon = ClassicInterface.ResourceIcon("gold")
                            },
                            new()
                            {
                                Name = "Lux",
                                GetResourceLabel = (val, city) =>
                                    city.Owner.LuxRate + "% " + Labels.For(LabelIndex.Lux) + ":" + val,
                                Icon = ClassicInterface.ResourceIcon("lux")
                            },
                            new()
                            {
                                Name = "Science",
                                GetResourceLabel = (val, city) =>
                                    city.Owner.LuxRate + "% " + Labels.For(LabelIndex.Sci) + ":" + val,
                                Icon = ClassicInterface.ResourceIcon("science")
                            }
                        ]
                    }
                ]
            }
        };

        // The buttons. Buy and Change sit at the top of the production panel, where
        // the client lays them out against that panel; the other six are the row
        // along the bottom right of the window.
        const float buttonHeight = 24;
        const float buyButtonWidth = 68;
        const int infoButtonWidth = 57;

        layout.Buttons.Add("Buy", new(Labels.For(LabelIndex.Buy), new(442, 223, buyButtonWidth, buttonHeight)));
        layout.Buttons.Add("Change", new(Labels.For(LabelIndex.Change), new(557, 223, buyButtonWidth, buttonHeight)));
        layout.Buttons.Add("Info", new(Labels.For(LabelIndex.Info), new(459, 364, infoButtonWidth, buttonHeight)));
        layout.Buttons.Add("Map", new(Labels.For(LabelIndex.Map), new(517, 364, infoButtonWidth, buttonHeight)));
        layout.Buttons.Add("Rename", new(Labels.For(LabelIndex.Rename), new(575, 364, infoButtonWidth, buttonHeight)));
        layout.Buttons.Add("Happy", new(Labels.For(LabelIndex.Happy), new(459, 389, infoButtonWidth, buttonHeight)));
        layout.Buttons.Add("View", new(Labels.For(LabelIndex.View), new(517, 389, infoButtonWidth, buttonHeight)));
        layout.Buttons.Add("Exit", new("Exit", new(575, 389, infoButtonWidth, buttonHeight)));

        // Headings. Each one names the box below it and stops short of that box's
        // top edge, which is what the classic numbers did not guarantee.
        layout.Labels.Add("Citizens", new(Labels.For(LabelIndex.Citizens), new(5, 46, 189, 12),
            HeadingColour, HeadingShadow));
        layout.Labels.Add("ResourceMap", new(Labels.For(LabelIndex.ResourceMap), new(5, 187, 189, 12),
            HeadingColour, HeadingShadow));
        layout.Labels.Add("UnitsSupported", new(Labels.For(LabelIndex.UnitsSupported), new(7, 215, 184, 12),
            HeadingColour, HeadingShadow));
        layout.Labels.Add("UnitsPresent", new(Labels.For(LabelIndex.UnitsPresent), new(196, 229, 236, 12),
            HeadingColour, HeadingShadow));
        layout.Labels.Add("CityImprovements", new(Labels.For(LabelIndex.CityImprovements), new(7, 292, 170, 12),
            HeadingColour, HeadingShadow));
        layout.Labels.Add("FoodStorage", new(Labels.For(LabelIndex.FoodStorage), new(437, 0, 195, 12),
            new Color(75, 155, 35, 255), Color.Black));
        layout.Labels.Add("ItemInProduction", new("", new(442, 193, 185, 12),
            new Color(63, 79, 167, 255), Color.Black));

        // The two caravan lines, and the trade routes the client writes under
        // "Demands" at the same spacing. They are the bottom of the info panel,
        // inset by 5 on both sides so the text does not render hard against its
        // border, and they stop short of the panel's bottom edge even with three
        // routes below them.
        layout.Labels.Add("Supplies", new(Labels.For(LabelIndex.Supplies), new(198, 345, 232, 12),
            CaravanColour, CaravanShadow));
        layout.Labels.Add("Demands", new(Labels.For(LabelIndex.Demands), new(198, 358, 232, 12),
            CaravanColour, CaravanShadow));

        return layout;
    }

    /// <summary>
    /// The sentinel the city screen already tolerates for "this window has no
    /// backdrop".
    /// <para>
    /// The classic layout asked for a 640x421 "city" sheet by name, which is a
    /// lookup that fails on every frame in a standalone build -- it is what
    /// hard-crashed the game the moment a city screen was opened. The standalone
    /// window has no backdrop to find: its face is the painted dialog base plus
    /// the stone panels the client draws itself. A computed image says exactly
    /// that, and the client's GetBackdrop resolves it once to "no backdrop".
    /// </para>
    /// </summary>
    private static IImageSource NoBackdrop() => new ComputedImage();
}
