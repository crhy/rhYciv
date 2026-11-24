using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Production;
using Model;
using Model.Controls;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Production;
using Model.Input;
using RaylibUI.BasicTypes;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// Find City: pick a city by name, take the map to it, and change what it is
/// building.
/// <para>
/// The taking-the-map-to-it half was never written. The dialog listed every city
/// on the board and its Ok did nothing whatever -- an empty branch with the one
/// line that would have done the work commented out -- so the command looked
/// broken from the moment it was reachable.
/// </para>
/// <para>
/// The advisor this command is (matrix row 31 of docs/CIV2-UI-ADDITIONS.md) also
/// reports what each city is at work on, and lets the player say otherwise without
/// opening that city's window: the list says what a city is making, and choosing a
/// city offers its legal alternatives in the same dialog the city window's Change
/// button raises. Sorting the list into columns is the other half of row 31 and is
/// not done here.
/// </para>
/// </summary>
public class FindCity(GameScreen gameScreen)
    : AlwaysOnCommand(gameScreen, CommandIds.FindCity, [new Shortcut(Key.C, true)])
{
    private List<City> _cities = [];

    public override void Action()
    {
        // Sorted by name, because the list is searched by typing a name's first
        // letter and that is only meaningful in alphabetical order.
        _cities = GameScreen.Game.AllCities
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var listbox = new ListboxDefinition
        {
            Type = ListboxType.Default,
            Rows = 16,
        };
        listbox.Update(_cities.Select(DescribeCity).ToList());

        // ReSharper disable once StringLiteralTypo
        GameScreen.ShowPopup("FINDCITY", DialogClick, listBox: listbox);
    }

    /// <summary>
    /// A city as the advisor lists it: which tribe it belongs to, and what its
    /// workshops are busy with.
    /// </summary>
    private static string DescribeCity(City city)
    {
        var atWork = city.ItemInProduction?.Title;
        return atWork == null
            ? $"{city.Name} ({city.Owner.Adjective})"
            : $"{city.Name} ({city.Owner.Adjective}) at work on {atWork}";
    }

    private void DialogClick(string button, int index, IList<bool>? checkboxes, IDictionary<string, string>? _)
    {
        if (button != Labels.Ok || index < 0 || index >= _cities.Count)
        {
            return;
        }

        var city = _cities[index];

        // The view anchor is how the map is pointed somewhere that is not the
        // active unit; it is what middle-click panning uses, and it survives until
        // something moves the unit selection.
        GameScreen.SetViewAnchor(city.Location);

        // What the city is making was asked for in the list; what it could make
        // instead belongs to its owner. Somebody else's city is reported, not
        // ordered about, which is how the city window treats it too.
        if (city.OwnerId == GameScreen.Player.Civilization.Id)
        {
            GameScreen.QueueAfterCurrentPopup(() => OfferProduction(city));
        }
    }

    /// <summary>
    /// The picked city's workshops, and the projects they could take on: the same
    /// dialog, the same rows and the same costs the city window's Change button
    /// raises, raised from the advisor instead.
    /// </summary>
    private void OfferProduction(City city)
    {
        var allowed = ProductionPossibilities.GetAllowedProductionOrders(city);
        if (allowed.Count == 0)
        {
            return;
        }

        var active = GameScreen.MainWindow.ActiveInterface;
        var shieldRows = GameScreen.Game.Rules.Cosmic.RowsShieldBox;
        GameScreen.ShowPopup("PRODUCTION",
            handleButtonClick: (_, chosen, _, _) => ChooseProduction(city, allowed, chosen),
            replaceStrings: [city.Name],
            listBox: new ListboxDefinition
            {
                ImageShift = false,
                Rows = Math.Min(9, allowed.Count),
                Groups = allowed.Select(order => DescribeOrder(order, city, active, shieldRows)).ToList(),
                // The list opens on what the city is making, so the player can see
                // the present project before choosing another.
                SelectedId = IndexOfReference(allowed, city.ItemInProduction),
            });
    }

    /// <summary>
    /// One project as the advisor offers it: the city window's build-list row, with
    /// the city's current project marked as such.
    /// </summary>
    private static ListboxGroup DescribeOrder(IProductionOrder order, City city, IUserInterface active, int shieldRows)
    {
        var row = order.GetBuildListEntry(active, city, shieldRows);
        if (ReferenceEquals(order, city.ItemInProduction))
        {
            row.Elements.Add(new ListboxGroupElement
            {
                Text = "at work on this",
                TextSizeOverride = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return row;
    }

    private void ChooseProduction(City city, IList<IProductionOrder> offered, int chosen)
    {
        if (chosen < 0 || chosen >= offered.Count)
        {
            return;
        }

        var next = offered[chosen];
        var rules = GameScreen.Game.Rules;

        // Crossing from a unit to a building or a wonder takes a share of the
        // shields already gathered, so the player is asked before that happens --
        // the same warning the city window gives, and for the same reason.
        var penalty = city.ProductionChangePenalty(next, rules);
        if (penalty > 0)
        {
            GameScreen.ShowPopup("CHANGEPRODUCTION",
                handleButtonClick: (confirm, _, _, _) =>
                {
                    if (confirm == Labels.Ok)
                    {
                        TryChangeProduction(city, offered, next, rules);
                    }
                },
                replaceNumbers: [penalty, Math.Max(0, city.ShieldsProgress)],
                replaceStrings: [city.ItemInProduction?.Title ?? string.Empty, next.Title]);
            return;
        }

        TryChangeProduction(city, offered, next, rules);
    }

    /// <summary>
    /// Switches the city's project to one of the alternatives the advisor offered,
    /// and reports whether the change stands.
    /// <para>
    /// The engine does the switching: it knows the ruleset's penalty rate, counts
    /// wonders as their own category, and charges at most once a turn. Only a
    /// project the advisor offered may be applied, and the engine's own check has
    /// to agree afterwards that this city can make it; anything else is refused
    /// with the city still building what it was.
    /// </para>
    /// </summary>
    public static bool TryChangeProduction(City city, IList<IProductionOrder> offered,
        IProductionOrder next, Rules rules)
    {
        var current = city.ItemInProduction;
        if (next == null || ReferenceEquals(next, current) ||
            !offered.Any(order => ReferenceEquals(order, next)))
        {
            return false;
        }

        city.ChangeProduction(next, rules);
        if (!ProductionPossibilities.ProductionValid(city))
        {
            city.ItemInProduction = current;
            return false;
        }

        // The city window shows a city's construction queue when it has one, so the
        // queue follows the change instead of going on naming the old project. The
        // shields already spent carry over, as they do when the change is made from
        // the window.
        var carried = Math.Max(0, city.ShieldsProgress);
        city.ConstructionQueue.Clear();
        city.ConstructionQueue.Enqueue(next, rules.Cosmic.RowsShieldBox);
        if (city.ConstructionQueue.Current is { } queued)
        {
            queued.RemainingCost = Math.Max(0, next.Cost - carried);
            queued.Status = carried > 0 ? ItemStatus.InProgress : ItemStatus.Queued;
        }

        return true;
    }

    private static int IndexOfReference(IList<IProductionOrder> orders, IProductionOrder? order)
    {
        for (var i = 0; i < orders.Count; i++)
        {
            if (ReferenceEquals(orders[i], order))
            {
                return i;
            }
        }

        return 0;
    }
}
