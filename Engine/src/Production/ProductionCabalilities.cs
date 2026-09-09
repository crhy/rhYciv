using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using Model.Constants;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.Production;

namespace RhyCiv.Engine.Production
{
    public static class ProductionPossibilities
    {
        private static List<IProductionOrder>[] _availableProducts = [];

        public static void InitializeProductionLists(IEnumerable<Civilization> civs, IProductionOrder[] possibleOrders)
        {
            var orders = possibleOrders
                .Where(o => o.RequiredTech != AdvancesConstants.No && o.ExpiresTech != AdvancesConstants.No).ToList();

            _availableProducts = civs.Select(c =>
                    orders.Where(o =>
                        (o.ExpiresTech == AdvancesConstants.Nil || (o.ExpiresTech < c.Advances.Length && !c.Advances[o.ExpiresTech])) &&
                        (o.RequiredTech == AdvancesConstants.Nil || (o.RequiredTech < c.Advances.Length && c.Advances[o.RequiredTech]))).ToList())
                .ToArray();
        }

        public static void AddItems(int targetCiv, IEnumerable<IProductionOrder> items)
        {
            _availableProducts[targetCiv].AddRange(items);
        }
        
        public static void RemoveItems(int targetCiv, IEnumerable<IProductionOrder> items)
        {
            var itemList = items.ToList();
            if (itemList.Count > 0)
            {
                _availableProducts[targetCiv].RemoveAll(i => itemList.Contains(i));
            }
        }

        /// <summary>
        /// Takes an improvement off every civilisation's build list.
        /// <para>
        /// This is how a wonder stops being available once somebody has built it.
        /// There is only ever one of each in the world, and nothing enforced that:
        /// two civilisations, or two cities of the same civilisation, could each
        /// raise the Pyramids and each get the benefit.
        /// </para>
        /// </summary>
        public static void WithdrawImprovement(int improvementType)
        {
            foreach (var available in _availableProducts)
            {
                available.RemoveAll(order =>
                    order is BuildingProductionOrder building && building.Improvement.Type == improvementType);
            }
        }

        public static bool ProductionValid(City city)
        {
            return _availableProducts[city.OwnerId].Contains(city.ItemInProduction) && city.ItemInProduction.IsValidBuild(city);
        }

        public static IProductionOrder? AutoNext(City city)
        {
            return _availableProducts[city.OwnerId]
                .Where(p => p.RequiredTech == city.ItemInProduction.ExpiresTech && p.Type == city.ItemInProduction.Type)
                .MinBy(p => p.Cost);
        }

        public static Improvement? FindByEffect(int targetCiv, Effects effect)
        {
            return _availableProducts[targetCiv].OfType<BuildingProductionOrder>()
                .Where(p => p.Improvement.Effects.ContainsKey(effect)).Select(o => o.Improvement).FirstOrDefault();
        }

        /// <summary>
        /// What this city may build, in a stable order: units first, then
        /// improvements, each in the order the ruleset declares them.
        /// <para>
        /// The underlying list is built once at game start and appended to by
        /// AddItems as advances are discovered, so without this an item unlocked
        /// mid-game landed at the bottom of a list of sixty entries rather than in
        /// its usual place. A player who researched Ceremonial Burial looked where
        /// the Temple belongs, did not find it, and reasonably concluded it could
        /// not be built.
        /// </para>
        /// </summary>
        public static IList<IProductionOrder> GetAllowedProductionOrders(City thisCity)
        {
            return _availableProducts[thisCity.OwnerId]
                .Where(i => i.IsValidBuild(thisCity))
                .OrderBy(i => i.Type)
                .ThenBy(i => i.ImageIndex)
                .ToList();
        }
    }
}
