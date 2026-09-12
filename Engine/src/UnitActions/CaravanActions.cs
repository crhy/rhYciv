using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Production;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine.UnitActions;

/// <summary>
/// What a Caravan does when it finally gets somewhere.
/// <para>
/// The whole apparatus of trade was in place and idle: cities were given
/// commodities to supply and demand, the city window has a line for trade routes,
/// the map draws the routes as golden threads, and the save format carries them.
/// Nothing could ever create one. A Caravan reaching a city simply stood there,
/// and a Caravan reaching somebody else's city was refused as a failed attack.
/// </para>
/// </summary>
public static class CaravanActions
{
    /// <summary>Routes a single city can hold, as in Civ II.</summary>
    public const int MaximumRoutesPerCity = 4;

    /// <summary>
    /// How far apart two cities must be before a route between them is worth
    /// anything. Civ II uses eight squares, and waives it for cities of different
    /// civilisations, where the point is the contact rather than the distance.
    /// </summary>
    public const int MinimumDistance = 8;

    /// <summary>
    /// Whether this unit carries goods rather than weapons. The ruleset marks
    /// Caravans and Freight with the Trade role, the same way it marks Settlers and
    /// Diplomats.
    /// </summary>
    public static bool IsCaravan(Unit unit) =>
        unit.AiRole == AiRoleType.Trade && unit.AttackBase == 0;

    /// <summary>The wonder this city is building, if it is building one.</summary>
    public static Improvement? WonderInProgress(City city) => WonderProgress.WonderUnderConstruction(city);

    /// <summary>
    /// Whether the caravan's cargo can be put into the city's great work. Civ II
    /// allows this for wonders only -- a caravan cannot hurry an ordinary building
    /// -- and only for cities of the caravan's own civilisation.
    /// </summary>
    public static bool CanHelpBuildWonder(Unit caravan, City city) =>
        IsCaravan(caravan) && city.Owner == caravan.Owner && WonderInProgress(city) != null;

    /// <summary>
    /// Adds the caravan's own cost in shields to the wonder and consumes the unit.
    /// Returns what was added, which is what the message tells the player.
    /// </summary>
    public static int HelpBuildWonder(IGame game, Unit caravan, City city)
    {
        if (!CanHelpBuildWonder(caravan, city))
        {
            return 0;
        }

        // What the caravan cost to build is what it is worth when it is broken up:
        // the wagons, the goods and the labour go straight into the work.
        var shields = Math.Max(1, caravan.TypeDefinition.Cost);
        city.ShieldsProgress += shields;
        Consume(game, caravan);
        return shields;
    }

    /// <summary>
    /// The city this caravan calls home, which is the other end of any route it
    /// establishes. A caravan whose home city has been lost trades on behalf of the
    /// nearest city its civilisation still holds.
    /// </summary>
    public static City? HomeCity(Unit caravan)
    {
        if (caravan.HomeCity is { } home && caravan.Owner.Cities.Contains(home))
        {
            return home;
        }

        return caravan.Owner.Cities
            .OrderBy(city => MapDistance(city.Location, caravan.CurrentLocation))
            .FirstOrDefault();
    }

    /// <summary>Why a route cannot be opened, or null when it can.</summary>
    public static string? RouteRefusal(City home, City destination)
    {
        if (home == destination)
        {
            return "A caravan cannot trade with the city it set out from.";
        }

        if (home.TradeRoutes.Length >= MaximumRoutesPerCity)
        {
            return $"{home.Name} already has all the trade routes it can manage.";
        }

        if (destination.TradeRoutes.Length >= MaximumRoutesPerCity)
        {
            return $"{destination.Name} already has all the trade routes it can manage.";
        }

        if (home.Owner == destination.Owner && MapDistance(home.Location, destination.Location) < MinimumDistance)
        {
            return $"{destination.Name} is too close to {home.Name} for the journey to be worth making.";
        }

        return null;
    }

    /// <summary>
    /// The standing value of a route between two cities, in trade arrows a turn for
    /// each end.
    /// <para>
    /// Civ II's own arithmetic: the distance between the cities plus ten, times the
    /// trade the two of them produce, over twenty-four -- then halved for cities on
    /// the same continent and halved again for cities of the same civilisation,
    /// which is why the profitable routes are the long ones to strangers.
    /// </para>
    /// </summary>
    /// <summary>
    /// The arrows a standing trade route brings its city every turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Civ II: <c>(T1 + T2 + 4) / 8</c> for a route to another civilisation, and
    /// <c>k(T1 + T2 + 4) / 16</c> between two of your own cities, where T1 and T2
    /// are the two cities' base trade and every division drops its remainder. The
    /// k is a transport modifier -- 1.5 for a road along the route, 2 for a
    /// railroad -- which this game does not work out yet and so treats as 1.
    /// </para>
    /// <para>
    /// What was here before was Civilization <em>I</em>'s formula, and not even
    /// that one's continuing-route formula: it was Civ I's one-off delivery
    /// payment, <c>(distance + 10) * (Ta + Tb) / 24</c>, charged again every turn.
    /// Measured against Civ II on the same save it paid four arrows a turn where
    /// Civ II paid one. Distance does not enter the Civ II figure at all.
    /// </para>
    /// </remarks>
    public static int RouteValue(City home, City destination)
    {
        // One division, not two: Civ II's arithmetic drops remainders as it goes,
        // so halving a halved figure is not the same as dividing by sixteen.
        var divisor = home.Owner == destination.Owner ? 16 : 8;

        return (home.TileTrade + destination.TileTrade + 4) / divisor;
    }

    /// <summary>
    /// What the arrival itself is worth, paid once in gold and again in research.
    /// A city that wants what the caravan is carrying pays twice over.
    /// </summary>
    public static int DeliveryBonus(Unit caravan, City home, City destination)
    {
        // The payment for the delivery itself, which is a different thing from the
        // route it leaves behind and is worked out differently: distance and the
        // two civilisations' remoteness from one another are what make a caravan
        // worth sending that far. This is still Civ I's shape and has not been
        // measured against Civ II -- see docs/CIV2-COMPARISON.md.
        var distance = MapDistance(home.Location, destination.Location);
        var value = (distance + 10) * (home.Trade + destination.Trade) / 24;

        if (home.Location.Island == destination.Location.Island)
        {
            value /= 2;
        }

        if (home.Owner == destination.Owner)
        {
            value /= 2;
        }

        var bonus = Math.Max(1, value) * DeliveryMultiplier;

        if (Demands(destination, caravan.CaravanCommodity))
        {
            bonus *= 2;
        }

        return Math.Max(1, bonus);
    }

    /// <summary>
    /// A delivery is worth a good deal more than a turn of the route it opens: the
    /// caravan is carrying goods, and they are sold on arrival.
    /// </summary>
    private const int DeliveryMultiplier = 8;

    private static bool Demands(City city, int commodity) =>
        city.CommodityDemanded?.Any(wanted => wanted.Id == commodity) == true;

    /// <summary>
    /// Opens the route, pays for the delivery and consumes the caravan. Both cities
    /// gain the route: trade goes both ways.
    /// </summary>
    public static TradeDelivery EstablishTradeRoute(IGame game, Unit caravan, City destination)
    {
        var home = HomeCity(caravan);
        if (home == null)
        {
            return new TradeDelivery(0, 0, 0);
        }

        var value = RouteValue(home, destination);
        var bonus = DeliveryBonus(caravan, home, destination);

        if (RouteRefusal(home, destination) == null)
        {
            AddRoute(game, home, destination, caravan.CaravanCommodity);
            AddRoute(game, destination, home, caravan.CaravanCommodity);
        }
        else
        {
            // Civ II still buys the goods when no route can be opened -- the
            // caravan has still crossed the world with something worth having.
            value = 0;
        }

        caravan.Owner.Money += bonus;
        caravan.Owner.Science += bonus;
        Consume(game, caravan);

        return new TradeDelivery(bonus, bonus, value);
    }

    /// <summary>What a delivery produced, for the message that reports it.</summary>
    public readonly record struct TradeDelivery(int Gold, int Science, int RouteValue);

    private static void AddRoute(IGame game, City city, City partner, int commodity)
    {
        var index = game.AllCities.IndexOf(partner);
        if (index < 0 || city.TradeRoutes.Any(route => route.Destination == index))
        {
            return;
        }

        var commodities = game.Rules.CaravanCommoditie;
        city.TradeRoutes = city.TradeRoutes
            .Append(new TradeRoute
            {
                Destination = index,
                Commodity = commodities.Length > 0
                    ? commodities[Math.Abs(commodity) % commodities.Length]
                    : new Commodity()
            })
            .ToArray();
        city.ActiveTradeRoutes = city.TradeRoutes.Length;
    }

    /// <summary>
    /// The trade a city's routes bring it each turn, added to what its own squares
    /// produce. Without this the routes were drawn on the map and listed in the
    /// city window and earned nothing whatever.
    /// </summary>
    /// <summary>
    /// Whether a trade-route slot holds a route at all.
    /// </summary>
    /// <remarks>
    /// A city keeps three slots whether or not it has any routes, and an unused
    /// one is zero in both fields. Both readers built a route out of every slot
    /// regardless, so every city in the world came out of a saved game with three
    /// routes to whichever city happened to be first in the list -- worth a
    /// hundred and twenty trade to a city whose squares produced three. It showed
    /// up the moment a real Civ II save was opened beside the original: Civ II
    /// said Cardiff had 3 trade and this said 123.
    ///
    /// Commodity zero alone is not enough to call a slot empty, because it names a
    /// real commodity; the pair being zero is what marks one unused.
    /// </remarks>
    public static bool IsRecordedRoute(int commodity, int destination) =>
        commodity != 0 || destination != 0;

    public static int TradeFromRoutes(IGame game, City city)
    {
        var total = 0;
        foreach (var route in city.TradeRoutes)
        {
            if (route.Destination < 0 || route.Destination >= game.AllCities.Count)
            {
                continue;
            }

            var partner = game.AllCities[route.Destination];
            if (partner == city)
            {
                continue;
            }

            total += RouteValue(city, partner);
        }

        return total;
    }

    /// <summary>
    /// Distance in squares between two places on the map, the long way round the
    /// world included. Map columns are half a square apart on alternate rows, which
    /// is why the horizontal difference is counted in the doubled coordinates the
    /// game uses everywhere else.
    /// </summary>
    public static int MapDistance(Tile from, Tile to)
    {
        if (from == null || to == null)
        {
            return 0;
        }

        var fromX = from.X * 2 + (from.Y & 1);
        var toX = to.X * 2 + (to.Y & 1);

        var dx = Math.Abs(fromX - toX);
        if (!from.Map.Flat)
        {
            dx = Math.Min(dx, from.Map.XDimMax - dx);
        }

        var dy = Math.Abs(from.Y - to.Y);

        // A diagonal step moves one column and one row at once, so the distance is
        // whichever of the two is greater rather than their sum.
        return Math.Max(dx / 2, dy);
    }

    /// <summary>
    /// The caravan is spent rather than killed: it is broken up where it stands and
    /// its city stops supporting it. The player is told through the same path as a
    /// unit that dies, which is what repaints the square it was standing on and
    /// takes it out of the selection.
    /// </summary>
    private static void Consume(IGame game, Unit caravan)
    {
        caravan.Dead = true;
        caravan.MovePointsLost = caravan.MaxMovePoints;
        caravan.Owner.Units.Remove(caravan);
        game.Players[caravan.Owner.Id].UnitLost(caravan, null);
    }
}
