using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Enums;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine.UnitActions;

/// <summary>
/// Airlifts. An air unit standing on a friendly city that carries an airbase can
/// be lifted straight to another such city, landing with its turn spent. The
/// Civilopedia sells the Airport as an air-power and rapid-transport hub and
/// promises one unit per turn between cities equipped with Airports
/// (docs/CIVILOPEDIA-TEXT.md:884), so a city that has already received a lift
/// this turn is not offered again until the next one -- the game's own text for
/// that case is @ALREADYAIRLIFT.
/// <para>
/// Unlike a paradrop, an airlift reveals nothing and fights nothing. Both ends
/// are squares the player already owns and can already see, so no territory is
/// uncovered, no neighbour is set visible, no defender is dislodged and no combat
/// is rolled: the unit simply appears on the far runway. Its fuel is settled the
/// usual way at the end of the turn, where a city or airbase already counts as
/// somewhere to refuel.
/// </para>
/// <para>
/// Restricted to air units. The improvement that arms the order is the airbase,
/// whose own level data hides air units (HideUnits = UnitGas.Air), and the
/// Civilopedia entry frames it as an air-power hub. Whether Civ II also airlifts
/// land units is left open rather than guessed at.
/// </para>
/// </summary>
public static class AirliftFunctions
{
    /// <summary>
    /// Whether the unit is in a position to be lifted at all, independent of any
    /// destination.
    /// </summary>
    public static bool CanAirlift(Unit unit)
    {
        if (unit.Dead || unit.Domain != UnitGas.Air || unit.CurrentLocation == null)
        {
            return false;
        }

        // As with a paradrop, the lift costs a whole turn, so it is only offered
        // to a unit that has the turn in hand and is not being carried.
        if (unit.MovePointsLost > 0 || unit.InShip != null)
        {
            return false;
        }

        // The runway the unit stands on has to be its own: an airbase in enemy
        // hands is not a place to be lifted from.
        return IsFriendlyAirbase(unit.CurrentLocation, unit.Owner);
    }

    private static bool HasAirbase(Tile tile) =>
        tile.EffectsList.Any(e => e.Target == ImprovementConstants.Airbase);

    private static bool IsFriendlyAirbase(Tile tile, Civilization owner) =>
        tile.CityHere != null && tile.CityHere.Owner == owner && HasAirbase(tile);

    /// <summary>
    /// The city that would receive the unit, when this turn allows it.
    /// </summary>
    private static bool TryGetTargetCity(IGame game, Unit unit, Tile target, out City? city)
    {
        city = target.CityHere;
        return CanAirlift(unit) && target != unit.CurrentLocation &&
               city != null && city.Owner == unit.Owner && HasAirbase(target) &&
               city.AirliftTurn != game.TurnNumber;
    }

    /// <summary>
    /// Every city the unit could be lifted into this turn: the player's other
    /// airbases whose airlift capacity for this turn is still open.
    /// </summary>
    public static IReadOnlyCollection<Tile> ValidTargets(IGame game, Unit unit)
    {
        if (!CanAirlift(unit))
        {
            return [];
        }

        var origin = unit.CurrentLocation;
        return game.AllCities
            .Where(city => city.Location != null && city.Location != origin && city.Owner == unit.Owner &&
                         HasAirbase(city.Location) && city.AirliftTurn != game.TurnNumber)
            .Select(city => city.Location)
            .ToList();
    }

    /// <summary>
    /// Whether this particular city may receive the unit this turn.
    /// </summary>
    public static bool IsValidTarget(IGame game, Unit unit, Tile target) =>
        TryGetTargetCity(game, unit, target, out _);

    /// <summary>
    /// Execute a lift. Returns false and changes nothing when the move is not legal.
    /// </summary>
    public static bool TryAirlift(IGame game, Unit unit, Tile target)
    {
        if (!TryGetTargetCity(game, unit, target, out var city))
        {
            return false;
        }

        unit.PrevXy = [unit.X, unit.Y];
        unit.X = target.X;
        unit.Y = target.Y;
        // Assigning the location moves the unit between the tiles' unit lists.
        unit.CurrentLocation = target;

        // The lift costs the whole turn however far it went, and leaves the unit
        // with no further order pending.
        unit.MovePointsLost = unit.MaxMovePoints;
        unit.Order = (int)OrderType.NoOrders;

        // The runway that received the unit is now full for this turn.
        city.AirliftTurn = game.TurnNumber;
        return true;
    }
}
