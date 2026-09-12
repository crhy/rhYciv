using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Terrains;
using RhyCiv.Engine.Units;
using Model.Core;
using Model.Core.Units;
using Model.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using Model.Constants;
using Model.Core.GameRules;
using Model.Core.Mapping;

namespace RhyCiv.Engine.UnitActions
{
    public static class MovementFunctions
    {
        public static void TryMoveNorth(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }
            
            if (activeUnit!.Y - 2 >= 0)
            {
                MoveC2(instance, activeUnit, 0, -2);
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }

            CheckForUnitTurnEnded(instance, activeUnit);
        }

        public static void TryMoveNorthEast(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }

            var map = activeUnit!.CurrentLocation!.Map;
            if (activeUnit.Y - 1 >= 0 && (!map.Flat || activeUnit.X + 1 < map.XDimMax))
            {
                if (!map.Flat && activeUnit.X == map.XDimMax - 1)
                {
                    MoveC2(instance, activeUnit, -map.XDimMax + 2, -1);
                }
                else
                {
                    MoveC2(instance, activeUnit, 1, -1);
                }
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }

            CheckForUnitTurnEnded(instance, activeUnit);
        }

        /// <summary>
        /// Wakes anything sleeping next to a square something hostile has just
        /// stepped into.
        /// <para>
        /// A unit told to sleep stayed asleep whatever walked past it, so a stack
        /// left to hold a pass or a border could be walked round, or attacked, while
        /// it was never offered to the player at all. Civ II wakes a sleeping unit
        /// the moment an enemy comes within sight of it; coming alongside is the
        /// case that matters, because that is the square an attack comes from.
        /// </para>
        /// <para>
        /// Fortified units are deliberately left alone. Fortifying is a stance a
        /// unit is meant to hold, not a way of not paying attention.
        /// </para>
        /// </summary>
        private static void WakeSentriesNear(Unit mover, Tile arrivedAt)
        {
            if (mover.Dead)
            {
                return;
            }

            foreach (var tile in arrivedAt.Neighbours())
            {
                foreach (var watcher in tile.UnitsHere.ToList())
                {
                    if (watcher.Dead || watcher.Owner == mover.Owner ||
                        watcher.Order != (int)OrderType.Sleep)
                    {
                        continue;
                    }

                    watcher.Order = (int)OrderType.NoOrders;
                    watcher.WaitOrder = false;
                }
            }
        }

        public static bool ActiveUnitCannotMove(Unit? activeUnit)
        {
            return activeUnit == null || activeUnit.Dead || activeUnit.CurrentLocation == null || activeUnit.TurnEnded;
        }

        private static void CheckForUnitTurnEnded(IGame game, Unit activeUnit)
        {
            // TurnEnded, not just MovePoints: a unit can finish its turn by taking
            // an order that occupies it -- fortifying, or starting a road, mine or
            // irrigation -- while still holding movement it cannot use. Checking
            // only the points left those units selected and blinking.
            if (activeUnit.TurnEnded || activeUnit.Dead)
            {
                game.ChooseNextUnit();
            }
        }

        public static void TryMoveEast(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }

            var map = activeUnit!.CurrentLocation!.Map;
            if (activeUnit.X + 2 < map.XDimMax)
            {
                MoveC2(instance, activeUnit, 2, 0);
            }
            else if (!map.Flat)
            {
                MoveC2(instance, activeUnit, -map.XDimMax + 2, 0);
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }
            
            CheckForUnitTurnEnded(instance,activeUnit);
        }
        
        public static void TryMoveSouthEast(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }

            var map = activeUnit!.CurrentLocation!.Map;
            if (activeUnit.Y + 1 < map.YDim && (!map.Flat || activeUnit.X + 1 < map.XDimMax))
            {
                if (!map.Flat && activeUnit.X == map.XDimMax - 1)
                {
                    MoveC2(instance, activeUnit, -map.XDimMax + 2, 1);
                }
                else
                {
                    MoveC2(instance, activeUnit, 1, 1);
                }
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }

            CheckForUnitTurnEnded(instance,activeUnit);
        }
        
        public static void TryMoveSouth(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }

            var map = activeUnit!.CurrentLocation!.Map;
            if (activeUnit.Y + 2 < map.YDim)
            {
                MoveC2(instance, activeUnit, 0, 2);
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }

            CheckForUnitTurnEnded(instance,activeUnit);
        }

        public static void TryMoveSouthWest(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }
            
            var map = activeUnit!.CurrentLocation!.Map;
            if (activeUnit.Y + 1 < map.YDim && (!map.Flat || activeUnit.X > 0))
            {
                if (!map.Flat && activeUnit.X == 0)
                {
                    MoveC2(instance, activeUnit, map.XDimMax - 2, 1);
                }
                else
                {
                    MoveC2(instance, activeUnit, -1, 1);
                }
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }

            CheckForUnitTurnEnded(instance, activeUnit);
        }

        public static void TryMoveWest(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }

            var map = activeUnit!.CurrentLocation!.Map;
            if (activeUnit.X - 2 >= 0)
            {
                MoveC2(instance, activeUnit, -2, 0);
            }
            else if(!map.Flat)
            {
                MoveC2(instance, activeUnit, map.XDimMax-2, 0);
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }

            CheckForUnitTurnEnded(instance, activeUnit);
        }

        public static void TryMoveNorthWest(IGame instance)
        {
            var activeUnit = instance.ActivePlayer.ActiveUnit;
            if (ActiveUnitCannotMove(activeUnit))
            {
                //TODO: Handle error
                return;
            }

            var map = activeUnit!.CurrentLocation!.Map;
            if (activeUnit.Y - 1 >= 0 && (!map.Flat || (map.Flat && activeUnit.X > 0)))
            {
                if (!map.Flat && activeUnit.CurrentLocation.X == 0)
                {
                    MoveC2(instance, activeUnit, map.XDimMax - 1, -1);
                }
                else
                {
                    MoveC2(instance, activeUnit, -1, -1);
                }
            }
            else
            {
                instance.Players[activeUnit.Owner.Id].MoveBlocked(activeUnit, BlockedReason.EdgeOfMap);
                return;
            }

            CheckForUnitTurnEnded(instance, activeUnit);
        }

        public static void MoveC2(IGame game, Unit unit, int deltaX, int deltaY)
        {
            if (unit == null)
            {
                throw new NotSupportedException("No unit selected");
            }

            var destX = unit.X + deltaX;
            var destY = unit.Y + deltaY;

            var tileTo = unit.CurrentLocation.Map.TileC2(destX, destY);
            if (tileTo.UnitsHere.Count > 0 && tileTo.UnitsHere[0].Owner != unit.Owner)
            {
                if (!AttackAtTile(unit, game, tileTo)) return;
            }
            else if (HasNonCombatBusinessAt(unit, tileTo))
            {
                // A Diplomat or a Caravan has business in somebody else's city
                // whether or not anybody is standing in it. This choice was made on
                // the units present alone, so an *undefended* city was an ordinary
                // move: the Diplomat walked in and took it the way a warrior would,
                // and a city of size one is destroyed by that. Which is how a
                // Diplomat sent to buy an empty city of one ended with no city, no
                // price offered, and nothing said about it.
                if (!AttackAtTile(unit, game, tileTo)) return;
            }
            else
            {
                Moveto(game, unit, destX, destY);
            }

            if (unit.Domain == UnitGas.Air && unit.MovePoints == 0)
            {
                //TODO: Air unit out of fuel check
            }
        }

        /// <summary>
        /// Whether this unit has an errand on a square rather than a fight: a
        /// Diplomat or Spy at somebody else's city, or a Caravan at a foreign
        /// market. Both are offered through <see cref="AttackAtTile"/>, which is
        /// where the missions live.
        /// </summary>
        private static bool HasNonCombatBusinessAt(Unit unit, Tile tileTo)
        {
            if (DiplomatActions.HasTarget(unit, tileTo))
            {
                return true;
            }

            return CaravanActions.IsCaravan(unit) && tileTo.CityHere is { } market &&
                   market.Owner != unit.Owner;
        }

        internal static bool AttackAtTile(Unit unit, IGame game, Tile tileTo)
        {
            // A Diplomat reaching somebody else's unit or city is not a failed
            // attack, it is the whole reason the unit exists. Offer the player what
            // can be done here before refusing the move for having no attack
            // strength, which is all that used to happen.
            if (DiplomatActions.HasTarget(unit, tileTo))
            {
                game.Players[unit.Owner.Id].DiplomatArrived(unit, tileTo);
                return false;
            }

            // Nor is a Caravan reaching a foreign market a failed attack. It is the
            // long journey paying off, and it was being refused for having no
            // attack strength.
            if (CaravanActions.IsCaravan(unit) && tileTo.CityHere is { } market &&
                market.Owner != unit.Owner)
            {
                game.Players[unit.Owner.Id].CaravanArrived(unit, market);
                return false;
            }

            if (unit.AttackBase == 0)
            {
                game.Players[unit.Owner.Id].MoveBlocked(unit, BlockedReason.ZeroAttackStrength);
                return false;
            }

            // Only Marines storm a shore straight off a transport. Every other land
            // unit has to be put ashore first, which is the whole point of the
            // amphibious flag and of holding a beachhead.
            if (unit.InShip != null && unit.Domain == UnitGas.Ground &&
                !unit.CanMakeAmphibiousAssaults && tileTo.Type != TerrainType.Ocean)
            {
                game.Players[unit.Owner.Id].MoveBlocked(unit, BlockedReason.NotAmphibious);
                return false;
            }


            // Attacking is how a war starts, declared or not. Walking into somebody
            // you have a treaty with tears it up, with everything that follows from
            // that: their vendetta, and two black marks against your name that every
            // civilisation who has met you will hold against you for a long time.
            var target = tileTo.CityHere?.Owner ??
                         tileTo.UnitsHere.FirstOrDefault(other => !other.Dead && other.Owner != unit.Owner)?.Owner;
            if (target != null && target != unit.Owner &&
                !Diplomacy.DiplomacyFunctions.AtWar(unit.Owner, target))
            {
                Diplomacy.DiplomacyFunctions.DeclareWar(game, unit.Owner, target);
                game.Players[target.Id].WarDeclared(unit.Owner);
            }

            if (tileTo.CityHere != null)
            {
                // Empty enemy cities are captured by moving into them.  The barbarian AI can
                // target cities directly, so do not enter the combat resolver unless there is
                // actually an enemy defender on the city tile.
                if (!tileTo.UnitsHere.Any(u => !u.Dead && u.Owner != unit.Owner))
                {
                    Moveto(game, unit, tileTo.X, tileTo.Y);
                    return true;
                }

                // Anything can attack or defend a city when a defender is present.
                Attack(game, unit, tileTo);
                return true;
            }

            if (tileTo.Type == TerrainType.Ocean)
            {
                if (unit.Domain == UnitGas.Ground)
                {
                    // Ground units cannot attack into the sea
                    return false;
                }
            }
            else
            {
                if (unit.SubmarineAdvantagesDisadvantages)
                {
                    //Submarines can't attack land
                    return false;
                }
            }

            if (tileTo.UnitsHere.Count == 0)
            {
#if DEBUG
                Console.WriteLine("No units on tile for attack");
#endif
                return false;
            }

            if (!unit.CanAttackAirUnits && tileTo.UnitsHere.Any(u => u.Domain == UnitGas.Air))
            {
                game.Players[unit.Owner.Id].MoveBlocked(unit, BlockedReason.CannotAttackAirUnits);
                return false;
            }

            Attack(game, unit, tileTo);
            return true;
        }

        private static void Attack(IGame game, Unit attacker, Tile tile)
        {
            // A nuclear missile does not fight; it arrives.
            if (Ai.AiPersonality.IsNuclearMissile(attacker.TypeDefinition))
            {
                NuclearStrike(game, attacker, tile);
                return;
            }


            // Primary defender is the enemy unit with the largest defense factor.  An
            // undefended city can be reached here when an AI routine calls AttackAtTile
            // directly rather than going through MoveC2, so guard against an empty defender
            // stack and capture the city by movement instead of indexing UnitsHere[0].
            var defenders = tile.UnitsHere.Where(u => !u.Dead && u.Owner != attacker.Owner).ToList();
            if (defenders.Count == 0)
            {
                if (tile.CityHere != null && tile.CityHere.Owner != attacker.Owner)
                {
                    Moveto(game, attacker, tile.X, tile.Y);
                }

                return;
            }

            var groundDefenceFactor = tile.EffectsList.Where(e => e.Target == ImprovementConstants.GroundDefence).Sum(e=>e.Value);
            var defender = defenders[0];
            var defenseFactor = defender.DefenseFactor(attacker, tile, groundDefenceFactor);
            for (var i = 1; i < defenders.Count; i++)
            {
                var altDefenseFactor = defenders[i].DefenseFactor(attacker, tile, groundDefenceFactor);
                if (altDefenseFactor > defenseFactor)
                {
                    defender = defenders[i];
                    defenseFactor = altDefenseFactor;
                }
            }

            // Calculate odds of attacker winning combat (a round of battle)
            // The barbarians hit as hard as the difficulty says they do: a quarter
            // strength on Chieftain, half as hard again on Deity. This is most of
            // what makes the early game on the high levels frightening.
            var attackFactor = DifficultyRules.AttackStrength(game, attacker.Owner,
                attacker.AttackFactor(defender));
            if (attacker.MovePoints < game.Rules.Cosmic.MovementMultiplier)
            {
                //if attacker has less than one move point left attack at reduced strength
                attackFactor = attackFactor * attacker.MovePoints / game.Rules.Cosmic.MovementMultiplier;
            }

            //Calculate the firepower of the attacker and defender
            var fpA = attacker.FirepowerBase;
            var fpD = defender.FirepowerBase;

            if (attacker.Domain == UnitGas.Sea && defender.Domain == UnitGas.Ground)
            {
                // When a sea unit attacks a land unit, both units have their firepower reduced to 1
                fpA = 1;
                fpD = 1;
            }else if (attacker.Domain != UnitGas.Sea && defender.Domain == UnitGas.Sea &&
                      tile.Type != TerrainType.Ocean)
            {
                // Caught in port (A sea unit’s firepower is reduced to 1 when it is caught in port (or on a land square) by a land or air unit; The attacking air or land unit’s firepower is doubled)
                fpA *= 2;
                fpD = 1;
            }else if (attacker.CanAttackAirUnits && defender.Domain == UnitGas.Air && defender.FuelRange == 0)
            {
                // Helicopters attacked by fighters have firepower reduced to 1.
                // This matches the x0.5 defence adjustment in DefenseFactor, which
                // is also keyed on the attacker being able to engage air units.
                fpD = 1;
            }
            
            // Civ II rolls rand(A + D) each round, so the attacker takes the round
            // with probability exactly A / (A + D). The curve this replaced was
            // symmetric about even odds but over-rewarded the stronger unit
            // everywhere else -- A2 against D1 paid 0.72 instead of 0.67.
            var oddsTotal = attackFactor + defenseFactor;
            var probAttackerWins = oddsTotal <= 0 ? 0.5 : (double)attackFactor / oddsTotal;

            // Battle -> Loop through combat rounds until a unit loses its HP.
            //
            // The game's own generator, not a fresh System.Random. Everything else
            // in the engine draws from the seeded one, so combat was the single part
            // of a game that could not be replayed from its seed -- which is exactly
            // the part players report and nobody can reproduce.
            var random = game.Random;
            var combatRoundsAttackerWins = new List<bool>();  // Register combat outcomes
            var attackerHitpoints = new List<int>();  // Register attacker hitpoints in each round
            var defenderHitpoints = new List<int>();  // Register defender hitpoints in each round
            do
            {
                var rand = random.Next(0, 1000) / 1000.0;
                var attackerWinsRound = probAttackerWins > rand;
                attackerHitpoints.Add(attacker.RemainingHitpoints);
                defenderHitpoints.Add(defender.RemainingHitpoints);
                if (attackerWinsRound)
                {
                    defender.HitPointsLost += fpA;
                    combatRoundsAttackerWins.Add(true);
                }
                else 
                { 
                    attacker.HitPointsLost += fpD;
                    combatRoundsAttackerWins.Add(false);
                }
            } while (attacker.RemainingHitpoints > 0 && defender.RemainingHitpoints > 0);

            var attackerWinsBattle = defender.RemainingHitpoints <= 0;


            var combatEventArgs = new CombatEventArgs(UnitEventType.Attack, attacker, defender, combatRoundsAttackerWins, attackerHitpoints, defenderHitpoints);

            for (int civId = 0; civId < tile.Visibility.Length; civId++)
            {
                if (tile.Visibility[civId] && tile.Map.IsCurrentlyVisible(tile, civId))
                {
                    var player = game.Players[civId];
                    player.CombatHappened(combatEventArgs);
                }
            }
            
            if (attackerWinsBattle)
            {
                ApplyPostCombatMovementCost(game, attacker);
                // Defender loses - kill all units on the tile (except if on city & if in fortress/airbase)
                if (tile.CityHere != null ||
                    tile.EffectsList.Any(e => e.Target == ImprovementConstants.NoStackElimination))
                {
                    game.Players[defender.Owner.Id].UnitLost(defender, attacker);
                    defender.Dead = true;
                    if (tile.CityHere != null &&
                        !tile.CityHere.Improvements.Any(i => i.Effects.ContainsKey(Effects.Walled)))
                    {
                        tile.CityHere.ShrinkCity(game);
                        game.UpdateTiles([tile]);
                    }
                    //_casualties.Add(defender);
                    //_units.Remove(defender);
                }
                else
                {
                    var deadUnits = tile.UnitsHere.ToList();
                    tile.UnitsHere.Clear();
                    foreach (var unit in deadUnits)
                    {
                        unit.Dead = true;
                        //_casualties.Add(unit);
                        //_units.Remove(unit);
                    }
                    game.Players[defender.Owner.Id].UnitsLost(deadUnits, attacker);
                }
            }
            else
            {
                
                attacker.Dead = true;
                game.Players[attacker.Owner.Id].UnitLost(attacker, defender);
                //_casualties.Add(attacker);
                //_units.Remove(attacker);
            }

            // Missiles are expended by their own attack, whatever the outcome.
            if (attacker.DestroyedAfterAttacking && !attacker.Dead)
            {
                attacker.Dead = true;
                game.Players[attacker.Owner.Id].UnitLost(attacker, defender);
            }

            // Civ II promotes a surviving combatant to veteran half the time.
            var survivor = attackerWinsBattle ? attacker : defender;
            if (!survivor.Dead && !survivor.Veteran && random.Next(0, 2) == 0)
            {
                survivor.Veteran = true;
            }

            var updatedTiles = new List<Tile> { tile };
            if (attacker.CurrentLocation != null)
            {
                updatedTiles.Add(attacker.CurrentLocation);
            }

            game.UpdateTiles(updatedTiles.Distinct().ToList());

        }

        private static void ApplyPostCombatMovementCost(IGame game, Unit attacker)
        {
            var attackCost = game.Rules.Cosmic.MovementMultiplier;
            var movementLost = Math.Min(attacker.MaxMovePoints, attacker.MovePointsLost + attackCost);

            if (attacker.Domain != UnitGas.Air && attacker.HitpointsBase > 0 && attacker.HitPointsLost > 0)
            {
                var remainingHitpointRatio = Math.Max(0d, attacker.RemainingHitpoints) / attacker.HitpointsBase;
                var damageLimitedMovement = (int)Math.Round(attacker.MaxMovePoints * remainingHitpointRatio);
                var minimumMovement = attacker.Domain == UnitGas.Sea
                    ? Math.Min(attacker.MaxMovePoints, game.Rules.Cosmic.MovementMultiplier * 2)
                    : Math.Min(attacker.MaxMovePoints, game.Rules.Cosmic.MovementMultiplier);

                damageLimitedMovement = Math.Max(minimumMovement, damageLimitedMovement);
                movementLost = Math.Max(movementLost, attacker.MaxMovePoints - damageLimitedMovement);
            }

            attacker.MovePointsLost = Math.Clamp(movementLost, 0, attacker.MaxMovePoints);
        }

        private static void Moveto(IGame game, Unit unit, int destX, int destY)
        {
            var map = unit.CurrentLocation.Map;
            var tileFrom =  map.TileC2(unit.X, unit.Y);
            var tileTo = map.TileC2(destX, destY);
            if (!unit.IgnoreZonesOfControl && !IsFriendlyTile(tileTo, unit.Owner) && IsNextToEnemy(tileFrom, unit.Owner, unit.Domain) && IsNextToEnemy(tileTo, unit.Owner, unit.Domain))
            {
                game.Players[unit.Owner.Id].MoveBlocked(unit, BlockedReason.Zoc);
                return;
            }

            ExecuteUnitMove(game, unit, tileTo, tileFrom);
        }

        internal static void ExecuteUnitMove(IGame game, Unit unit, Tile tileTo, Tile tileFrom)
        {
            var isCity = tileTo.IsCityPresent;
            var unitMoved = isCity;
            var cosmicRules = game.Rules.Cosmic;
            var moveCost = cosmicRules.MovementMultiplier;
            switch (unit.Domain)
            {
                case UnitGas.Ground:
                {
                    if (tileTo.Type == TerrainType.Ocean)
                    {
                        //Check if we can board a ship there
                        var availableShip = tileTo.UnitsHere.FirstOrDefault(u =>
                            u.Owner == unit.Owner && u.ShipHold > u.CarriedUnits.Count);
                        if (availableShip != null)
                        {
                            availableShip.CarriedUnits.Add(unit);
                            moveCost = unit.MovePoints;
                            unit.InShip = availableShip;
                            unitMoved = true;
                            unit.Order = (int)OrderType.Sleep;
                        }

                        break;
                    }
                    
                    moveCost = GroundMoveCost(unit, tileTo, tileFrom, cosmicRules);
                    unitMoved = true;
                    break;
                }
                case UnitGas.Sea:
                {
                    if (tileTo.Type != TerrainType.Ocean)
                    {
                        if (!isCity && unit.CarriedUnits.Count > 0)
                        {
                            //Make landfall. We must capture the list since we want to modify it while we loop over it
                            var units = unit.CarriedUnits.ToList();
                            foreach (var u in units)
                            {
                                u.Order = (int)OrderType.NoOrders;
                                ExecuteUnitMove(game, u, tileTo, tileFrom);
                                u.InShip = null;
                            }

                            unit.CarriedUnits.Clear();
                            // It's okay to exit early here since the unit moved for the carried units will trigger the appropriate actions
                            return; 
                        }

                        break;
                    }

                    if (unit.ShipHold > 0 && unit.CarriedUnits.Count < unit.ShipHold)
                    {
                        if (tileFrom.Terrain.Type == TerrainType.Ocean)
                        {
                            foreach (var unaccountedUnit in tileFrom.UnitsHere
                                .Where(u => u.InShip == null &&
                                            u.Domain == UnitGas.Ground)
                                .Take(unit.ShipHold - unit.CarriedUnits.Count))
                            {
                                unaccountedUnit.InShip = unit;
                                unaccountedUnit.Order = (int)OrderType.Sleep;
                                unit.CarriedUnits.Add(unaccountedUnit);
                            }
                        }
                        else if (tileFrom.IsCityPresent)
                        {
                            foreach (var unaccountedUnit in tileFrom.UnitsHere
                                .Where(u => u.InShip == null &&
                                            u.Domain == UnitGas.Ground && u.Order == (int)OrderType.Sleep)
                                .Take(unit.ShipHold - unit.CarriedUnits.Count))
                            {
                                unaccountedUnit.InShip = unit;
                                unit.CarriedUnits.Add(unaccountedUnit);
                            }
                        }
                    }

                    unitMoved = true;
                    break;
                }
                case UnitGas.Air:
                {
                    if (unit.InShip != null)
                    {
                        unit.InShip.CarriedUnits.Remove(unit);
                        unit.InShip = null;
                    }

                    if (tileTo.EffectsList.Any(e=>e.Target == ImprovementConstants.Airbase) || tileTo.IsCityPresent)
                    {
                        moveCost = unit.MovePoints;
                    }
                    else
                    {
                        var carrier = tileTo.UnitsHere.FirstOrDefault(u =>
                            u.CanCarryAirUnits && u.CarriedUnits.Count < 20);
                        if (carrier != null)
                        {
                            moveCost = unit.MovePoints;
                            carrier.CarriedUnits.Add(unit);
                            unit.InShip = carrier;
                        }
                    }

                    unitMoved = true;
                    break;
                }
                case UnitGas.Special:
                    unitMoved = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // If unit moved, update its X-Y coords
            if (unitMoved)
            {
                // A unit may always complete a move it has any movement left for,
                // even onto ground that costs more than it has; it simply ends its
                // turn there. Capping the loss keeps the remaining points from
                // going negative, which the interface reads as points still owed.
                unit.MovePointsLost = Math.Min(unit.MaxMovePoints, unit.MovePointsLost + moveCost);
                // Set previous coords
                unit.PrevXy = [unit.X, unit.Y];

                // Set new coords
                unit.X = tileTo.X;
                unit.Y = tileTo.Y;
                unit.CurrentLocation = tileTo;
                if (unit.CarriedUnits.Count > 0)
                {
                    unit.CarriedUnits.ForEach(u =>
                    {
                        u.PrevXy = unit.PrevXy;
                        u.X = unit.X;
                        u.Y = unit.Y;
                        u.CurrentLocation = tileTo;
                    });
                    if (isCity) //If we're docking activate units carried
                    {
                        unit.CarriedUnits.ForEach(u =>
                        {
                            u.Order = (int)OrderType.NoOrders;
                            u.InShip = null;
                        });
                        unit.CarriedUnits.Clear();
                    }
                }
                else if (unit.InShip != null)
                {
                    unit.InShip.CarriedUnits.Remove(unit);
                    unit.InShip = null;
                }

                if (unit.Order != (int)OrderType.GoTo)
                {
                    unit.Order = (int)OrderType.NoOrders;
                }
                
                // Only tell a player about a move they can actually watch. The test
                // used to be Visibility alone, which records that a civilisation has
                // *explored* a square, not that it can see it now. So every enemy
                // step through territory you had once walked was animated on your
                // map: the view scrolled away to a dark corner of the world to
                // follow a unit you were not entitled to see, which is what made the
                // map jump into the fog on ending a turn.
                for (var civId = 0; civId < unit.CurrentLocation.Visibility.Length; civId++)
                {
                    if (unit.CurrentLocation.Visibility[civId] &&
                        (tileTo.Map.IsCurrentlyVisible(tileTo, civId) ||
                         tileFrom.Map.IsCurrentlyVisible(tileFrom, civId)))
                    {
                        game.Players[civId].UnitMoved(unit, tileTo, tileFrom);
                    }
                }
                
                game.ActivePlayer.ActiveTile = tileTo;
                WakeSentriesNear(unit, tileTo);
                var mapUpdates = new List<Tile>();
                foreach (var neighbourTile in tileTo.Neighbours(unit.TwoSpaceVisibility))
                {
                    if (!neighbourTile.IsVisible(unit.Owner.Id))
                    {
                        neighbourTile.SetVisible(unit.Owner.Id);
                        mapUpdates.Add(neighbourTile);
                    }
                    else if (neighbourTile.KnowledgeIsStale(unit.Owner.Id))
                    {
                        // Explored ground can still hold a surprise: a city founded,
                        // taken or grown while nobody was watching. Only squares
                        // whose record actually disagrees with what is there are
                        // reported, because reporting every square in sight on every
                        // step would have the interface recomposing a screenful of
                        // terrain for each unit that moves.
                        mapUpdates.Add(neighbourTile);
                    }
                }

                if (tileTo.KnowledgeIsStale(unit.Owner.Id))
                {
                    mapUpdates.Add(tileTo);
                }

                MeetTheNeighbours(game, unit, tileTo);
                
                if (tileTo.CityHere is { } ownCity && ownCity.Owner.Id == unit.Owner.Id &&
                    CaravanActions.IsCaravan(unit))
                {
                    // A Caravan that has reached a city of its own civilisation can
                    // put its cargo into a wonder, or open a route to the city it
                    // set out from. It used to arrive and stand there.
                    game.Players[unit.Owner.Id].CaravanArrived(unit, ownCity);
                }

                if(tileTo.CityHere != null && tileTo.CityHere.Owner.Id != unit.Owner.Id)
                {
                    var loser = tileTo.CityHere.Owner;
                    tileTo.CityHere.ShrinkCity(game);
                    if (tileTo.CityHere != null)
                    {
                        tileTo.CityHere.EliminateCityUnits(game);
                        tileTo.CityHere.Owner.Cities.Remove(tileTo.CityHere);
                        tileTo.CityHere.Owner = unit.Owner;
                        unit.Owner.Cities.Add(tileTo.CityHere);
                        
                        game.Players[loser.Id].CityLost(tileTo.CityHere);

                        // And their map. They cannot see the square any more --
                        // losing the city is exactly what took it out of their
                        // sight -- so nothing else will ever correct it.
                        game.UpdateTilesFor([tileTo], loser.Id);

                        // The capture is reported before its spoils. It used to be
                        // the other way round, which put "you have learned Writing"
                        // in front of "the gates of Ulundi are open" and left the
                        // discovery with nothing to attribute itself to.
                        game.Players[unit.Owner.Id].CityCaptured(tileTo.CityHere);

                        // Wonders stand where they were built. Taking the city takes
                        // them, which is worth being told about: a captured Colossus
                        // is worth more than the city around it.
                        foreach (var wonder in tileTo.CityHere.Improvements.Where(i => i.IsWonder))
                        {
                            game.Players[unit.Owner.Id].WonderCaptured(tileTo.CityHere, wonder);
                        }

                        if (!game.ScenarioData.ForbidTechFromConquests)
                        {
                            var techs = AdvanceFunctions.CalculateResearchTheft(game, unit.Owner, loser);
                            if (techs.Count > 0)
                            {
                                game.Players[unit.Owner.Id].SelectTechFromConquest(techs);
                            }
                        }
                        
                    }
                    mapUpdates.Add(tileTo);
                }else if (tileTo.HasGoodyHut)
                {
                    var eligibleAdvances = AdvanceFunctions.CalculateAvailableResearch(game, unit.Owner)
                        .Select(advance => advance.Index)
                        .ToArray();
                    var outcome = tileTo.ConsumeGoodyHut(unit, eligibleAdvances,
                        NearSettlement(game, unit, tileTo));
                    if (outcome.AdvanceIndex is { } advanceIndex)
                    {
                        game.GiveAdvance(advanceIndex, unit.Owner);
                    }
                    if (outcome.OutcomeType == "AdvancedTribe")
                    {
                        ApplyAdvancedTribeOutcome(game, unit, tileTo, outcome, mapUpdates);
                    }
                    else if (outcome.OutcomeType == "Nomads")
                    {
                        ApplyNomadsOutcome(game, outcome);
                    }
                    else if (outcome.OutcomeType == "Barbarians")
                    {
                        ApplyBarbarianOutcome(game, unit, tileTo, outcome, mapUpdates);
                    }
                    else if (outcome.OutcomeType == "Mercenaries")
                    {
                        ApplyMercenariesOutcome(game, outcome);
                    }

                    game.Players[unit.Owner.Id].GoodyHutTriggered(unit, outcome);

                    mapUpdates.Add(tileTo);
                }

                if (mapUpdates.Count > 0)
                {
                    game.UpdateTiles(mapUpdates);
                }
            }
        }

        /// <summary>
        /// A nuclear strike, which is not combat and has no result to roll for.
        /// <para>
        /// Civ II's rules: everything standing on the target square dies, whichever
        /// side it belongs to, a city there loses half its people, and the ground
        /// around it is left poisoned. The missile is spent. Everyone who can see
        /// it happen is told, because a mushroom cloud is not a private matter.
        /// </para>
        /// </summary>
        private static void NuclearStrike(IGame game, Unit missile, Tile target)
        {
            var casualties = target.UnitsHere.Where(unit => !unit.Dead).ToList();
            foreach (var owner in casualties.Select(unit => unit.Owner).Distinct().ToList())
            {
                var lost = casualties.Where(unit => unit.Owner == owner).ToList();
                lost.ForEach(unit => unit.Dead = true);
                game.Players[owner.Id].UnitsLost(lost, missile);
            }

            var city = target.CityHere;
            if (city != null)
            {
                for (var half = city.Size / 2; half > 0 && city.Size > 1; half--)
                {
                    city.ShrinkCity(game);
                }

                game.Players[city.Owner.Id].CityDecrease(city);
            }

            // Fallout. The squares around the blast are left as polluted as any
            // century of industry could manage.
            var poisoned = new List<Tile> { target };
            poisoned.AddRange(target.Neighbours());
            foreach (var square in poisoned)
            {
                PollutionFunctions.Poison(game, square);
            }

            game.UpdateTiles(poisoned);

            missile.Dead = true;
            missile.Owner.Units.Remove(missile);
            game.Players[missile.Owner.Id].UnitLost(missile, null);

            foreach (var civ in game.AllCivilizations.Where(c => c.Alive))
            {
                if (target.IsVisible(civ.Id))
                {
                    game.Players[civ.Id].NuclearStrike(target, missile.Owner);
                }
            }
        }

        /// <summary>
        /// Anybody the unit can now see, and who can see it, has met this
        /// civilisation.
        /// <para>
        /// Civilisations used to walk into each other with nothing happening at
        /// all: no herald, no record that they had met, and so nothing that could
        /// later be talked about. Contact is what everything else in diplomacy
        /// rests on.
        /// </para>
        /// </summary>
        private static void MeetTheNeighbours(IGame game, Unit unit, Tile tileTo)
        {
            if (unit.Owner.PlayerType == PlayerType.Barbarians)
            {
                // The barbarians are nobody's neighbours. They are not a
                // civilisation you can talk to, and meeting them is not an event.
                return;
            }

            foreach (var neighbour in tileTo.Neighbours().Append(tileTo))
            {
                var strangers = neighbour.UnitsHere
                    .Where(other => !other.Dead)
                    .Select(other => other.Owner)
                    .Append(neighbour.CityHere?.Owner)
                    .OfType<Civilization>()
                    .Where(civ => civ != unit.Owner && civ.PlayerType != PlayerType.Barbarians)
                    .Distinct();

                foreach (var stranger in strangers)
                {
                    Diplomacy.DiplomacyFunctions.MakeContact(game, unit.Owner, stranger);
                }
            }
        }

        /// <summary>
        /// How near a hut counts as being within somebody's reach, in squares.
        /// </summary>
        private const double NearCityDistance = 4.0;

        /// <summary>
        /// The turn after which a civilisation with no cities is no longer given
        /// the beginner's protection.
        /// </summary>
        private const int NoCitiesRuleLastTurn = 50;

        /// <summary>
        /// Whether a hut should withhold a wandering tribe and a barbarian horde.
        /// <para>
        /// Civ II suppresses both when the finder has founded nothing yet and it is
        /// still early -- there is nowhere for a tribe to join and no chance against
        /// a horde -- and when the hut is within four squares of a city, where a
        /// tribe would have nowhere to settle. Their share goes to mercenaries.
        /// </para>
        /// </summary>
        private static bool NearSettlement(IGame game, Unit unit, Tile hut)
        {
            if (unit.Owner.Cities.Count == 0 && game.TurnNumber < NoCitiesRuleLastTurn)
            {
                return true;
            }

            return game.AllCities.Any(city => city.Location != null &&
                                              Utilities.DistanceTo(city.Location, hut) < NearCityDistance);
        }

        private static void ApplyAdvancedTribeOutcome(IGame game, Unit unit, Tile tile,
            Model.Core.GoodyHuts.Outcomes.GoodyHutOutcomeResult outcome, IList<Tile> mapUpdates)
        {
            var cityNearby = tile.CityHere != null || tile.Neighbours().Any(t => t.IsCityPresent);
            if (tile.Type != TerrainType.Ocean && !tile.Terrain.Impassable && !cityNearby)
            {
                tile.HasGoodieHut = false;
                var cityName = CityActions.GetCityName(unit.Owner, game);
                var city = CityActions.BuildCity(unit, game, cityName);

                // A hut-created advanced tribe founds a city, but the unit that opened
                // the hut survives and stands inside the new city. BuildCity normally
                // consumes a settler, so restore the triggering unit explicitly.
                unit.Dead = false;
                unit.X = tile.X;
                unit.Y = tile.Y;
                unit.MapIndex = tile.Z;
                unit.MovePointsLost = unit.MaxMovePoints;
                if (!tile.UnitsHere.Contains(unit))
                {
                    tile.UnitsHere.Add(unit);
                }

                tile.SetVisible(unit.Owner.Id);
                mapUpdates.Add(tile);
                foreach (var neighbour in tile.Neighbours())
                {
                    if (neighbour.Visibility.Length > unit.Owner.Id)
                    {
                        neighbour.SetVisible(unit.Owner.Id);
                    }
                    mapUpdates.Add(neighbour);
                }

                outcome.Message = $"The villagers found the city of {city.Name} and join your civilization.";
                return;
            }

            tile.HasGoodieHut = false;
            unit.Owner.Money += 25;
            outcome.Message = "The villagers cannot found a city here, but they welcome your people with gifts worth 25 gold.";
            mapUpdates.Add(tile);
        }


        private static void ApplyBarbarianOutcome(IGame game, Unit triggeringUnit, Tile hutTile,
            Model.Core.GoodyHuts.Outcomes.GoodyHutOutcomeResult outcome, IList<Tile> mapUpdates)
        {
            var barbarianCiv = game.AllCivilizations.FirstOrDefault(c => c.PlayerType == PlayerType.Barbarians)
                               ?? game.AllCivilizations.FirstOrDefault(c => c.Id == 0);
            if (barbarianCiv != null)
            {
                barbarianCiv.Alive = true;
            }

            if (barbarianCiv == null)
            {
                outcome.Message = "The village is deserted, but ominous tracks lead away from it.";
                return;
            }

            var barbarianUnitDefinition = Barbarians.UnitFor(game, triggeringUnit.Owner);
            if (barbarianUnitDefinition == null)
            {
                outcome.Message = "The village is deserted, but ominous tracks lead away from it.";
                return;
            }

            var spawnTiles = hutTile.Neighbours()
                .Where(tile => tile.Type != TerrainType.Ocean)
                .Where(tile => !tile.Terrain.Impassable)
                .Where(tile => tile.CityHere == null)
                .Where(tile => tile.UnitsHere.All(u => u.Owner == barbarianCiv))
                .OrderBy(tile => Math.Abs(tile.X - triggeringUnit.X) + Math.Abs(tile.Y - triggeringUnit.Y))
                .Take(Math.Max(1, Math.Min(3, game.BarbarianActivity + 1)))
                .ToList();

            if (spawnTiles.Count == 0)
            {
                var fallbackTile = hutTile.Neighbours()
                    .FirstOrDefault(tile => tile.Type != TerrainType.Ocean && !tile.Terrain.Impassable && tile.CityHere == null);
                if (fallbackTile != null)
                {
                    spawnTiles.Add(fallbackTile);
                }
            }

            var created = 0;
            foreach (var spawnTile in spawnTiles)
            {
                var barbarian = Barbarians.Create(barbarianCiv, barbarianUnitDefinition, spawnTile,
                    veteran: DifficultyRules.BarbariansAreVeterans(game));
                barbarian.MovePointsLost = barbarian.MaxMovePoints;
                spawnTile.SetVisible(triggeringUnit.Owner.Id);
                created++;
                if (!mapUpdates.Contains(spawnTile))
                {
                    mapUpdates.Add(spawnTile);
                }
            }

            outcome.Message = created == 0
                ? "A barbarian horde was near, but could not reach this village."
                : created == 1
                    ? "A barbarian warrior appears near the village!"
                    : "A barbarian horde appears near the village!";
        }

        /// <summary>
        /// Gives the mercenaries a soldier's uniform.
        /// <para>
        /// The outcome itself has no access to the ruleset, so it can only copy the
        /// type of whatever unit walked into the village. That made the "skilled
        /// mercenaries" a second copy of the explorer that found them, or -- worse
        /// -- a free Settlers unit for any settler that stumbled on a hut. They
        /// should be soldiers, so the best fighter the finder's civilisation could
        /// field is substituted here, where the rules are in reach.
        /// </para>
        /// </summary>
        private static void ApplyMercenariesOutcome(IGame game,
            Model.Core.GoodyHuts.Outcomes.GoodyHutOutcomeResult outcome)
        {
            if (outcome.CreatedUnit == null)
            {
                return;
            }

            var owner = outcome.CreatedUnit.Owner;
            var soldier = game.Rules.UnitTypes
                .Where(definition => definition.Domain == UnitGas.Ground)
                .Where(definition => definition.Attack > 0 && !definition.IsSettler)
                .Where(definition => AdvanceFunctions.HasTech(owner, definition.Prereq))
                .MaxBy(definition => definition.Attack * 2 + definition.Defense);

            if (soldier == null)
            {
                return;
            }

            outcome.CreatedUnit.TypeDefinition = soldier;
            outcome.CreatedUnit.MovePointsLost = 0;
            outcome.Message =
                $"You have discovered a friendly tribe of skilled mercenaries. They join you as {soldier.Name}.";
        }

        private static void ApplyNomadsOutcome(IGame game, Model.Core.GoodyHuts.Outcomes.GoodyHutOutcomeResult outcome)
        {
            if (outcome.CreatedUnit == null)
            {
                return;
            }

            var settlerDefinition = game.Rules.UnitTypes.FirstOrDefault(u => u.IsSettler)
                                    ?? game.Rules.UnitTypes.FirstOrDefault(u => u.AIrole == AiRoleType.Settle)
                                    ?? game.Rules.UnitTypes.FirstOrDefault(u =>
                                        string.Equals(u.Name, "Settlers", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(u.Name, "Settler", StringComparison.OrdinalIgnoreCase));
            if (settlerDefinition == null)
            {
                return;
            }

            outcome.CreatedUnit.TypeDefinition = settlerDefinition;
            outcome.CreatedUnit.MovePointsLost = 0;
            outcome.Message = "You discover a band of wandering nomads. They agree to join your tribe as Settlers.";
        }

        /// <summary>
        /// What one step of ground movement costs, in movement fragments.
        /// <para>
        /// There used to be a rule here that a unit whose whole allowance was a
        /// single movement point spent all of it on any move costing less than a
        /// full point. A road costs a third of a point, so that meant a road was
        /// worth nothing at all to a Settlers, Warriors, Phalanx or Musketeers:
        /// every one-move unit in the game walked its own roads at one square a
        /// turn. Roads are for everybody.
        /// </para>
        /// </summary>
        internal static int GroundMoveCost(Unit unit, Tile tileTo, Tile tileFrom, CosmicRules cosmicRules)
        {
            var moveCost = cosmicRules.MovementMultiplier * tileTo.MoveCost;
            moveCost = MoveCost(tileTo, tileFrom, moveCost, cosmicRules);

            // Alpine movement, where it is cheaper than the ground being crossed.
            if (cosmicRules.AlpineMovement < moveCost && unit.Alpine)
            {
                moveCost = cosmicRules.AlpineMovement;
            }

            return moveCost;
        }

        internal static int MoveCost(Tile tileTo, Tile tileFrom, int moveCost, CosmicRules cosmicRules)
        {
            foreach (var movementEffect in tileFrom.EffectsList.Where(e =>
                         e.Target == ImprovementConstants.Movement)
                    )
            {
                var matchingEffect = tileTo.EffectsList.Where(e =>
                    e.Source == movementEffect.Source && e.Target == ImprovementConstants.Movement).MinBy(i=>i.Value);
                if (matchingEffect == null) continue;

                if (matchingEffect.Level < movementEffect.Level)
                {
                    if (matchingEffect.Value < moveCost)
                    {
                        moveCost = matchingEffect.Value;
                    }
                }
                else
                {
                    if (movementEffect.Value < moveCost)
                    {
                        moveCost = movementEffect.Value;
                    }
                }
            }

            if (cosmicRules.RiverMovement < moveCost && tileFrom.River && tileTo.River &&
                Math.Abs(tileTo.X - tileFrom.X) == 1 &&
                Math.Abs(tileTo.Y - tileFrom.Y) == 1) //For rivers only for diagonal movement
            {
                moveCost = cosmicRules.RiverMovement;
            }

            return moveCost;
        }

        internal static bool IsFriendlyTile(Tile tileTo, Civilization unitOwner)
        {
            return tileTo.UnitsHere.Any(u => u.Owner == unitOwner) ||
                   (tileTo.CityHere != null && tileTo.CityHere.Owner == unitOwner);
        }

        internal static bool IsNextToEnemy(Tile tile, Civilization civ, UnitGas domain)
        {
            return tile.Neighbours().Any(t =>
                t.UnitsHere.Any(u => u.Owner != civ && u.InShip == null && u.Domain == domain));
        }

        public static IEnumerable<Tile> GetPossibleMoves(Tile tile, Unit unit)
        {
            var neighbours = unit switch
            {
                { Domain: UnitGas.Ground } => tile.Neighbours().Where(n =>
                    n.Type != TerrainType.Ocean || n.UnitsHere.Any(u =>
                        u.Owner == unit.Owner && u.ShipHold > 0 && u.CarriedUnits.Count < u.ShipHold)),

                { Domain: UnitGas.Sea, SubmarineAdvantagesDisadvantages: true } =>
                    tile.Neighbours().Where(t =>
                        t.Type == TerrainType.Ocean || (t.CityHere != null && t.CityHere.OwnerId == unit.Owner.Id)),
                { Domain: UnitGas.Sea } =>
                    tile.UnitsHere.Any(u =>
                        u is { Domain: UnitGas.Ground, MovePoints: > 0 } && u.InShip == unit)

                        ? tile.Neighbours().Where(n =>
                            !n.Terrain.Impassable)

                        : tile.Neighbours().Where(t => t.Type == TerrainType.Ocean ||
                                                       (t.CityHere != null && t.CityHere.OwnerId == unit.Owner.Id) ||
                                                       t.UnitsHere.Any(u => u.Owner != unit.Owner)),


                _ => tile.Neighbours().Where(n => !n.Terrain.Impassable)
            };
            if (unit.IgnoreZonesOfControl || !IsNextToEnemy(tile, unit.Owner, unit.Domain))
            {
                return neighbours;
            }
            return neighbours
                .Where(n => n.UnitsHere.Count > 0 || !IsNextToEnemy(n, unit.Owner, unit.Domain));
        }

        public static IList<int> GetIslandsFor(Unit unit)
        {
            if (unit.Domain == UnitGas.Sea)
            {
                return unit.CurrentLocation!.Type == TerrainType.Ocean
                    ? new List<int> { unit.CurrentLocation.Island }
                    : unit.CurrentLocation.Neighbours().Where(t => t.Type == TerrainType.Ocean).Select(t => t.Island)
                        .Distinct().ToList();
            }

            if (unit.CurrentLocation!.Type == TerrainType.Ocean)
            {
                return unit.CurrentLocation.Neighbours().Where(n => n.Type != TerrainType.Ocean)
                    .Select(n => n.Island).Distinct().ToList();
            }

            return new[] { unit.CurrentLocation.Island };
        }
    }
}
