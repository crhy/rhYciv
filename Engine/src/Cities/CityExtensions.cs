using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using RhyCiv.Engine.Production;
using Model.Core.Cities;
using Model.Core.Production;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine
{
    public static class CityExtensions
    {
        public static void CalculateOutput(this City city, int government, IGame game)
        {
            var orgLevel = city.GetOrganizationLevel(game.Rules);

            var lowOrganisation = orgLevel < 1;
            // var hasSuperMarket = city.ImprovementExists(ImprovementType.Supermarket);
            // var hasSuperhighways = city.ImprovementExists(ImprovementType.Superhighways);

            var totalFood = 0;
            var totalSheilds = 0;
            var totalTrade = 0;

            // King Richard's Crusade adds a shield to every worked tile of its city;
            // the Colossus adds trade to every worked tile already producing some.
            var wonderShieldBonus = WonderFunctions.GetWorkedTileShieldBonus(city);
            var wonderTradeBonus = WonderFunctions.GetProducingTileTradeBonus(city);

            city.WorkedTiles.ForEach(t =>
            {
                totalFood += t.GetFood(lowOrganisation);
                totalSheilds += t.GetShields(lowOrganisation) + wonderShieldBonus;

                var tileTrade = t.GetTrade(orgLevel);
                if (tileTrade > 0)
                {
                    tileTrade += wonderTradeBonus;
                }

                totalTrade += tileTrade;
            });

            // Recorded before the routes are added, because that is the figure a
            // route is valued from and it must not include the route's own arrows.
            city.TileTrade = totalTrade;

            // A trade route brings its city trade every turn for as long as it
            // stands. The routes were drawn on the map and listed in the city window
            // and earned nothing whatever.
            totalTrade += UnitActions.CaravanActions.TradeFromRoutes(game, city);

            // Factory, Mfg. Plant and the power plants raise shield production by
            // a percentage rather than a flat amount; Hoover Dam grants every city
            // of its owner the Hydro Plant bonus (see WonderFunctions).
            totalSheilds = (int)(totalSheilds * city.GetMultiplier(Effects.ProductionMultiplier));

            city.Support = city.SupportedUnits.Count(u => u.NeedsSupport);


            var distance = ComputeDistanceFactor(city, government, game);
            if (distance == 0)
            {
                city.Waste = 0;
                city.Corruption = 0;
            }
            else
            {
                distance *= city.Location.Map.ScaleFactor;
                var gov = (int)(city.WeLoveKingDay ? government + 1 : government);

                var corruptionTenTenFactor = 15d / (4 + gov);
                var wasteTenTenFactor = 15d / (4 + gov * 4);

                // https://apolyton.net/forum/miscellaneous/archives/civ2-strategy-archive/62524-corruption-and-waste
                var corruption = totalTrade * Math.Min(32, distance) * corruptionTenTenFactor / 100;

                var waste = (totalSheilds - city.Support) * Math.Min(16, distance) * wasteTenTenFactor / 100;

                var corruptionReduction =
                    city.Improvements.Sum(i => i.Effects.GetValueOrDefault(Effects.ReduceCorruption));
                if (corruptionReduction > 0)
                {
                    var modifier = corruptionReduction / 100d;
                    waste *= modifier;
                    corruption *= modifier;
                }

                //TODO: Trade route to capital


                city.Waste = (int)Math.Floor(waste);
                city.Corruption = (int)Math.Floor(corruption);
            }

            city.TotalProduction = totalSheilds;
            city.Trade = totalTrade - city.Corruption;
            city.Production = totalSheilds - city.Support - city.Waste;
            city.FoodConsumption = city.Size * game.Rules.Cosmic.FoodEatenPerTurn +
                                   city.SupportedUnits.Count(u => u.AiRole == AiRoleType.Settle) *
                                   game.Rules.Governments[government].SettlersConsumption;
            city.FoodProduction = totalFood;
            city.SurplusHunger = totalFood - city.FoodConsumption;



            city.Pollution = CalculatePollution(city);
            city.CalculateHappiness(game);
        }

        private static int CalculatePollution(City city)
        {
            var smokestackPoints = 0;

            if (!city.Improvements.Any(i => i.Effects.ContainsKey(Effects.EliminateIndustrialPollution)))
            {
                var modifier = 1 + city.Improvements
                                   .Where(i => i.Effects.ContainsKey(Effects.IndustrialPollutionModifier))
                                   .Sum(i => i.Effects[Effects.IndustrialPollutionModifier]) +
                               city.Owner.GlobalEffects.GetValueOrDefault(
                                   Effects.IndustrialPollutionModifier, 0);
                if (modifier > 0)
                {
                    smokestackPoints = Math.Max((city.Production / modifier) - 20, 0);
                }
            }

            if (!city.Improvements.Any(i => i.Effects.ContainsKey(Effects.EliminatePopulationPollution)))
            {
                var sum = city.Improvements
                    .Where(i => i.Effects.ContainsKey(Effects.PopulationPollutionModifier))
                    .Sum(i => i.Effects[Effects.PopulationPollutionModifier]);
                var modifier = sum +
                               city.Owner.GlobalEffects.GetValueOrDefault(
                                   Effects.PopulationPollutionModifier, 0);
                if (modifier > 0)
                {
                    smokestackPoints += city.Size * modifier / 4;
                }
            }

            return smokestackPoints;
            // [Industrial Pollution + Pop. Pollution]
            // Industrial Pollution = (Shield (Civ2).png Production generated by city / Modifier) - 20
            // Modifier = 2 if city has Hydro Plant or Nuclear Plant;
            // Modifier = 3 if city has a Recycling Center;
            // Industrial Pollution = 0; if city has a Solar Plant;
            // Population Pollution = (City Size x Pollution Modifier.) / 4
            // Pollution Modifiers = 0.0 by default
            //     +1 with Industrialization;
            // +1 with Automobile;
            // +1 with Mass Production;
            // +1 with Plastics;
            // +1 with Sanitation NOT discovered after Industrialization;
            // -1 with Environmentalism
            //     -1 with Solar Plant in the city.
            //     Population Pollution = 0; if city has Mass Transit
        }

        public static void SetUnitSupport(this City city, Government government)
        {
            var freeSupport = city.FreeSupport(government);
            var supportFreeTypes = government.UnitTypesAlwaysFree;
            city.SupportedUnits.ForEach(unit =>
            {
                unit.NeedsSupport = !unit.FreeSupport(supportFreeTypes) && freeSupport <= 0;
                freeSupport--;
            });
        }

        private static int FreeSupport(this City city, Government government)
        {
            var support = government.NumberOfFreeUnitsPerCity;
            return support == -1 ? city.Size : support;
        }

        private static double ComputeDistanceFactor(City city, int governmentIndex, IGame game)
        {
            if (city.ImprovementExists(Effects.Capital)) return 0; //Capital is always at 0 distance  

            var government = game.Rules.Governments[governmentIndex];
            double distance = government.Distance;
            if (distance >= 0)
                return distance; // if distance is fixed for govt (Communism, Fundamentalism or Democracy)


            Func<City, double> calculateDistance = government.Level < 1
                ? c => Utilities.DistanceTo(c.Location, city.Location) + game.DifficultyLevel // TODO: work out the actual distance modifier based on difficulty
                : c => Utilities.DistanceTo(c.Location, city.Location);

            distance =
                city.Owner.Cities.Where(c => c.ImprovementExists(Effects.Capital))
                    .Select(calculateDistance)
                    .Where(d => d <= game.MaxDistance)
                    .DefaultIfEmpty(game.MaxDistance)
                    .Min();

            return distance;
        }

        public static bool SellImprovement(this City city, Improvement improvement)
        {
            if (city.ImprovementSold)
            {
                return false;
            }
            //Since effects are computed by checking improvements removing improvement removes all effects

            city.OrderedImprovements.Remove(improvement.Type);
            city.ImprovementSold = true;
            return true;
        }

        public static int GetSaleValue(this Improvement improvement, Rules rules) =>
            improvement.Cost * rules.Cosmic.RowsShieldBox;

        public static void AddImprovement(this City city, Improvement improvement) =>
            city.OrderedImprovements.Add(improvement.Type, improvement);

        public static bool ImprovementExists(this City city, int improvement) =>
            city.OrderedImprovements.ContainsKey(improvement);

        public static bool ImprovementExists(this City city, Effects improvement) =>
            city.OrderedImprovements.Values.Any(i => i.Effects.ContainsKey(improvement));

        /// <summary>
        /// Civ II stalls a city at <see cref="CosmicRules.ToExceedCitySizeAqueductNeeded"/>
        /// without an Aqueduct and at <see cref="CosmicRules.SewerNeeded"/> without
        /// a Sewer System. A city already over a cap does not shrink, it simply
        /// cannot grow further until the works are built.
        /// </summary>
        public static bool CanGrow(this City city, Rules rules)
        {
            if (city.Size >= rules.Cosmic.SewerNeeded &&
                !city.ImprovementExists((int)ImprovementType.SewerSystem))
            {
                return false;
            }

            return city.Size < rules.Cosmic.ToExceedCitySizeAqueductNeeded ||
                   city.ImprovementExists((int)ImprovementType.Aqueduct);
        }

        public static void ShrinkCity(this City city, IGame game)
        {
            city.Size -= 1;
            if (city.Size <= 0)
            {
                //Destroy city
                var location = city.Location;
                location.CityHere = null;
                city.Owner.Cities.Remove(city);
                game.AllCities.Remove(city);

                // Setting Tile.WorkedBy removes that tile from City.WorkedTiles as a side-effect.
                // Enumerate a snapshot so razing/shrinking a city during combat cannot modify the
                // collection currently being walked.
                foreach (var workedTile in city.WorkedTiles.ToList())
                {
                    workedTile.WorkedBy = null;
                }

                city.EliminateCityUnits(game);

                // The map draws cities from each player's remembered copy of the tile.
                // UpdateTiles only refreshes players who can currently see it, so anyone
                // watching from a distance went on being shown a city that no longer
                // exists. A razing is worth telling everyone who knew the place about.
                foreach (var player in game.Players)
                {
                    var civId = player.Civilization.Id;
                    if (location.PlayerKnowledge is { } knowledge &&
                        civId < knowledge.Length && knowledge[civId] != null)
                    {
                        location.UpdatePlayer(civId);
                        player.MapChanged([location]);
                    }
                }
            }
            else
            {
                city.AutoRemoveWorkersDistribution(game.Rules);
                city.CalculateOutput(city.Owner.Government, game);
            }
        }

        internal static void EliminateCityUnits(this City city, IGame game)
        {
            var unitsEliminated = city.SupportedUnits.ToList();
            if (unitsEliminated.Count <= 0) return;
            
            foreach (var unit in unitsEliminated)
            {
                unit.Dead = true;
            }

            game.Players[city.OwnerId].UnitsLost(unitsEliminated);
        }

        public static void GrowCity(this City city, IGame game)
        {
            city.Size += 1;

            city.AutoAddDistributionWorkers(game.Rules); // Automatically add a workers on a tile
            city.CalculateOutput(city.Owner.Government, game);

            game.UpdateTiles(new List<Tile> { city.Location });
        }

        public static void ResetFoodStorage(this City city, int foodRows)
        {
            city.FoodInStorage = 0;


            var totalStorage = city.GetFoodStorage();

            if (totalStorage == 0) return;

            var maxFood = (city.Size + 1) * foodRows;
            city.FoodInStorage += maxFood * totalStorage / 100;
        }

        public static int GetFoodStorage(this City city)
        {
            var storageBuildings = city.Improvements
                .Where(i => i.Effects.ContainsKey(Effects.FoodStorage))
                .Select(b => b.Effects[Effects.FoodStorage]).ToList();

            // The Pyramids act as a granary in every city their owner holds.
            var wonderStorage = WonderFunctions.GetFoodStorageBonus(city);
            if (wonderStorage > 0)
            {
                storageBuildings.Add(wonderStorage);
            }

            if (storageBuildings.Count <= 0) return 0;

            var totalStorage = storageBuildings.Sum();
            if (totalStorage is > 100 or < 0)
            {
                totalStorage = storageBuildings.Where(v => v is >= 0 and <= 100).Max();
            }

            return totalStorage;
        }

        public static void AutoRemoveWorkersDistribution(this City city, Rules gameRules)
        {
            // Civ II removes a specialist before taking a citizen off the map.
            if (city.NoOfSpecialistsx4 >= 4)
            {
                city.NoOfSpecialistsx4 -= 4;
                city.SpecialistTypes = city.SpecialistTypes.Take(city.NoOfSpecialistsx4 / 4).ToArray();
                return;
            }

            var tiles = city.WorkedTiles.Where(t => t != city.Location).ToList();
            if (tiles.Count == 0)
            {
                return;
            }

            var organization = city.GetOrganizationLevel(gameRules);

            var unworked = tiles.OrderBy(t =>
                t.GetFood(organization == 0) + t.GetShields(organization == 0) +
                t.GetTrade(organization)).First();

            unworked.WorkedBy = null;
        }

        /// <summary>
        /// Take a citizen off the land and make it a specialist, which is Civ II's
        /// direct answer to civil disorder: the new entertainer stops working a tile
        /// and starts producing luxury instead. The tile given up is the least
        /// productive one the city works, and never the city centre.
        /// </summary>
        public static bool MakeSpecialist(this City city, Rules gameRules)
        {
            if (city.NoOfSpecialistsx4 / 4 >= city.Size)
            {
                return false;
            }

            var tiles = city.WorkedTiles.Where(t => t != city.Location).ToList();
            if (tiles.Count == 0)
            {
                return false;
            }

            var organization = city.GetOrganizationLevel(gameRules);
            var worst = tiles.OrderBy(t =>
                t.GetFood(organization == 0) + t.GetShields(organization == 0) +
                t.GetTrade(organization)).First();

            worst.WorkedBy = null;
            city.NoOfSpecialistsx4 += 4;

            // GetSpecialistTypes normalises the array to the new count, defaulting
            // the added entry to an entertainer.
            city.GetSpecialistTypes();
            return true;
        }

        /// <summary>
        /// Put a specialist back to work on the best free tile in the city radius.
        /// </summary>
        public static bool MakeWorker(this City city, Rules gameRules)
        {
            if (city.NoOfSpecialistsx4 < 4)
            {
                return false;
            }

            city.NoOfSpecialistsx4 -= 4;
            city.GetSpecialistTypes();
            city.AutoAddDistributionWorkers(gameRules);
            return true;
        }

        /// <summary>
        /// Units, buildings and wonders are three separate things to be building.
        /// </summary>
        private static int ProductionCategory(IProductionOrder order) =>
            order is BuildingProductionOrder { Improvement.IsWonder: true } ? 2
                : order.Type == ItemType.Unit ? 0 : 1;

        /// <summary>
        /// Switch what a city is building, charging Civ II's penalty for crossing
        /// between units, buildings and wonders. Switching within a category is
        /// free, and the penalty is charged once a turn however often the choice
        /// changes. The rate comes from the ruleset's own ShieldPenaltyTypeChange,
        /// which was parsed and then never read.
        /// </summary>
        /// <summary>
        /// Shields that switching to <paramref name="next"/> would cost, without
        /// charging them. Civ II takes a share of the accumulated shields when the
        /// change crosses between units, buildings and wonders, and only once a
        /// turn -- so the player should be told before it happens, not after.
        /// </summary>
        public static int ProductionChangePenalty(this City city, IProductionOrder? next, Rules rules)
        {
            var current = city.ItemInProduction;
            if (next == null || current == null || ReferenceEquals(next, current) ||
                city.ProductionChanged ||
                ProductionCategory(current) == ProductionCategory(next))
            {
                return 0;
            }

            var penalty = Math.Clamp(rules.Cosmic.ShieldPenaltyTypeChange, 0, 100);
            return Math.Max(0, city.ShieldsProgress) * penalty / 100;
        }

        public static void ChangeProduction(this City city, IProductionOrder next, Rules rules)
        {
            var current = city.ItemInProduction;
            if (next == null || ReferenceEquals(next, current))
            {
                return;
            }

            if (current != null && !city.ProductionChanged &&
                ProductionCategory(current) != ProductionCategory(next))
            {
                var penalty = Math.Clamp(rules.Cosmic.ShieldPenaltyTypeChange, 0, 100);
                city.ShieldsProgress -= city.ShieldsProgress * penalty / 100;
                city.ProductionChanged = true;
            }

            city.ItemInProduction = next;
        }

        public static void AutoAddDistributionWorkers(this City city, Rules gameRules)
        {
            // First determine how many workers are to be added
            var specialists = Math.Clamp(city.NoOfSpecialistsx4 / 4, 0, city.Size);
            int workersToBeAdded = city.Size + 1 - specialists - city.WorkedTiles.Count;
            if (workersToBeAdded <= 0)
            {
                return;
            }

            var organization = city.GetOrganizationLevel(gameRules);
            
            var lowOrganization = organization == 0;

            // Make a list of tiles where you can add workers
            var tilesToAddWorkersTo = new List<Tile>();

            var tileValue = new List<double>();
            foreach (var tile in city.Location.CityRadius().Where(t =>
                         t.WorkedBy == null && t.IsVisible(city.OwnerId) &&
                         !t.UnitsHere.Any<Unit>(u => u.Owner != city.Owner && u.AttackBase > 0) && t.CityHere == null))
            {
                var food = tile.GetFood(lowOrganization) * 1.5;
                var shields = tile.GetShields(lowOrganization);
                var trade = tile.GetTrade(organization) * 0.5;

                var total = food + shields + trade;
                var insertionIndex = tilesToAddWorkersTo.Count;
                for (; insertionIndex > 0; insertionIndex--)
                {
                    if (tileValue[insertionIndex - 1] >= total)
                    {
                        break;
                    }
                }

                if (insertionIndex == tilesToAddWorkersTo.Count)
                {
                    if (insertionIndex >= workersToBeAdded) continue;

                    tilesToAddWorkersTo.Add(tile);
                    tileValue.Add(total);
                }
                else
                {
                    tilesToAddWorkersTo.Insert(insertionIndex, tile);
                    tileValue.Insert(insertionIndex, total);
                }
            }

            foreach (var tile in tilesToAddWorkersTo.Take(workersToBeAdded))
            {
                tile.WorkedBy = city;
            }
        }

        public static int GetPopulation(this City city)
        {
            var population = 0;
            for (int i = 1; i <= city.Size; i++)
                population += i * 10000;
            return population;
        }

        public static int GetOrganizationLevel(this City city, Rules rules)
        {
            var baseLevel = rules.Governments[city.Owner.Government].Level;
            return city.WeLoveKingDay ? baseLevel + 1 : baseLevel;
        }

        public static bool IsNextToOcean(this City city) =>
        city.Location.Neighbours().Any(t => t.Type == TerrainType.Ocean);

        public static bool IsNextToRiver(this City city) =>
        city.Location.Neighbours().Any(t => t.River);
    }
}
