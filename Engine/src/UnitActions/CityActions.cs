using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Production;
using RhyCiv.Engine.Units;
using Model.Constants;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Production;
using Model.Core.Units;

namespace RhyCiv.Engine.UnitActions
{
    public static class CityActions 
    {
        public static string GetCityName(Civilization civ , IGame game)
        {
            var cityCount = game.CitiesBuiltSoFar.GetValueOrDefault(civ, (byte) 0);
            var names = game.CityNames;
            var tribe = civ.TribeName.ToUpperInvariant();
            var civCityList = names.TryGetValue(tribe, out var tribeList) ? tribeList
                : names.TryGetValue("EXTRA", out var extraList) ? extraList
                : null;
            if (civCityList != null && cityCount < civCityList.Count)
            {
                return civCityList[cityCount];
            }
            
            return "Dummy Name";
        }

        /// <summary>
        /// Removes a unit from the game entirely. A unit standing in, or homed
        /// to, a city credits that city's current production with half its
        /// shield cost, as Civ II does when a unit is disbanded in a city;
        /// otherwise it is simply removed.
        /// </summary>
        public static void DisbandUnit(Unit unit, IGame game)
        {
            var city = unit.CurrentLocation?.CityHere ?? unit.HomeCity;
            if (city != null)
            {
                ApplyDisbandProductionCredit(city, unit, game.Rules.Cosmic.RowsShieldBox);
            }

            unit.Dead = true;
            unit.Owner.Units.Remove(unit);
        }

        /// <summary>
        /// Credits half a disbanded unit's shield cost toward the city's item in
        /// production, capped at that item's remaining cost. Only applies when
        /// the unit is standing in, or homed to, the crediting city.
        /// </summary>
        public static void ApplyDisbandProductionCredit(City city, Unit unit, int shieldRows)
        {
            if (unit.HomeCity != city && unit.CurrentLocation != city.Location)
            {
                return;
            }

            var totalCost = Math.Max(1, city.ItemInProduction.Cost);
            var shieldCredit = Math.Max(1, unit.TypeDefinition.Cost / 2);
            city.ShieldsProgress = Math.Min(totalCost, city.ShieldsProgress + shieldCredit);

            var queuedItem = city.ConstructionQueue.Current;
            if (queuedItem != null)
            {
                queuedItem.RemainingCost = Math.Max(0, queuedItem.RemainingCost - shieldCredit);
            }
        }

        /// <summary>
        /// Give a new city the commodities it supplies and demands. Only the save
        /// readers ever set these, so every city founded in play showed an empty
        /// Supplies and Demands line. Civ II derives them from the city's makeup;
        /// until caravans exist to trade them this picks a stable set from the
        /// ruleset's list, seeded by where the city stands so it survives a save and
        /// reload and differs between neighbours.
        /// </summary>
        private static void AssignTradeCommodities(City city, Rules rules)
        {
            var commodities = rules.CaravanCommoditie;
            if (commodities.Length == 0)
            {
                return;
            }

            const int wanted = 3;
            var seed = city.Location.X * 7919 + city.Location.Y * 104729 + city.Owner.Id;
            var order = Enumerable.Range(0, commodities.Length)
                .OrderBy(i => HashCode.Combine(seed, i))
                .ToArray();

            var supplied = order.Take(Math.Min(wanted, order.Length)).ToArray();
            var demanded = order.Skip(supplied.Length).Take(Math.Min(wanted, order.Length - supplied.Length)).ToArray();

            city.CommoditySupplied = supplied.Select(i => commodities[i]).ToArray();
            city.CommodityDemanded = demanded.Select(i => commodities[i]).ToArray();
        }

        /// <summary>
        /// What a newly founded city starts building.
        /// <para>
        /// This used to be the cheapest thing in the whole ruleset, taken straight
        /// from the unit and improvement tables. Those tables carry every slot the
        /// format defines, including disabled ones costing nothing, so a new city
        /// routinely opened building an item that was not buildable and made no
        /// progress -- the city sat at zero shields and the player had to notice and
        /// change it by hand.
        /// </para>
        /// <para>
        /// Civ II opens on Warriors. The nearest general rule is the cheapest
        /// defender this city is actually allowed to build, falling back to the
        /// cheapest allowed item of any kind.
        /// </para>
        /// </summary>
        private static IProductionOrder? ChooseOpeningProduction(City city, IGame game)
        {
            var allowed = ProductionPossibilities.GetAllowedProductionOrders(city)
                .Where(order => order.Cost > 0)
                .ToList();
            if (allowed.Count == 0)
            {
                return null;
            }

            var defender = allowed.OfType<UnitProductionOrder>()
                .Where(order => order.UnitDefinition.Domain == UnitGas.Ground)
                .Where(order => order.UnitDefinition.Defense > 0 && !order.UnitDefinition.IsSettler)
                .MinBy(order => order.Cost);

            return defender ?? allowed.MinBy(order => order.Cost);
        }

        public static City BuildCity(Unit unit, IGame game, string name)
        {
            var tile = unit.CurrentLocation;
            // Something has to be set before the city is registered; the real choice
            // is made below, once the city is on the map and can be asked what it is
            // allowed to build.
            var initialProduction = ProductionOrder.GetAll(game.Rules).MinBy(i => i.Cost);
            var city = new City
            {
                Location = tile,
                Name = name,
                X = tile.X,
                Y = tile.Y,
                Owner = unit.Owner,
                Size = 1,
                ItemInProduction = initialProduction!,
                WhoBuiltIt = unit.Owner,
            };
            // A city replaces whatever village/hut marker was on this tile.  Settlers can start
            // on a goody hut, and Civ2 allows founding there; if the hut is not cleared the map
            // renderer continues to draw the hut instead of the newly founded city.
            tile.HasGoodieHut = false;

            tile.WorkedBy = city;
            tile.CityHere = city;
            game.AllCities.Add(tile.CityHere);
            unit.Owner.Cities.Add(tile.CityHere);

            game.SetImprovementsForCity(city);

            city.ItemInProduction = ChooseOpeningProduction(city, game) ?? city.ItemInProduction;

            
            if (unit.Owner.Cities.Count == 1)
            {
                var capitalImprovement = ProductionPossibilities.FindByEffect(city.Owner.Id, Effects.Capital)
                                         ?? game.Rules.Improvements.Where(i =>
                                             i.Effects.ContainsKey(Effects.Capital) &&
                                             city.Owner.AllowedAdvanceGroups[
                                                 game.Rules.Advances[i.Prerequisite].AdvanceGroup] !=
                                             AdvanceGroupAccess.Prohibited).MinBy(i => i.Cost);
                if (capitalImprovement != null)
                {
                    city.AddImprovement(capitalImprovement);
                }
            }
            game.History.CityBuilt(tile.CityHere);
            int currentCityCount = game.CitiesBuiltSoFar.GetValueOrDefault(city.Owner, 0);
            game.CitiesBuiltSoFar[city.Owner] = currentCityCount + 1;

            AssignTradeCommodities(city, game.Rules);

            // A city sees the ground it works. Founding one used to reveal nothing
            // at all -- the UI command marked the single square the settler stood on
            // and the engine marked none -- so the twenty squares around a new city
            // stayed unseen unless a unit happened to walk over them.
            //
            // That is not only a matter of what is drawn. A citizen is only put to
            // work on a square the civilisation can see, so a city whose radius was
            // unseen worked nothing but its own centre: two food in, two food eaten,
            // no surplus, and no growth ever. It is why by AD 1220 the largest city
            // in the world was size four.
            tile.Map.SetAsStartingLocation(tile, city.OwnerId);

            city.AutoAddDistributionWorkers(game.Rules);
            city.CalculateOutput(city.Owner.Government, game);

            unit.Dead = true;
            unit.MovePointsLost = unit.MovePoints;

            if (tile.Fertility != -2)
            {
                tile.Map.AdjustFertilityForCity(tile);
            }

            game.UpdateTiles(new List<Tile> {tile});

            return city;
        }
    }
}