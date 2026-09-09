using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Production;
using RhyCiv.Engine.Scripting;
using RhyCiv.Engine.Scripting.ScriptObjects;
using RhyCiv.Engine.Scripting.UnitActions;
using RhyCiv.Engine.Terrains;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Engine.Units;
using Model.Constants;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GoodyHuts.Outcomes;
using Model.Core.Mapping;
using Model.Core.Player;
using Model.Core.Production;
using Model.Core.Units;
using Neo.IronLua;

namespace RhyCiv.Engine
{
    public class AiPlayer(int difficultyLevel, Civilization civilization, Tile tile0, Game game, AiInterface ai)
        : IPlayer
    {
        public AiInterface Ai { get; } = ai;
        public Civilization Civilization { get; } = civilization;

        public int DifficultyLevel { get; } = difficultyLevel;

        public void WeLoveTheKingCanceled(City city)
        {
        }

        public Tile ActiveTile { get; set; } = civilization.Units.FirstOrDefault()?.CurrentLocation ??
                                               civilization.Cities.FirstOrDefault()?.Location ?? tile0;

        public Unit? ActiveUnit { get; private set; }

        public List<Unit> WaitingList { get; } = [];

        public void CivilDisorder(City city)
        {
        }

        public void OrderRestored(City city)
        {
        }

        public void WeLoveTheKingStarted(City city)
        {
        }

        public void CantMaintain(City city, Improvement cityImprovement)
        {
        }

        public void SelectNewAdvance(List<Advance> researchPossibilities)
        {
            var res = Ai.Call(AiEvent.ResearchComplete,
                new LuaTable
                {
                    {
                        "researchPossibilities",
                        LuaTable.pack(researchPossibilities.Select(object (a) => new Tech(game.Rules.Advances, a.Index))
                            .ToArray())
                    }
                });
            if (res is not null && res.Count > 0)
            {
                Civilization.ReseachingAdvance = res.Values[0] switch
                {
                    Tech tech => tech.id,
                    Advance advance => advance.Index,
                    int index and >= 0 when index < researchPossibilities.Count => researchPossibilities[index].Index,
                    _ => game.Random.ChooseFrom(researchPossibilities).Index
                };
            }
        }

        public void CantProduce(City city, IProductionOrder? newItem)
        {
            // The caller applies newItem when it found one; only step in when it
            // could not, otherwise the city keeps building something invalid.
            if (newItem != null)
            {
                return;
            }

            var replacement = ChooseProduction(city, ProductionPossibilities.GetAllowedProductionOrders(city));
            if (replacement != null)
            {
                city.ItemInProduction = replacement;
            }
        }

        public void CityProductionComplete(City city)
        {
            var productionOrders = ProductionPossibilities.GetAllowedProductionOrders(city);
            if (productionOrders.Count == 0)
            {
                return;
            }

            var result = Ai.Call(AiEvent.CityProductionComplete,
                new LuaTable { { "city", city }, { "productionOrders", productionOrders } });

            // A script may answer with an order, an index into the list, or a title.
            // Without an answer the built-in heuristic decides, so an AI civilisation
            // always chooses deliberately rather than falling through to the
            // cheapest item of whatever it happened to build last.
            var chosen = ResolveProductionChoice(result, productionOrders)
                         ?? ChooseProduction(city, productionOrders);
            if (chosen != null)
            {
                city.ItemInProduction = chosen;
            }
        }

        private static IProductionOrder? ResolveProductionChoice(LuaResult? result,
            IList<IProductionOrder> orders)
        {
            if (result is not { Count: > 0 })
            {
                return null;
            }

            switch (result[0])
            {
                case IProductionOrder order when orders.Contains(order):
                    return order;
                case string title:
                    return orders.FirstOrDefault(o =>
                        string.Equals(o.Title, title, StringComparison.OrdinalIgnoreCase));
                case int index:
                    return index >= 0 && index < orders.Count ? orders[index] : null;
                case long longIndex:
                    return longIndex >= 0 && longIndex < orders.Count ? orders[(int)longIndex] : null;
                case double number:
                {
                    var index = (int)number;
                    return index >= 0 && index < orders.Count ? orders[index] : null;
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// How many cities this civilisation tries to found before it stops
        /// prioritising settlers. Harder opponents expand further, which is the
        /// main lever Civ II uses to make higher difficulties harder.
        /// </summary>
        private int ExpansionTarget => 6 + DifficultyLevel * 2;

        /// <summary>
        /// The built-in production heuristic: garrison the city, expand while the
        /// empire is small, then build infrastructure, and fall back to offence.
        /// </summary>
        private IProductionOrder? ChooseProduction(City city, IList<IProductionOrder> orders)
        {
            if (orders.Count == 0)
            {
                return null;
            }

            var units = orders.OfType<UnitProductionOrder>().ToList();
            var buildings = orders.OfType<BuildingProductionOrder>()
                .Where(b => !b.Improvement.IsWonder)
                .ToList();

            var garrison = city.Location.UnitsHere
                .Count(u => !u.Dead && u.Owner == Civilization && u.DefenseBase > 0);
            var wantedGarrison = 1 + city.Size / 6;
            if (garrison < wantedGarrison)
            {
                var defender = Cheapest(units.Where(u => u.UnitDefinition.AIrole == AiRoleType.Defend));
                if (defender != null)
                {
                    return defender;
                }
            }

            // Founding a city costs a population point, so never squeeze out a
            // settler from a city that would disappear.
            if (city.Size > 1 && Civilization.Cities.Count < ExpansionTarget)
            {
                var settler = Cheapest(units.Where(u => u.UnitDefinition.IsSettler));
                if (settler != null)
                {
                    return settler;
                }
            }

            var building = Cheapest(buildings);
            if (building != null)
            {
                return building;
            }

            return Cheapest(units.Where(u => u.UnitDefinition.Attack > 0)) ?? Cheapest(orders);
        }

        private static IProductionOrder? Cheapest(IEnumerable<IProductionOrder> orders) =>
            orders.MinBy(o => o.Cost);

        public IInterfaceCommands Ui { get; } = null!;
        public string AIScript { get; set; } = civilization.PlayerType == PlayerType.Barbarians ? "barbarian.ai" : "default.ai";

        public void NotifyImprovementEnabled(TerrainImprovement improvement, int level)
        {
        }

        public void MapChanged(List<Tile> tiles)
        {
        }

        public void WaitingAtEndOfTurn()
        {
            Ai.Call(AiEvent.TurnEnd, new LuaTable());
            game.ChoseNextCiv();
        }

        public void NotifyAdvanceResearched(int advance)
        {
        }

        /// <summary>
        /// A computer civilisation takes the most advanced government it can form,
        /// which is the order the rules list them in. It has no reason to dither:
        /// the anarchy is already paid for by the time this is asked.
        /// </summary>
        public void ChooseGovernment(IList<int> availableGovernments)
        {
            if (availableGovernments.Count == 0)
            {
                return;
            }

            GovernmentFunctions.AdoptGovernment(game, Civilization,
                (Enums.GovernmentType)availableGovernments.Max());
        }

        /// <summary>
        /// Revolts as soon as something better is available. A computer
        /// civilisation left under Despotism all game is not playing the same game
        /// as the player.
        /// </summary>
        public void GovernmentAvailable(int government)
        {
            if (GovernmentFunctions.CanRevolt(Civilization))
            {
                GovernmentFunctions.BeginRevolution(game, Civilization);
            }
        }

        public void FoodShortage(City city)
        {
        }

        public void CityGrowthHalted(City city)
        {
        }

        public void CityPolluted(City city, Tile square)
        {
        }

        public void WonderBegun(City city, Improvement wonder)
        {
        }

        public void WonderNearlyComplete(City city, Improvement wonder)
        {
        }

        public void WonderCompleted(City city, Improvement wonder)
        {
        }

        public void WonderLost(City ourCity, Improvement wonder, City builtIn)
        {
        }

        public void WonderCaptured(City city, Improvement wonder)
        {
        }

        public void GlobalWarming(int squaresChanged)
        {
        }

        public void CivilizationDestroyed()
        {
        }


        public void CivilizationVictorious()
        {
        }
        public void CityDecrease(City city)
        {
        }

        public void TurnStart(int turnNumber)
        {
            WaitingList.Clear();
            Ai.Call(AiEvent.TurnStart, new LuaTable { { "Turn", turnNumber } });
        }

        public void SetUnitActive(Unit? unit, bool move)
        {
            ActiveUnit = unit;
            if (unit == null) return;

            if (unit.CurrentLocation != null) ActiveTile = unit.CurrentLocation;
            if (!move) return;

            var safety = 0;
            while (!unit.Dead && unit.MovePoints > 0 && safety++ < 12)
            {
                var oldTile = unit.CurrentLocation;
                var oldMovesLost = unit.MovePointsLost;
                var oldOrder = unit.Order;

                var action = GetScriptedAction(unit) ?? GetFallbackAction(unit) ?? new NothingAction(unit, game);
                action.Execute();

                if (unit.Dead || unit.MovePoints <= 0)
                {
                    break;
                }

                if (unit.CurrentLocation == oldTile && unit.MovePointsLost == oldMovesLost && unit.Order == oldOrder)
                {
                    unit.SkipTurn();
                    break;
                }
            }

            // The unit has had its go. If it is somehow still awaiting orders -- it
            // used its moves up on actions that left it with points it cannot spend,
            // or the loop above ran out of attempts -- its turn is ended here. The
            // selector offers whatever is still awaiting orders, so a unit that came
            // back from this unfinished would be offered again, and again, and the
            // computer civilisation's turn would never end.
            if (!unit.Dead && unit.AwaitingOrders)
            {
                unit.SkipTurn();
            }
        }

        private UnitAction? GetScriptedAction(Unit unit)
        {
            var result = Ai.Call(AiEvent.UnitOrdersNeeded, new LuaTable { { "Unit", new UnitApi(unit, game) } });
            if (result is not LuaResult { Count: > 0 } luaResult)
            {
                return null;
            }

            return luaResult[0] switch
            {
                UnitAction luaAction => luaAction,
                TileApi tile => TileToAction(tile.BaseTile, unit),
                "B" or "b" => new BuildCityAction(unit, game),
                "F" or "f" => new FortifyAction(unit, game),
                "W" or "w" => new WaitAction(unit, game, this),
                _ => null
            };
        }

        private UnitAction? GetFallbackAction(Unit unit)
        {
            if (unit.CurrentLocation == null || unit.Dead)
            {
                return null;
            }

            var currentTile = unit.CurrentLocation;

            if (unit.AttackBase > 0)
            {
                var adjacentEnemy = AdjacentEnemyAction(unit, currentTile);
                if (adjacentEnemy != null)
                {
                    return adjacentEnemy;
                }
            }

            if (unit.AiRole == AiRoleType.Settle || unit.TypeDefinition.IsSettler)
            {
                return SettlerAction(unit, currentTile);
            }

            if (unit.AiRole == AiRoleType.Defend && currentTile.CityHere?.Owner == unit.Owner)
            {
                return new FortifyAction(unit, game);
            }

            if (unit.AttackBase > 0)
            {
                var targetAction = MoveTowardNearestEnemy(unit, currentTile);
                if (targetAction != null)
                {
                    return targetAction;
                }
            }

            return ExploreAction(unit, currentTile);
        }

        private UnitAction? AdjacentEnemyAction(Unit unit, Tile currentTile)
        {
            var attackTile = MovementFunctions.GetPossibleMoves(currentTile, unit)
                .Where(tile => IsEnemyTile(unit, tile))
                .OrderBy(tile => tile.CityHere == null ? 1 : 0)
                .ThenByDescending(tile => tile.UnitsHere.Any(other => other.AttackBase > 0))
                .FirstOrDefault();

            return attackTile == null ? null : TileToAction(attackTile, unit);
        }

        /// <summary>
        /// How many cities a civilisation founds before its settlers stop looking
        /// for more land and start working the land it has. Civ II's computer
        /// players expand first and improve afterwards; below this they are still
        /// expanding.
        /// </summary>
        private const int CitiesBeforeWorkingTheLand = 4;

        private UnitAction? SettlerAction(Unit unit, Tile currentTile)
        {
            // Pollution is cleaned wherever it is found, however early it is: it
            // halves what the square yields and it is what warms the world.
            var cleanUp = ImprovementWorkHere(unit, currentTile, cleanOnly: true);
            if (cleanUp != null)
            {
                return cleanUp;
            }

            if (unit.Owner.Cities.Count >= CitiesBeforeWorkingTheLand)
            {
                var work = ImprovementWorkHere(unit, currentTile, cleanOnly: false);
                if (work != null)
                {
                    return work;
                }

                var towardWork = MoveTowardWork(unit, currentTile);
                if (towardWork != null)
                {
                    return towardWork;
                }
            }

            if (CanFoundUsefulCity(unit, currentTile) && ShouldFoundCityNow(unit, currentTile))
            {
                return new BuildCityAction(unit, game);
            }

            var destination = MovementFunctions.GetPossibleMoves(currentTile, unit)
                .Where(tile => IsSafeForNonCombatUnit(unit, tile))
                .Where(tile => tile.Type != TerrainType.Ocean && !tile.Terrain.Impassable)
                .OrderByDescending(tile => CitySiteScore(unit, tile))
                .FirstOrDefault();

            if (destination != null && CitySiteScore(unit, destination) >= CitySiteScore(unit, currentTile))
            {
                return new MoveAction(unit, destination, game);
            }

            if (CanFoundUsefulCity(unit, currentTile))
            {
                return new BuildCityAction(unit, game);
            }

            return ExploreAction(unit, currentTile, avoidEnemies: true);
        }

        /// <summary>
        /// Sets the unit to work on the square it stands on, if there is anything
        /// there worth doing. Pollution first, then whatever the terrain will take.
        /// </summary>
        private UnitAction? ImprovementWorkHere(Unit unit, Tile tile, bool cleanOnly)
        {
            if (tile.CityHere != null || unit.Building != 0)
            {
                return null;
            }

            var improvement = WorkFor(unit, tile, cleanOnly);
            return improvement == null ? null : new BuildImprovementAction(unit, improvement, game);
        }

        /// <summary>
        /// What a worker would do to this square, or nothing if it is finished.
        /// <para>
        /// The order is Civ II's own sense of priorities: clear the pollution, then
        /// irrigate, mine and road. An improvement in an exclusive group that the
        /// square already carries something from is skipped -- mining a square that
        /// is already irrigated replaces the irrigation, and a worker that did that
        /// would spend the rest of the game undoing itself.
        /// </para>
        /// </summary>
        private TerrainImprovement? WorkFor(Unit unit, Tile tile, bool cleanOnly)
        {
            int[] wanted = cleanOnly
                ? [ImprovementTypes.Pollution]
                : [ImprovementTypes.Pollution, ImprovementTypes.Irrigation, ImprovementTypes.Mining,
                   ImprovementTypes.Road];

            foreach (var id in wanted)
            {
                if (!game.TerrainImprovements.TryGetValue(id, out var improvement))
                {
                    continue;
                }

                if (!improvement.Negative && improvement.ExclusiveGroup > 0 &&
                    tile.Improvements.Any(existing => existing.Group == improvement.ExclusiveGroup &&
                                                      existing.Improvement != improvement.Id))
                {
                    continue;
                }

                if (TerrainImprovementFunctions.CanImprovementBeBuiltHere(tile, improvement, unit.Owner).Enabled)
                {
                    return improvement;
                }
            }

            return null;
        }

        /// <summary>
        /// A step towards the nearest square of the civilisation's own land that
        /// wants work. Without this a worker only ever improves squares it happens
        /// to be standing on, which for a wandering settler is next to none of them.
        /// </summary>
        private UnitAction? MoveTowardWork(Unit unit, Tile currentTile)
        {
            var target = unit.Owner.Cities
                .Where(city => city.Location != null)
                .SelectMany(city => city.Location.CityRadius())
                .Where(tile => tile != null && tile != currentTile && tile.CityHere == null &&
                               tile.Type != TerrainType.Ocean && !tile.Terrain.Impassable &&
                               IsSafeForNonCombatUnit(unit, tile) &&
                               !tile.UnitsHere.Any(other => other.Owner == unit.Owner && other.Building != 0) &&
                               WorkFor(unit, tile, cleanOnly: false) != null)
                .OrderBy(tile => Utilities.DistanceTo(currentTile, tile))
                .FirstOrDefault();

            if (target == null)
            {
                return null;
            }

            var step = MovementFunctions.GetPossibleMoves(currentTile, unit)
                .Where(tile => IsSafeForNonCombatUnit(unit, tile))
                .Where(tile => tile.Type != TerrainType.Ocean && !tile.Terrain.Impassable)
                .OrderBy(tile => Utilities.DistanceTo(tile, target))
                .FirstOrDefault();

            if (step == null || Utilities.DistanceTo(step, target) >= Utilities.DistanceTo(currentTile, target))
            {
                return null;
            }

            return new MoveAction(unit, step, game);
        }

        private bool ShouldFoundCityNow(Unit unit, Tile tile)
        {
            if (unit.Owner.Cities.Count == 0)
            {
                return true;
            }

            if (tile.Fertility >= 6)
            {
                return true;
            }

            return !MovementFunctions.GetPossibleMoves(tile, unit)
                .Any(next => CitySiteScore(unit, next) > CitySiteScore(unit, tile) + 25);
        }

        private bool CanFoundUsefulCity(Unit unit, Tile tile)
        {
            if (tile.Type == TerrainType.Ocean || tile.Terrain.Impassable || tile.CityHere != null)
            {
                return false;
            }

            if (tile.UnitsHere.Any(other => other.Owner != unit.Owner))
            {
                return false;
            }

            return !tile.CityRadius().Any(other => other.CityHere != null && other.CityHere.Owner == unit.Owner);
        }

        private int CitySiteScore(Unit unit, Tile tile)
        {
            if (!CanFoundUsefulCity(unit, tile))
            {
                return int.MinValue / 2;
            }

            var score = (int)(tile.Fertility * 100);
            if (tile.River) score += 80;
            if (tile.Special >= 0) score += 90;
            if (tile.HasShield) score += 25;
            if (tile.Resource) score += 40;
            if (tile.HasGoodyHut) score -= 75;

            var friendlyCityDistance = unit.Owner.Cities.Count == 0
                ? 8d
                : unit.Owner.Cities.Min(city => Utilities.DistanceTo(tile, city.Location));
            score += (int)(Math.Min(friendlyCityDistance, 10d) * 8d);

            var enemyNearby = tile.Neighbours().Any(neighbour => IsEnemyTile(unit, neighbour));
            if (enemyNearby)
            {
                score -= 150;
            }

            return score;
        }

        private UnitAction? MoveTowardNearestEnemy(Unit unit, Tile currentTile)
        {
            var target = FindNearestEnemyTile(unit, currentTile);
            if (target == null)
            {
                return null;
            }

            var destination = MovementFunctions.GetPossibleMoves(currentTile, unit)
                .Where(tile => tile == target || !BlocksCombatUnit(unit, tile))
                .OrderBy(tile => Utilities.DistanceTo(tile, target))
                .ThenByDescending(tile => tile.CityHere != null && tile.CityHere.Owner != unit.Owner)
                .FirstOrDefault();

            return destination == null ? null : TileToAction(destination, unit);
        }

        private UnitAction? ExploreAction(Unit unit, Tile currentTile, bool avoidEnemies = false)
        {
            var moves = MovementFunctions.GetPossibleMoves(currentTile, unit)
                .Where(tile => unit.AttackBase > 0 || IsSafeForNonCombatUnit(unit, tile))
                .Where(tile => !avoidEnemies || !tile.Neighbours().Any(neighbour => IsEnemyTile(unit, neighbour)))
                .ToList();

            if (moves.Count == 0)
            {
                return new NothingAction(unit, game);
            }

            var destination = moves
                .OrderBy(tile => tile.Visibility.Length > unit.Owner.Id && tile.Visibility[unit.Owner.Id] ? 1 : 0)
                .ThenByDescending(tile => tile.Fertility)
                .ThenBy(_ => game.Random.Next(1000))
                .First();

            return TileToAction(destination, unit);
        }

        private Tile? FindNearestEnemyTile(Unit unit, Tile currentTile)
        {
            Tile? best = null;
            var bestDistance = double.MaxValue;
            foreach (var tile in currentTile.Map.Tile)
            {
                if (!IsEnemyTile(unit, tile))
                {
                    continue;
                }

                if (unit.Domain == UnitGas.Ground && tile.Type == TerrainType.Ocean)
                {
                    continue;
                }

                var distance = Utilities.DistanceTo(currentTile, tile);
                if (distance < bestDistance)
                {
                    best = tile;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static bool IsSafeForNonCombatUnit(Unit unit, Tile tile)
        {
            if (tile.UnitsHere.Any(other => other.Owner != unit.Owner))
            {
                return false;
            }

            if (tile.CityHere != null && tile.CityHere.Owner != unit.Owner)
            {
                return false;
            }

            return !tile.Neighbours().Any(neighbour => IsEnemyTile(unit, neighbour));
        }

        private static bool BlocksCombatUnit(Unit unit, Tile tile)
        {
            return tile.UnitsHere.Any(other => other.Owner == unit.Owner && other.InShip == null) &&
                   tile.CityHere?.Owner != unit.Owner;
        }

        private static bool IsEnemyTile(Unit unit, Tile tile)
        {
            if (tile.CityHere != null && tile.CityHere.Owner != unit.Owner)
            {
                return true;
            }

            return tile.UnitsHere.Any(other => !other.Dead && other.Owner != unit.Owner && other.InShip == null);
        }

        private void CallUnitsLost(LuaTable units, Unit? by)
        {
            var args = new LuaTable { { "Units", units } };
            if (by != null)
            {
                args.Add("By", new UnitApi(by, game));
            }

            Ai.Call(AiEvent.UnitsLost, args);
        }

        public void UnitLost(Unit unit, Unit? killedBy)
        {
            var units = new LuaTable { new UnitApi(unit, game) };
            CallUnitsLost(units, killedBy);
        }

        public void UnitsLost(List<Unit> deadUnits, Unit? killedBy)
        {
            var units = new LuaTable();
            foreach (var u in deadUnits)
            {
                units.Add(new UnitApi(u, game));
            }
            CallUnitsLost(units, killedBy);
        }

        public void UnitMoved(Unit unit, Tile tileTo, Tile tileFrom)
        {
            Ai.Call(AiEvent.UnitMoved,
                new LuaTable
                {
                    { "Unit", new UnitApi(unit, game) },
                    { "TileTo", new TileApi(tileTo, game) },
                    { "TileFrom", new TileApi(tileFrom, game) }
                });
        }

        public void CombatHappened(CombatEventArgs combatEventArgs)
        {
        }

        public void MoveBlocked(Unit unit, BlockedReason blockedReason)
        {
        }

        public void GoodyHutTriggered(Unit unit, GoodyHutOutcomeResult outcome)
        {
        }

        public void SelectTechFromConquest(List<Advance> techs)
        {
            int result;
            var luaResult = Ai.Call(AiEvent.SelectTechFromConquest,
                new LuaTable { { "Techs", techs.Select(t => new Tech(game.Rules.Advances, t.Index)).ToList() } });
            if (luaResult is { Count: > 0 })
            {
                result = luaResult[0] switch
                {
                    int index and >= 0 when index < techs.Count => techs[index].Index,
                    int index when index > techs.Count => index % game.Rules.Advances.Length,
                    Advance advance => advance.Index,
                    Tech tech => tech.id,
                    _ => ai.Random.ChooseFrom(techs).Index
                };
            }
            else
            {
                result = ai.Random.ChooseFrom(techs).Index;
            }
            game.GiveAdvance(result, Civilization);
        }

        public void CityLost(City city)
        {
            Ai.Call(AiEvent.CityLost, new LuaTable { { "city", new CityApi(city, game) } });
        }

        public void CityCaptured(City city)
        {
            Ai.Call(AiEvent.CityCaptured, new LuaTable { { "city", new CityApi(city, game) } });
        }

        /// <summary>
        /// The AI does not yet buy units or cities. It declines rather than spending
        /// its treasury on a decision nothing has been written to make well.
        /// </summary>
        public void DiplomatArrived(Unit diplomat, Tile target)
        {
        }

        /// <summary>
        /// A computer civilisation's caravan has arrived somewhere useful. It puts
        /// its cargo into a wonder if the city is building one -- that is what
        /// caravans are for, and it is how the computer players ever finish a great
        /// work -- and otherwise sells the goods and opens a route.
        /// </summary>
        public void CaravanArrived(Unit caravan, City city)
        {
            if (CaravanActions.CanHelpBuildWonder(caravan, city))
            {
                CaravanActions.HelpBuildWonder(game, caravan, city);
                return;
            }

            var home = CaravanActions.HomeCity(caravan);
            if (home != null && home != city)
            {
                CaravanActions.EstablishTradeRoute(game, caravan, city);
            }
        }

        private UnitAction? TileToAction(Tile tile, Unit unit)
        {
            if (tile == unit.CurrentLocation)
            {
                return new NothingAction(unit, game);
            }

            if (!unit.CurrentLocation.Neighbours().Contains(tile))
            {
                return new GotoAction(unit, tile, game);
            }

            if (tile.UnitsHere.Count > 0 && tile.UnitsHere[0].Owner.Id != unit.Owner.Id)
            {
                return new AttackAction(unit, tile, game);
            }
            return new MoveAction(unit, tile, game);
        }
    }
}
