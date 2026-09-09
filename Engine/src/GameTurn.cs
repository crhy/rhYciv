using System;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Production;
using Model.Constants;
using Model.Core.Cities;
using Model.Core.Player;

namespace RhyCiv.Engine
{
    public static class GameTurn
    {
        /// <summary>
        /// Updates the stats of all cities for the active player's turn.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="player">The active player.</param>
        /// <remarks>
        /// This method performs the following actions for each city:
        /// - Updates food storage and city size based on surplus/deficit
        /// - Handles civil disorder and "We Love the King Day" events
        /// - Manages production and item completion
        /// - Collects taxes and pays for city improvements
        /// - Contributes to research progress
        /// </remarks>
        public static void CitiesTurn(this Game game, IPlayer player)
        {
            var activeCiv = game.GetActiveCiv;
            var rules = game.Rules;
            
            var foodRows = rules.Cosmic.RowsFoodBox;
            var shieldRows = rules.Cosmic.RowsShieldBox;

            // A city that starves to nothing removes itself from this list, so walk a
            // snapshot: enumerating the live collection threw the moment any city was
            // lost to famine during its owner's turn.
            foreach (var city in activeCiv.Cities.ToList())
            {
                if (city.Size <= 0 || !activeCiv.Cities.Contains(city))
                {
                    continue;
                }

                city.ImprovementSold = false;
                city.ProductionChanged = false;

                // Change food in storage
                city.FoodInStorage += city.SurplusHunger;

                var shields = city.Production;

                //TODO: Combine these calls
                var tax = city.GetTax();
                var science = city.GetScience();

                // Change city size
                if (city.FoodInStorage < 0)
                {
                    city.FoodInStorage = 0;
                    Starve(game, city, player);
                }
                else if (city.SurplusHunger < 0 && city.FoodInStorage + city.SurplusHunger < 0)
                {
                    player.FoodShortage(city);
                }
                else
                {
                    var maxFood = (city.Size + 1) * foodRows;
                    if (city.FoodInStorage >= maxFood)
                    {
                        if (city.CanGrow(rules))
                        {
                            // GrowCity reports the square itself, so the size drawn
                            // beside the city's name follows it up.
                            city.GrowCity(game);
                            city.ResetFoodStorage(foodRows);
                        }
                        else
                        {
                            // Civ II keeps the food box full and stalls the city
                            // until an Aqueduct / Sewer System is built.
                            city.FoodInStorage = maxFood;
                            player.CityGrowthHalted(city);
                        }
                    }
                }

                var happiness = city.CalculateHappiness(game);
                if (happiness.UnhappyCitizens > 0)
                {
                    if (city.WeLoveKingDay)
                    {
                        player.WeLoveTheKingCanceled(city);
                        city.WeLoveKingDay = false;
                    }

                    if (happiness.IsInDisorder)
                    {
                        player.CivilDisorder(city);
                        city.CivilDisorder = true;
                        continue;
                    }

                    if (city.CivilDisorder)
                    {
                        city.CivilDisorder = false;
                        player.OrderRestored(city);
                    }

                }
                else
                {
                    if (city.CivilDisorder)
                    {
                        city.CivilDisorder = false;
                        player.OrderRestored(city);
                    }

                    if (happiness.CanCelebrate(city.Size))
                    {
                        if (!city.WeLoveKingDay)
                        {
                            player.WeLoveTheKingStarted(city);
                        }

                        city.WeLoveKingDay = true;
                    }
                }

                if (!ProductionPossibilities.ProductionValid(city))
                {
                    var newItem = ProductionPossibilities.AutoNext(city);

                    player.CantProduce(city, newItem);

                    if (newItem != null)
                    {
                        city.ItemInProduction = newItem;
                    }
                }

                city.ShieldsProgress += shields;


                // RULES.txt costs are already shields: Warriors 10, Temple 40,
                // Cure for Cancer 600, exactly as Civ II charges. Multiplying by the
                // shield box's row count made everything ten times its price, so a
                // city producing a handful of shields a turn never finished anything.
                if (city.ShieldsProgress >= city.ItemInProduction.Cost)
                {
                    if (city.ItemInProduction.CompleteProduction(city, rules))
                    {
                        city.ShieldsProgress = 0;

                        var government = rules.Governments[city.Owner.Government];
                        city.SetUnitSupport(government);
                        city.CalculateOutput(city.Owner.Government, game);

                        // Something has appeared on the square, and building a
                        // settler has taken a citizen off the city as well, so the
                        // map's idea of this tile is out of date either way. Without
                        // this a city that made a settler kept its old size beside
                        // its name until the next time the square happened to be
                        // redrawn.
                        game.UpdateTiles([city.Location]);

                        GrantWonderCompletionAdvances(game, city, player);

                        player.CityProductionComplete(city);
                    }
                }

                activeCiv.Money += tax;

                foreach (var cityImprovement in city.Improvements)
                {
                    // A. Smith's Trading Co. covers every building that costs one gold.
                    if (WonderFunctions.PaysUpkeepFor(activeCiv, cityImprovement.Upkeep))
                    {
                        continue;
                    }

                    if (cityImprovement.Upkeep > 0)
                    {
                        if (activeCiv.Money >= cityImprovement.Upkeep)
                        {
                            activeCiv.Money -= cityImprovement.Upkeep;
                        }
                        else
                        {
                            //Sell it !!
                            city.SellImprovement(cityImprovement);
                            activeCiv.Money += cityImprovement.GetSaleValue(rules);
                            player.CantMaintain(city, cityImprovement);
                        }
                    }
                }

                if (science > 0)
                {
                    activeCiv.Science += science;
                }
            }

            ResolveResearch(game, player);
        }

        /// <summary>
        /// A city whose food store has run dry.
        /// <para>
        /// Civ II eats a settler before it eats a citizen: a city supporting
        /// Settlers or Engineers disbands one of them, and only loses a population
        /// point when it has none to give up. That is what makes over-building
        /// settlers survivable, and it was missing -- the city went straight to
        /// losing a citizen while the settlers that were eating its food carried on
        /// as though nothing had happened.
        /// </para>
        /// </summary>
        private static void Starve(Game game, City city, IPlayer player)
        {
            var settler = city.SupportedUnits
                .FirstOrDefault(unit => !unit.Dead && unit.AiRole == AiRoleType.Settle);

            if (settler == null)
            {
                city.ShrinkCity(game);
                game.UpdateTiles([city.Location]);
                player.CityDecrease(city);
                return;
            }

            settler.Dead = true;
            city.SetUnitSupport(game.Rules.Governments[city.Owner.Government]);
            city.CalculateOutput(city.Owner.Government, game);
            player.UnitLost(settler, null);
        }

        /// <summary>
        /// Civ II's Darwin's Voyage delivers two immediate technology advances the
        /// moment it is completed. It is a one-off, so it is resolved here at the
        /// point of construction rather than from a per-turn hook.
        /// </summary>
        private static void GrantWonderCompletionAdvances(Game game, City city, IPlayer player)
        {
            if (city.ItemInProduction is not BuildingProductionOrder
                {
                    Improvement.Type: (int)ImprovementType.DarwinVoyage
                })
            {
                return;
            }

            for (var i = 0; i < 2; i++)
            {
                var options = AdvanceFunctions.CalculateAvailableResearch(game, city.Owner);
                if (options.Count == 0)
                {
                    break;
                }

                var advance = options
                    .OrderByDescending(a => a.AIvalue)
                    .ThenBy(a => a.Index)
                    .First();

                game.GiveAdvance(advance.Index, city.Owner);
                player.NotifyAdvanceResearched(advance.Index);
            }
        }

        private static void ResolveResearch(Game game, IPlayer player)
        {
            var activeCiv = game.GetActiveCiv;
            if (activeCiv.Cities.Count == 0)
            {
                return;
            }

            if (activeCiv.ReseachingAdvance < 0)
            {
                var researchPossibilities = AdvanceFunctions.CalculateAvailableResearch(game, activeCiv);
                if (researchPossibilities.Count > 0)
                {
                    player.SelectNewAdvance(researchPossibilities);
                }
                return;
            }

            var currentScienceCost = AdvanceFunctions.CalculateScienceCost(game, activeCiv);
            if (currentScienceCost > 0 && activeCiv.Science >= currentScienceCost)
            {
                var completedAdvance = activeCiv.ReseachingAdvance;
                game.GiveAdvance(completedAdvance, activeCiv);
                activeCiv.Science = Math.Max(0, activeCiv.Science - currentScienceCost);

                // What to research next has to be worked out after the advance is
                // granted, so its own unlocks are on the list. Both of these push a
                // window and the last one pushed is the one the player meets first,
                // so the discovery goes second: announce the advance, then ask what
                // follows it, rather than asking before saying what was found.
                var researchPossibilities = AdvanceFunctions.CalculateAvailableResearch(game, activeCiv);
                if (researchPossibilities.Count > 0)
                {
                    player.SelectNewAdvance(researchPossibilities);
                }

                player.NotifyAdvanceResearched(completedAdvance);
            }
        }
    }
}
