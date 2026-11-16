using System.Text.RegularExpressions;
using Model.Controls;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.UI.Classic.CityWindow;
using Raylib_CSharp.Transformations;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// The city window's layout has to hold together without the classic backdrop.
/// <para>
/// The classic layout positioned every label, button and panel against Civ II's
/// 640x421 "city" sheet, which the standalone art set does not ship. With the
/// picture missing the numbers had nothing to line up against, and the headings
/// landed on top of each other and on top of the boxes they named -- "Citizens",
/// "Resource Map", "Units Supported", "Units Present" and the orange "Supplies"/
/// "Demands" pair (issue #45).
/// </para>
/// <para>
/// The standalone layout declares every rectangle in the window's own coordinates,
/// so the properties that made that overlap impossible can be checked here instead
/// of by opening the window: nothing outside the window, no heading over another
/// heading or over the body of the box it heads, the panels pairwise disjoint, and
/// the key set the client reads by name intact -- a missing key throws at CityWindow
/// .cs:122 the moment a city screen opens.
/// </para>
/// </summary>
public class CityWindowLayoutTests
{
    // Every rectangle the layout names, under the name the client uses for it.
    private static (string Name, Rectangle Box)[] NamedRects(CityWindowLayout layout)
    {
        var rects = new List<(string Name, Rectangle Box)>
        {
            ("TileMap", layout.TileMap),
            ("InfoPanel", layout.InfoPanel.Box),
            ("InfoPanel.UnitsPresent", layout.InfoPanel.UnitsPresent.Box),
            ("Production", layout.Production.Box),
            ("FoodStorage", layout.FoodStorage),
            ("Improvements", layout.Improvements.Box),
            ("UnitSupport", layout.UnitSupport.Box),
            ("CitizensBox", layout.CitizensBox),
            ("Resources.TitlePosition", layout.Resources.TitlePosition)
        };

        foreach (var label in layout.Labels)
        {
            rects.Add(($"Label {label.Key}", label.Value.Box));
        }

        foreach (var button in layout.Buttons)
        {
            rects.Add(($"Button {button.Key}", button.Value.Box));
        }

        for (var i = 0; i < layout.Resources.Resources.Count; i++)
        {
            rects.Add(($"Resources[{i}]", layout.Resources.Resources[i].Bounds));
        }

        return rects.ToArray();
    }

    // The heading that names each box, and the box it names. A heading may touch
    // the top edge of its box; anything lower is drawn over what the box shows.
    private static (string Heading, string Box, Rectangle Body)[] Headings(CityWindowLayout layout) =>
    [
        ("Citizens", "CitizensBox", layout.CitizensBox),
        ("ResourceMap", "TileMap", layout.TileMap),
        ("UnitsPresent", "InfoPanel.UnitsPresent", layout.InfoPanel.UnitsPresent.Box),
        ("UnitsSupported", "UnitSupport", layout.UnitSupport.Box),
        ("CityImprovements", "Improvements", layout.Improvements.Box),
        ("FoodStorage", "FoodStorage", layout.FoodStorage),
        ("ItemInProduction", "Production", layout.Production.Box)
    ];

    // The inset panels the city screen paints. They are the window's structure,
    // so one over another is a hole in it.
    private static (string Name, Rectangle Box)[] Panels(CityWindowLayout layout) =>
    [
        ("TileMap", layout.TileMap),
        ("InfoPanel", layout.InfoPanel.Box),
        ("Production", layout.Production.Box),
        ("FoodStorage", layout.FoodStorage),
        ("Improvements", layout.Improvements.Box),
        ("UnitSupport", layout.UnitSupport.Box),
        ("CitizensBox", layout.CitizensBox)
    ];

    private static CityWindowLayout Create()
    {
        // The layout asks for its headings by index out of the loaded label table,
        // so the table has to be at least as long as the highest index it names.
        // Same filler the other tests use; the text is not what is under test here.
        if (Labels.Items is not { } items || items.Length <= (int)LabelIndex.ResourceMap)
        {
            Labels.Items = Enumerable.Range(0, 1000).Select(i => $"Label {i}").ToArray();
        }

        return StandaloneCityWindowLayout.Create();
    }

    [Fact]
    public void EveryRectTheLayoutNamesFitsInsideTheWindow()
    {
        var layout = Create();
        Assert.True(layout.Width > 0 && layout.Height > 0);

        var named = NamedRects(layout);
        Assert.NotEmpty(named);

        var outside = named
            .Where(rect => !FitsInside(rect.Box, layout.Width, layout.Height))
            .Select(rect => $"{rect.Name} {rect.Box} in {layout.Width}x{layout.Height}")
            .ToList();

        Assert.True(outside.Count == 0,
            "These lie outside the window, so they would be clipped or would push their " +
            "controls out of the panel:\n  " + string.Join("\n  ", outside));
    }

    [Fact]
    public void NoLabelSitsOnTopOfAnotherLabel()
    {
        var layout = Create();
        var labels = layout.Labels.ToList();

        var clashes = new List<string>();
        for (var i = 0; i < labels.Count; i++)
        {
            for (var j = i + 1; j < labels.Count; j++)
            {
                if (Overlaps(labels[i].Value.Box, labels[j].Value.Box))
                {
                    clashes.Add($"\"{labels[i].Key}\" {labels[i].Value.Box} over " +
                                $"\"{labels[j].Key}\" {labels[j].Value.Box}");
                }
            }
        }

        Assert.True(clashes.Count == 0,
            "Two headings in the same pixels is the overlap the issue screenshots show:\n  " +
            string.Join("\n  ", clashes));
    }

    [Fact]
    public void AHeadingNeverCoversTheBodyOfTheBoxItHeads()
    {
        var layout = Create();

        foreach (var (heading, boxName, body) in Headings(layout))
        {
            Assert.True(layout.Labels.ContainsKey(heading), $"The layout has no \"{heading}\" label");
            var label = layout.Labels[heading];

            // The first row of units used to be drawn over the words because the
            // box shared its origin with the heading. Touching the top edge is
            // allowed; anything below it is not.
            Assert.True(ClearOfBody(label.Box, body),
                $"\"{heading}\" {label.Box} is drawn into the body of {boxName} {body}");

            // A heading on the far side of the window names nothing. One line --
            // 12 pixels in this layout -- is as far as a heading may sit from its
            // box and still read as its heading.
            var gap = label.Box.Y >= body.Y + body.Height
                ? label.Box.Y - (body.Y + body.Height)
                : body.Y - (label.Box.Y + label.Box.Height);
            Assert.True(gap >= 0 && gap <= 12,
                $"\"{heading}\" {label.Box} is {gap} pixels from {boxName} {body}, too far " +
                "away to read as its heading");
        }
    }

    [Fact]
    public void TheWindowPanelsDoNotOverlapEachOther()
    {
        var layout = Create();
        var panels = Panels(layout);

        var clashes = new List<string>();
        for (var i = 0; i < panels.Length; i++)
        {
            for (var j = i + 1; j < panels.Length; j++)
            {
                if (Overlaps(panels[i].Box, panels[j].Box))
                {
                    clashes.Add($"{panels[i].Name} {panels[i].Box} over {panels[j].Name} {panels[j].Box}");
                }
            }
        }

        Assert.True(clashes.Count == 0,
            "The inset panels are the window's structure, and one over another is a hole " +
            "in it:\n  " + string.Join("\n  ", clashes));
    }

    [Fact]
    public void TheResourceRowsDoNotOverlapEachOther()
    {
        var layout = Create();
        var areas = layout.Resources.Resources.ToList();

        // Food, trade, shields and the shared tax/lux/science row: four accounts
        // read as four lines, so no two of them may share a pixel.
        Assert.True(areas.Count == 4, $"Expected four resource areas, found {areas.Count}");
        Assert.True(areas.Any(area => area is SharedResourceArea),
            "The shared tax/lux/science row is missing from the layout.");

        var clashes = new List<string>();
        for (var i = 0; i < areas.Count; i++)
        {
            for (var j = i + 1; j < areas.Count; j++)
            {
                if (Overlaps(areas[i].Bounds, areas[j].Bounds))
                {
                    clashes.Add($"{areas[i].GetType().Name} {areas[i].Bounds} over " +
                                $"{areas[j].GetType().Name} {areas[j].Bounds}");
                }
            }
        }

        Assert.True(clashes.Count == 0,
            "Two resource rows in the same pixels count their icons over each other:\n  " +
            string.Join("\n  ", clashes));
    }

    [Fact]
    public void ALabelNeverCoversAPanelItDoesNotBelongTo()
    {
        var layout = Create();

        // The info panel carries text of its own -- its heading and the two caravan
        // lines -- so those three belong inside it. Every other label belongs on the
        // window's stone, above or below a panel and never across it: that is how
        // "Resource Map" came out drawn through the middle of the tile map.
        var insideInfoPanel = new List<string> { "UnitsPresent", "Supplies", "Demands" };

        foreach (var label in layout.Labels)
        {
            var name = label.Key;
            var box = label.Value.Box;

            foreach (var (panel, body) in Panels(layout))
            {
                if (panel == "InfoPanel" && insideInfoPanel.Contains(name))
                {
                    Assert.True(FitsInside(box, body),
                        $"\"{name}\" {box} is expected inside the info panel {body}");
                    continue;
                }

                Assert.True(!Overlaps(box, body),
                    $"\"{name}\" {box} is drawn across the {panel} panel {body}");
            }
        }
    }

    [Fact]
    public void TheCaravanLinesAndTheirTradeRoutesStayInTheInfoPanel()
    {
        var layout = Create();
        var panel = layout.InfoPanel.Box;
        var supplies = layout.Labels["Supplies"].Box;
        var demands = layout.Labels["Demands"].Box;

        Assert.True(FitsInside(supplies, panel), $"\"Supplies\" {supplies} is outside the info panel {panel}");
        Assert.True(FitsInside(demands, panel), $"\"Demands\" {demands} is outside the info panel {panel}");
        Assert.True(supplies.Y < demands.Y, "\"Supplies\" must sit above \"Demands\"");

        // The client writes one line per trade route under "Demands", 13 pixels
        // apart and the height of the "Demands" box, and a city routinely holds
        // three. They used to run past the bottom of the panel and out of the
        // window.
        for (var route = 0; route < 3; route++)
        {
            var line = new Rectangle(
                demands.X,
                demands.Y + 15 + 13 * route,
                demands.Width,
                demands.Height);

            Assert.True(FitsInside(line, panel),
                $"Trade route line {route} at {line} runs outside the info panel {panel}");
            Assert.True(!Overlaps(line, supplies) && !Overlaps(line, demands),
                $"Trade route line {route} at {line} is drawn over the caravan lines");
        }
    }

    [Fact]
    public void TheLayoutCarriesExactlyTheKeysTheClientReads()
    {
        var layout = Create();

        // A missing key is not a blank space: the client indexes these by name, and
        // a key it does not find throws the moment a city screen opens.
        var expectedLabels = new List<string>
        {
            "Citizens", "ResourceMap", "UnitsPresent", "UnitsSupported", "CityImprovements",
            "FoodStorage", "ItemInProduction", "Supplies", "Demands"
        };
        var expectedButtons = new List<string> { "Buy", "Change", "Info", "Map", "Rename", "Happy", "View", "Exit" };

        Assert.Equal(expectedLabels.OrderBy(k => k).ToList(), layout.Labels.Keys.OrderBy(k => k).ToList());
        Assert.Equal(expectedButtons.OrderBy(k => k).ToList(), layout.Buttons.Keys.OrderBy(k => k).ToList());

        // And that set is still what the client asks for. Walking the city window's
        // own source catches a read added without a matching entry in the layout.
        var repository = RepositoryRoot();
        var read = new HashSet<string>(StringComparer.Ordinal);
        var indexer = new Regex("(?:Labels|Buttons)\\[\\s*\"([A-Za-z0-9]+)\"");
        var buttonKey = new Regex("new CityButton\\([^,]+,\\s*\"([A-Za-z0-9]+)\"");

        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "RaylibUI"), "*.cs",
            SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var source = File.ReadAllText(file);
            foreach (Match match in indexer.Matches(source))
            {
                read.Add(match.Groups[1].Value);
            }
            foreach (Match match in buttonKey.Matches(source))
            {
                read.Add(match.Groups[1].Value);
            }
        }

        Assert.NotEmpty(read);
        var provided = expectedLabels.Concat(expectedButtons);
        var missing = read.Except(provided).ToList();
        Assert.True(missing.Count == 0,
            "The client reads these and the layout does not provide them, so opening a " +
            "city window would throw:\n  " + string.Join("\n  ", missing));
    }

    private static bool FitsInside(Rectangle box, int width, int height) =>
        box.X >= 0 && box.Y >= 0 && box.Width > 0 && box.Height > 0 &&
        box.X + box.Width <= width && box.Y + box.Height <= height;

    private static bool FitsInside(Rectangle box, Rectangle container) =>
        box.X >= container.X && box.Y >= container.Y &&
        box.X + box.Width <= container.X + container.Width &&
        box.Y + box.Height <= container.Y + container.Height;

    // Rectangles are half-open: sharing an edge is not sharing a pixel, which is
    // what lets a heading touch the top edge of the box it heads.
    private static bool Overlaps(Rectangle a, Rectangle b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static bool ClearOfBody(Rectangle heading, Rectangle body)
    {
        if (!Overlaps(heading, body))
        {
            return true;
        }

        // The one overlap allowed: the heading's last row is the box's first row.
        return heading.Y >= body.Y && heading.Y + heading.Height <= body.Y + 1;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "rhYciv.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
