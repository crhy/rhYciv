using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using Model.Controls;
using Model.Core.Cities;
using Model.Input;
using RaylibUI.BasicTypes;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// Find City: pick a city by name and take the map to it.
/// <para>
/// The taking-the-map-to-it half was never written. The dialog listed every city
/// on the board and its Ok did nothing whatever -- an empty branch with the one
/// line that would have done the work commented out -- so the command looked
/// broken from the moment it was reachable.
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
        listbox.Update(_cities.Select(c => $"{c.Name} ({c.Owner.Adjective})").ToList());

        // ReSharper disable once StringLiteralTypo
        GameScreen.ShowPopup("FINDCITY", DialogClick, listBox: listbox);
    }

    private void DialogClick(string button, int index, IList<bool>? checkboxes, IDictionary<string, string>? _)
    {
        if (button != Labels.Ok || index < 0 || index >= _cities.Count)
        {
            return;
        }

        // The view anchor is how the map is pointed somewhere that is not the
        // active unit; it is what middle-click panning uses, and it survives until
        // something moves the unit selection.
        GameScreen.SetViewAnchor(_cities[index].Location);
    }
}
