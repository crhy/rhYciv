using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Units;
using Model.Utils;

namespace RhyCiv.Engine.UnitActions
{
    /// <summary>
    /// What a Diplomat can do to somebody else's units and cities: buy them.
    /// <para>
    /// None of this existed. A Diplomat has no attack strength, so walking one into
    /// an enemy unit or city was refused with "your Diplomat has no attack strength
    /// and cannot move onto an enemy unit" — the unit could be built and moved and
    /// was good for nothing at all.
    /// </para>
    /// <para>
    /// The two prices follow Civ II's shape rather than claiming to reproduce its
    /// arithmetic exactly: both rise with how much gold the owner is sitting on and
    /// fall away with distance from the seat of their government, so a garrison on
    /// a far frontier of a poor empire is cheap and the guard on a rich capital is
    /// not worth trying. The ruleset's own per-unit minimum bribe is honoured as a
    /// floor.
    /// </para>
    /// </summary>
    public static class DiplomatActions
    {
        /// <summary>
        /// Added to the owner's treasury before dividing by distance. A civilisation
        /// with nothing in the bank still has units worth something, so the price
        /// never falls to nothing.
        /// </summary>
        private const int UnitEndowment = 750;
        private const int CityEndowment = 1000;

        /// <summary>
        /// Distance assumed when a civilisation has no capital at all — its
        /// government has nothing left to hold the provinces with, so everything is
        /// as cheap as the furthest frontier.
        /// </summary>
        private const int NoCapitalDistance = 16;

        /// <summary>A veteran costs half as much again to turn.</summary>
        private const double VeteranPremium = 1.5;

        public static bool IsDiplomat(Unit unit) =>
            !unit.Dead && unit.AiRole == AiRoleType.Diplomacy;

        /// <summary>
        /// Whether this is a Spy rather than a Diplomat. A Spy does the same work
        /// and survives more of it: Civ II sends a Diplomat home in a body bag after
        /// almost anything it does, while a Spy walks away from the easier jobs.
        /// </summary>
        public static bool IsSpy(Unit unit) =>
            IsDiplomat(unit) && unit.Type == (int)UnitType.Spy;

        /// <summary>The enemy city on a square, if there is one.</summary>
        public static City? EnemyCityAt(Unit diplomat, Tile tile) =>
            tile.CityHere is { } city && city.Owner != diplomat.Owner ? city : null;

        /// <summary>Living units on a square that do not belong to the diplomat's civilisation.</summary>
        public static List<Unit> EnemyUnitsAt(Unit diplomat, Tile tile) =>
            tile.UnitsHere.Where(unit => !unit.Dead && unit.Owner != diplomat.Owner).ToList();

        /// <summary>
        /// Whether a diplomat arriving here has anything to offer.
        /// </summary>
        public static bool HasTarget(Unit diplomat, Tile tile) =>
            IsDiplomat(diplomat) &&
            (EnemyCityAt(diplomat, tile) != null || EnemyUnitsAt(diplomat, tile).Count > 0);

        /// <summary>
        /// Whether the units on a square can be bought.
        /// <para>
        /// Only a lone unit can. Gold buys one commander's loyalty; a stack watching
        /// each other cannot all be turned at once, which is exactly why standing a
        /// second unit beside a valuable one is worth doing.
        /// </para>
        /// </summary>
        public static Unit? BribableUnitAt(Unit diplomat, Tile tile)
        {
            var enemies = EnemyUnitsAt(diplomat, tile);
            if (enemies.Count != 1)
            {
                return null;
            }

            var target = enemies[0];

            // A city's garrison is bought by inciting the city, not one unit at a
            // time, and some barbarians answer to nobody's money.
            if (tile.CityHere != null || target.TypeDefinition.UnbribaleBarb)
            {
                return null;
            }

            return target;
        }

        /// <summary>
        /// Looks inside somebody else's city: what it is building, what it has
        /// built, and -- the reason anybody does this -- what is standing in it.
        /// <para>
        /// Civ II charges a Diplomat its life for the report and lets a Spy walk
        /// away having spent a move. That difference is the whole argument for
        /// researching the Spy, so it is the difference kept here.
        /// </para>
        /// </summary>
        /// <returns>Whether the report was obtained.</returns>
        public static bool InvestigateCity(IGame game, Unit agent, City city)
        {
            if (!IsDiplomat(agent) || city.Owner == agent.Owner)
            {
                return false;
            }

            SpendAgent(game, agent);
            return true;
        }

        /// <summary>
        /// Opens a permanent mission in somebody else's city.
        /// <para>
        /// The first thing Civ II's Diplomat is for, and the one thing this one
        /// could not do. An embassy is what turns a rival from a name on the map
        /// into a civilisation whose treasury, research and treaties can be seen --
        /// and it is the ordinary way to open relations without a war.
        /// </para>
        /// </summary>
        public static bool EstablishEmbassy(IGame game, Unit agent, City city)
        {
            if (!IsDiplomat(agent) || city.Owner == agent.Owner ||
                Diplomacy.DiplomacyFunctions.HasEmbassyWith(agent.Owner, city.Owner))
            {
                return false;
            }

            Diplomacy.DiplomacyFunctions.EstablishEmbassy(agent.Owner, city.Owner);
            SpendAgent(game, agent);
            return true;
        }

        /// <summary>
        /// What a job costs the agent that did it: a Diplomat its life, a Spy a
        /// move. Spending a whole move point rather than the single step the walk in
        /// would have cost is what stops a Spy working through a line of cities in
        /// one turn.
        /// </summary>
        private static void SpendAgent(IGame game, Unit agent)
        {
            if (IsSpy(agent))
            {
                agent.MovePointsLost += game.Rules.Cosmic.MovementMultiplier;
                if (agent.MovePoints < 0)
                {
                    agent.MovePointsLost = agent.MaxMovePoints;
                }

                return;
            }

            agent.Dead = true;
            game.Players[agent.Owner.Id].UnitLost(agent, null);
        }

        /// <summary>
        /// Advances this city's owner knows and the agent's civilisation does not.
        /// </summary>
        public static List<Advance> StealableAdvances(IGame game, Unit agent, City city) =>
            IsDiplomat(agent) && city.Owner != agent.Owner
                ? AdvanceFunctions.CalculateResearchTheft(game, agent.Owner, city.Owner)
                : new List<Advance>();

        /// <summary>Whether this city still has an advance left to be taken from it.</summary>
        public static bool CanStealFrom(City city) => !city.TechnologyStolen;

        /// <summary>
        /// Takes an advance out of somebody else's city.
        /// <para>
        /// The agent is spent on the same terms as a report: a Diplomat does not
        /// come home, a Spy does, having used a move. A city can only be robbed
        /// once, as in Civ II -- otherwise a rival capital is an endless supply of
        /// technology to anybody willing to keep building Diplomats.
        /// </para>
        /// </summary>
        /// <returns>Whether the advance was taken.</returns>
        public static bool StealTechnology(IGame game, Unit agent, City city, int advanceIndex)
        {
            if (!IsDiplomat(agent) || city.Owner == agent.Owner || !CanStealFrom(city) ||
                !AdvanceFunctions.HasTech(city.Owner, advanceIndex) ||
                AdvanceFunctions.HasTech(agent.Owner, advanceIndex))
            {
                return false;
            }

            game.GiveAdvance(advanceIndex, agent.Owner);

            // GiveAdvance ignores an advance the civilisation is barred from, so a
            // theft that could not land must not cost the agent.
            if (!AdvanceFunctions.HasTech(agent.Owner, advanceIndex))
            {
                return false;
            }

            city.TechnologyStolen = true;
            SpendAgent(game, agent);
            return true;
        }

        /// <summary>Whether a city can be bought out from under its owner.</summary>
        public static bool CanIncite(City city) =>
            !city.ImprovementExists(Effects.Capital);

        public static int BribeCost(IGame game, Unit target)
        {
            var price = Baseline(game, target.Owner, target.CurrentLocation, UnitEndowment);
            if (target.Veteran)
            {
                price *= VeteranPremium;
            }

            return Math.Max((int)Math.Round(price), target.TypeDefinition.MinBribe);
        }

        public static int InciteCost(IGame game, City city)
        {
            // A larger city has more people to bring round, and more to hand over.
            var price = Baseline(game, city.Owner, city.Location, CityEndowment) * Math.Max(1, city.Size);
            return Math.Max((int)Math.Round(price), 1);
        }

        /// <summary>
        /// The common part of both prices: the owner's wealth, spread thinner the
        /// further the target is from the seat of their government.
        /// </summary>
        private static double Baseline(IGame game, Civilization owner, Tile? where, int endowment)
        {
            var distance = DistanceFromCapital(owner, where);
            return (Math.Max(0, owner.Money) + endowment) / (double)(distance + 3);
        }

        private static int DistanceFromCapital(Civilization owner, Tile? where)
        {
            if (where == null)
            {
                return NoCapitalDistance;
            }

            var capitals = owner.Cities
                .Where(city => city.Location != null && city.ImprovementExists(Effects.Capital))
                .ToList();

            if (capitals.Count == 0)
            {
                return NoCapitalDistance;
            }

            return (int)Math.Round(capitals.Min(city => Utilities.DistanceTo(city.Location, where)));
        }

        /// <summary>
        /// Buys a unit. The diplomat is spent doing it, as Civ II spends one, and
        /// the bought unit arrives with its turn already over: it has just changed
        /// sides, not been given fresh orders.
        /// </summary>
        public static bool BribeUnit(IGame game, Unit diplomat, Unit target)
        {
            var buyer = diplomat.Owner;
            var cost = BribeCost(game, target);
            if (buyer.Money < cost)
            {
                return false;
            }

            var loser = target.Owner;
            var where = target.CurrentLocation;

            buyer.Money -= cost;

            loser.Units.Remove(target);
            target.Owner = buyer;
            // Nobody at home is paying for it any more.
            target.HomeCity = null;
            target.NeedsSupport = false;
            target.Order = (int)OrderType.NoOrders;
            target.MovePointsLost = target.MaxMovePoints;
            buyer.Units.Add(target);

            game.Players[loser.Id].UnitLost(target, diplomat);

            SpendDiplomat(game, diplomat);

            if (where != null)
            {
                where.SetVisible(buyer.Id);
                game.UpdateTiles(new List<Tile> { where });
            }

            return true;
        }

        /// <summary>
        /// Buys a city, and the garrison that was defending it. The diplomat is
        /// spent. A capital cannot be bought — the one city a government will not
        /// let go of at any price.
        /// </summary>
        public static bool InciteRevolt(IGame game, Unit diplomat, City city)
        {
            if (!CanIncite(city))
            {
                return false;
            }

            var buyer = diplomat.Owner;
            var cost = InciteCost(game, city);
            if (buyer.Money < cost)
            {
                return false;
            }

            var loser = city.Owner;
            var location = city.Location;
            buyer.Money -= cost;

            // The gold changes hands: the city's treasury goes with it.
            loser.Money = Math.Max(0, loser.Money - cost / 2);

            // Everyone standing in the city comes over with it. Leaving them behind
            // would put an enemy garrison inside a city the player now owns.
            var garrison = location.UnitsHere.Where(unit => !unit.Dead && unit.Owner == loser).ToList();
            foreach (var unit in garrison)
            {
                loser.Units.Remove(unit);
                unit.Owner = buyer;
                unit.HomeCity = null;
                unit.NeedsSupport = false;
                unit.Order = (int)OrderType.NoOrders;
                unit.MovePointsLost = unit.MaxMovePoints;
                buyer.Units.Add(unit);
            }

            loser.Cities.Remove(city);
            city.Owner = buyer;
            city.WhoBuiltIt ??= loser;
            buyer.Cities.Add(city);

            game.Players[loser.Id].CityLost(city);
            game.Players[buyer.Id].CityCaptured(city);

            SpendDiplomat(game, diplomat);

            location.SetVisible(buyer.Id);
            game.UpdateTiles(new List<Tile> { location });
            return true;
        }

        /// <summary>
        /// A Diplomat does not come home. Removing it through the same path combat
        /// uses means the interface is told, so the square is repainted and the unit
        /// stops being offered orders.
        /// </summary>
        private static void SpendDiplomat(IGame game, Unit diplomat)
        {
            diplomat.Dead = true;
            diplomat.MovePointsLost = diplomat.MaxMovePoints;
            game.Players[diplomat.Owner.Id].UnitLost(diplomat, null);
        }
    }
}
