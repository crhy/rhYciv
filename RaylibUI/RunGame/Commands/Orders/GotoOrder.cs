using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.UnitActions;
using JetBrains.Annotations;
using Model;
using Model.Core.Units;
using Model.Input;
using Model.Interface;
using Model.Controls;
using Model.Core.Cities;
using Path = RhyCiv.Engine.Units.Path;

namespace RaylibUI.RunGame.Commands.Orders;

[UsedImplicitly]
public class GotoOrder(GameScreen gameScreen) : Order(gameScreen, new Shortcut(Key.G), CommandIds.GotoOrder)
{
    private List<City> _cities = gameScreen.Player.Civilization.Cities;
    private bool _allCities;

    /// <summary>
    /// The button that widens the list from the player's own cities to everybody's,
    /// and narrows it again. It says what pressing it will show, not what is being
    /// shown now.
    /// <para>
    /// This used to have no button of its own: the dialog offers Ok and Cancel, and
    /// anything that was not Ok was taken as a request to swap the list -- so
    /// Cancel reopened the dialog with the other set of cities instead of closing
    /// it, and there was no way out of the dialog at all except to pick a
    /// destination.
    /// </para>
    /// </summary>
    private string ToggleButton => _allCities ? "Own Cities" : "All Cities";

    public override bool Update()
    {
        return SetCommandState(GameScreen.Player.ActiveUnit != null ? CommandStatus.Normal : CommandStatus.Invalid);
    }

    public override void Action()
    {
        _allCities = false;
        var activeUnit = GameScreen.Player.ActiveUnit!;
        Show(GameScreen.Player.Civilization.Cities, activeUnit);
    }

    private void HandleButtonClick(string button, int index, IList<bool>? arg3, IDictionary<string, string>? arg4)
    {
        if (button == ToggleButton)
        {
            _allCities = !_allCities;
            Show(_allCities ? GameScreen.Game.AllCities : GameScreen.Player.Civilization.Cities,
                GameScreen.Player.ActiveUnit!);
            return;
        }

        if (button != Labels.Ok || GameScreen.Player.ActiveUnit is not { } activeUnit ||
            index < 0 || index >= _cities.Count)
        {
            // Cancel, or a list with nothing in it. The dialog has already closed.
            return;
        }

        var city = _cities[index];
        var path = Path.CalculatePathBetween(GameScreen.Game, activeUnit.CurrentLocation, city.Location,
            activeUnit.Domain,
            activeUnit.MaxMovePoints, activeUnit.Owner, activeUnit.Alpine, activeUnit.IgnoreZonesOfControl);
        if (path == null)
        {
            return;
        }

        activeUnit.Order = (int)OrderType.GoTo;
        activeUnit.GoToX = city.Location.X;
        activeUnit.GoToY = city.Location.Y;
        activeUnit.GoToMapIndex = city.Location.Z;
        path.Follow(GameScreen.Game, activeUnit);
        if (activeUnit.MovePoints <= 0)
        {
            GameScreen.Game.ChooseNextUnit();
        }
    }

    private void Show(List<City> cities, Unit activeUnit)
    {
        var islands = MovementFunctions.GetIslandsFor(activeUnit);
        _cities = cities.Where(c => c.Location != activeUnit.CurrentLocation &&
                                    islands.Contains(c.Location.Island) ||
                                    c.Location.Neighbours().Any(l => islands.Contains(l.Island))).OrderBy(c => c.Name)
            .ToList();
        var listbox = new ListboxDefinition();
        listbox.Update(_cities.Select(c => c.Name).ToList());

        // Ok, then the widen/narrow toggle, then the way out.
        var defined = GameScreen.Main.ActiveInterface.GetDialog("GOTO")?.Button ?? [];
        var buttons = new List<string> { Labels.Ok, ToggleButton };
        buttons.AddRange(defined.Where(b => b != Labels.Ok && b != ToggleButton));

        GameScreen.ShowPopup("GOTO", handleButtonClick: HandleButtonClick,
            listBox: listbox, buttons: buttons);
    }
}