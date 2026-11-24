using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Units;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Terrains;
using RhyCiv.Engine.UnitActions;
using Model.Constants;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Player;
using Model.Core.Units;

// `Path` here is the engine's unit movement path, not System.IO.Path, which
// ImplicitUsings now brings into scope.
using Path = RhyCiv.Engine.Units.Path;
namespace RhyCiv.Engine
{
    public partial class Game
    {
        /// <summary>
        /// This is now only used for lua script integration for other events raise them on the player version
        /// </summary>
        public event EventHandler<UnitEventArgs> OnUnitEvent;
        internal event EventHandler<CivEventArgs> OnCivEvent;

        private readonly int[] _doNothingOrders = { (int)OrderType.Fortified, (int)OrderType.Sleep };

        // Choose next unit for orders. If all units ended turn, update cities.
        private bool _choosingNextUnit;
        private bool _chooseNextUnitAgain;

        /// <summary>
        /// Most rounds of the deferred-request loop below. Each round either gives a
        /// unit its orders or ends a civilisation's turn, so a bound well past the
        /// number of units on the board can only be reached by a selector that is
        /// not making progress.
        /// </summary>
        private const int SelectionRounds = 512;

        public void ChooseNextUnit()
        {
            // Re-entry is normal here rather than exceptional. Choosing a unit tells
            // the player, the player moves it, and moving it to the end of its turn
            // asks for the next one -- from inside the call that is still choosing.
            // Recursing on that would run the whole turn down the stack and overflow
            // it, which .NET cannot catch and which has already killed one release.
            //
            // A nested request is therefore remembered and served by the loop below
            // once the current one has finished. It must not simply be dropped: a
            // dropped request is a computer civilisation that moves one unit and
            // then stops, and a turn that never comes back round to the player, who
            // is left pressing Enter at a game that appears to ignore them.
            if (_choosingNextUnit)
            {
                _chooseNextUnitAgain = true;
                return;
            }

            _choosingNextUnit = true;
            try
            {
                var rounds = 0;
                do
                {
                    _chooseNextUnitAgain = false;
                    ChooseNextUnitCore();
                } while (_chooseNextUnitAgain && ++rounds < SelectionRounds);
            }
            finally
            {
                _chooseNextUnitAgain = false;
                _choosingNextUnit = false;
            }
        }

        /// <summary>
        /// Most times round the selection loop below before giving up. One pass per
        /// unit that turns out to be unusable is plenty; the bound is only here so a
        /// selector that kept answering with the same unit could not hang the game.
        /// </summary>
        private const int SelectionAttempts = 64;

        private void ChooseNextUnitCore()
        {
            var player = Players[_activeCiv.Id];
            var units = _activeCiv.Units.Where(u => !u.Dead).ToList();

            // Look for units on this square or neighbours of this square.
            //
            // This is a loop rather than a recursive call. It used to ask again by
            // calling ChooseNextUnit, which the re-entrancy guard turns into a no-op
            // while a selection is already in progress -- so when the chosen unit
            // turned out to be unusable, nothing further happened at all. The
            // interface was left with no active unit *and* without being told the
            // turn was waiting to be ended, which is the state where a unit appears
            // to blink without responding and Enter seems to do nothing.
            Unit? nextUnit = null;
            for (var attempt = 0; attempt < SelectionAttempts; attempt++)
            {
                nextUnit = NextUnit(player, units);
                if (nextUnit == null || nextUnit.AwaitingOrders)
                {
                    break;
                }

                // It cannot be given orders, so it must not be offered: the
                // interface refuses a unit whose turn has ended and would be left
                // pointing at nothing. Take it out of the running and look again.
                player.WaitingList.Remove(nextUnit);
                units.Remove(nextUnit);
                nextUnit = null;
            }
            
            // End turn if no units awaiting orders
            if (nextUnit == null)
            {
                // Nothing left to move, so nothing should still be selected. The
                // ActiveUnit setter refuses a unit whose turn has ended and leaves
                // the previous one in place, so without this the unit that just
                // spent its last move point stayed active and went on blinking for
                // the rest of the turn.
                player.SetUnitActive(null, false);

                var anyUnitsMoved = units.Any(u => u.MovePointsLost > 0);
                if ((!anyUnitsMoved || Options.AlwaysWaitAtEndOfTurn))
                {
                    Players[_activeCiv.Id].WaitingAtEndOfTurn();
                }
                else
                {
                    if (ProcessEndOfTurn())
                    {
                        ChoseNextCiv();
                    }
                }
            }
            else
            {
                //TODO: determine the true values of these extra props
                OnUnitEvent?.Invoke(this, new ActivationEventArgs(unit: nextUnit, userInitiated: true, reactivation: false));
                player.SetUnitActive(nextUnit, true);
            }
        }

        private Unit? NextUnit(IPlayer player, List<Unit> units)
        {
            // The order the army is walked in. With the advanced setting on, the
            // next unit comes from the neighbourhood of the square the player is
            // working on; with it off, the order below is exactly what it always
            // was.
            if (Settings.NearbyUnitsFirstEnabled)
            {
                return NearbyUnit(player, units);
            }

            if (player.WaitingList is { Count: > 0 })
            {
                return
                    ActiveTile.UnitsHere.FirstOrDefault(u => u.AwaitingOrders && !player.WaitingList.Contains(u)) ??
                    ActiveTile
                        .Neighbours()
                        .SelectMany(
                            t => t.UnitsHere.Where(u =>
                                u.Owner == _activeCiv && u.AwaitingOrders && !player.WaitingList.Contains(u)))
                        .FirstOrDefault() ??
                    units.FirstOrDefault(u => u.AwaitingOrders && !player.WaitingList.Contains(u)) ??
                    ResetWaiting(player);

            }

            return ActiveTile.UnitsHere.FirstOrDefault(u => u.AwaitingOrders) ??
                   ActiveTile
                       .Neighbours()
                       .SelectMany(
                           t => t.UnitsHere.Where(u => u.Owner == _activeCiv && u.AwaitingOrders))
                       .FirstOrDefault() ?? units.FirstOrDefault(u => u.AwaitingOrders);

        }

        /// <summary>
        /// The next unit to be given its orders, taken from the area the player is
        /// working on rather than from wherever the unit list happens to keep it.
        /// <para>
        /// Reported as: "you move one unit, then it jumps to some totally different
        /// part of the map to move another unit, even though I had several unmoved
        /// units in that same area." The selector looked at the active square and
        /// its eight neighbours and, finding nothing there, took the first awaiting
        /// unit in list order -- which is another continent. The area is spent
        /// before the selector leaves it.
        /// </para>
        /// <para>
        /// This is a rule beyond ordinary Civ II play, so it sits behind an
        /// Advanced Settings checkbox and is off unless the player asks for it.
        /// </para>
        /// </summary>
        private Unit? NearbyUnit(IPlayer player, List<Unit> units)
        {
            var inReach = FirstUnitWithinReach(player);
            if (inReach != null)
            {
                return inReach;
            }

            // Nothing of ours awaiting orders anywhere on this map. A unit the walk
            // cannot reach -- standing on another map, or never placed -- is still
            // better offered than left out of the turn, so the old fallbacks stay
            // behind it, last resort rather than first answer.
            return units.FirstOrDefault(u => u.AwaitingOrders && !player.WaitingList.Contains(u)) ??
                   (player.WaitingList.Count > 0 ? ResetWaiting(player) : null);
        }

        /// <summary>
        /// The first of this civilisation's awaiting units found by walking outward
        /// from the active square one ring at a time, so the whole neighbourhood is
        /// finished before anything further away is considered. Squares within a
        /// ring are taken in (Y, X) order, which keeps the answer the same from
        /// one call to the next. Only the civilisation's own units are considered,
        /// as the squares around the active one always were.
        /// </summary>
        private Unit? FirstUnitWithinReach(IPlayer player)
        {
            var start = ActiveTile;
            if (start == null)
            {
                return null;
            }

            var seen = new HashSet<Tile> { start };
            var ring = new List<Tile> { start };

            // Each ring only adds squares not already reached, so the walk stops
            // once the map is covered rather than circling a wrapping world.
            while (ring.Count > 0)
            {
                foreach (var tile in ring.OrderBy(t => (t.Y, t.X)))
                {
                    var candidate = tile.UnitsHere
                        .Where(u => u.Owner == _activeCiv && u.AwaitingOrders &&
                                    !player.WaitingList.Contains(u))
                        .FirstOrDefault();

                    if (candidate != null)
                    {
                        return candidate;
                    }
                }

                var next = new List<Tile>();
                foreach (var tile in ring)
                {
                    foreach (var neighbour in tile.Neighbours())
                    {
                        if (seen.Add(neighbour))
                        {
                            next.Add(neighbour);
                        }
                    }
                }

                ring = next;
            }

            return null;
        }

        /// <summary>
        /// Everything the player told to wait has come round again. The list is
        /// emptied and the first of them that can still be given orders is offered.
        /// <para>
        /// This used to return the head of the list whatever state it was in. A unit
        /// told to wait and then fortified, or spent, or killed, was handed to the
        /// interface, which refuses a unit whose turn has ended -- leaving nothing
        /// selected and the turn not marked as waiting either.
        /// </para>
        /// </summary>
        private Unit? ResetWaiting(IPlayer player)
        {
            var waiting = player.WaitingList.ToList();
            player.WaitingList.Clear();
            return waiting.FirstOrDefault(unit => unit.AwaitingOrders);
        }

        /// <summary>
        /// Runs every unit's end-of-turn processing, and reports whether the turn
        /// can now end.
        /// <para>
        /// A unit may turn out to need a decision the player has to make — a GoTo
        /// whose route no longer exists, one that arrived with movement to spare,
        /// or a settler freed by finishing what it was building. This used to
        /// return the moment it found one, abandoning the rest of the list. Two
        /// things followed. Every unit after it in the order went unprocessed, so a
        /// unit told to fortify did not become fortified and construction did not
        /// complete, until whatever came before it had been dealt with. And because
        /// each press of End Turn processed only as far as the next such unit, a
        /// player with several of them had to press Enter over and over, appearing
        /// to do nothing each time.
        /// </para>
        /// <para>
        /// Every unit is now processed, and the first that wants a decision is
        /// offered once at the end. So a turn with nothing outstanding ends on the
        /// first press, and a turn with something outstanding asks about it once.
        /// </para>
        /// </summary>
        public bool ProcessEndOfTurn()
        {
            var player = Players[_activeCiv.Id];

            // The first unit that needs the player to decide something. Held rather
            // than acted on, so the rest of the army still gets its turn processed.
            Unit? awaitingOrders = null;

            // Snapshot: following a GoTo can pop a goody hut that hands the player new
            // units, and combat can kill them, both of which modify Units mid-loop.
            foreach (var unit in _activeCiv.Units.ToList())
            {
                if (unit.Dead)
                {
                    continue;
                }

                if (unit is { MovePoints: > 0, CurrentLocation: not null } && !_doNothingOrders.Contains(unit.Order))
                {
                    switch ((OrderType)unit.Order)
                    {
                        case OrderType.Fortify:
                            unit.Order = (int)OrderType.Fortified;
                            unit.MovePointsLost = unit.MovePoints;
                            break;
                        case OrderType.GoTo:
                            if (!unit.CurrentLocation.Map.IsValidTileC2(unit.GoToX, unit.GoToY))
                            {
                                ClearGotoOrder(unit);
                                break;
                            }

                            var destination = unit.CurrentLocation.Map.TileC2(unit.GoToX, unit.GoToY);
                            if (destination == unit.CurrentLocation)
                            {
                                ClearGotoOrder(unit);
                                break;
                            }

                            var path = Path.CalculatePathBetween(this, unit.CurrentLocation, destination, unit.Domain, unit.MaxMovePoints, unit.Owner, unit.Alpine, unit.IgnoreZonesOfControl);
                            if (path == null)
                            {
                                ClearGotoOrder(unit);
                                awaitingOrders ??= unit;
                                break;
                            }

                            var startedAt = unit.CurrentLocation;
                            path.Follow(this, unit);

                            if (unit.Dead)
                            {
                                break;
                            }

                            if (unit.CurrentLocation == destination)
                            {
                                ClearGotoOrder(unit);
                                break;
                            }

                            if (unit.CurrentLocation == startedAt)
                            {
                                // The path exists but the next square is blocked - an enemy
                                // in the way, or a zone of control. Keeping the GoTo order
                                // handed the unit straight back to the player, who could
                                // neither move it nor end the turn, because ending the turn
                                // came back here and offered the same stuck unit again.
                                // Drop the order so it is genuinely awaiting orders.
                                ClearGotoOrder(unit);
                                awaitingOrders ??= unit;
                                break;
                            }

                            if (unit.MovePoints > 0)
                            {
                                awaitingOrders ??= unit;
                            }

                            break;
                        case OrderType.Automate:
                            ProcessAutomatedSettler(unit);
                            break;
                        default:
                        {
                            unit.ProcessOrder();

                            if (TerrainImprovements.TryGetValue(unit.Building, out var improvement))
                            {
                                var completedUnits = this.CheckConstruction(unit.CurrentLocation, improvement);
                                foreach (var completedUnit in completedUnits.Where(u => u.WaitOrder && u.AiRole == AiRoleType.Settle))
                                {
                                    completedUnit.Order = (int)OrderType.Automate;
                                    completedUnit.Building = 0;
                                    completedUnit.SkipTurn();
                                }

                                var activeUnit = completedUnits.FirstOrDefault(u => u.MovePoints > 0 && !u.WaitOrder);
                                if (activeUnit != null)
                                {
                                    awaitingOrders ??= activeUnit;
                                }
                            }

                            break;
                        }
                    }
                }
            }

            if (awaitingOrders is { Dead: false })
            {
                player.SetUnitActive(awaitingOrders, true);
                return false;
            }

            return true;
        }

        private static void ClearGotoOrder(Unit unit)
        {
            unit.Order = (int)OrderType.NoOrders;
            unit.GoToX = unit.X;
            unit.GoToY = unit.Y;
            unit.GoToMapIndex = unit.MapIndex;
        }

        private void ProcessAutomatedSettler(Unit unit)
        {
            if (unit.AiRole != AiRoleType.Settle || unit.CurrentLocation == null)
            {
                unit.SkipTurn();
                return;
            }

            var currentTile = unit.CurrentLocation;
            if (TryAssignAutomatedSettlerJob(unit, currentTile))
            {
                return;
            }

            // Our own cities only. This did not check the owner, so after the
            // barbarians took a city the settler beside it carried on improving the
            // land -- developing terrain for whoever had just captured the place.
            var nearbyCity = currentTile.CityRadius()
                .Select(tile => tile.CityHere)
                .FirstOrDefault(city => city != null && city.OwnerId == unit.Owner.Id);

            var workTile = FindAutomatedSettlerWorkTile(unit, currentTile, nearbyCity?.Location);
            if (workTile != null)
            {
                StepAutomatedSettler(unit, currentTile, workTile);
                return;
            }

            // Nothing of ours to work on here. Walk to the nearest city we still
            // hold rather than loitering next to one we have lost.
            if (nearbyCity == null && NearestOwnCity(unit) is { } refuge)
            {
                var towards = MovementFunctions.GetPossibleMoves(currentTile, unit)
                    .Where(tile => tile.Type != TerrainType.Ocean && !tile.Terrain.Impassable)
                    .MinBy(tile => DistanceBetween(tile, refuge.Location));

                if (towards != null && DistanceBetween(towards, refuge.Location) <
                    DistanceBetween(currentTile, refuge.Location))
                {
                    StepAutomatedSettler(unit, currentTile, towards);
                    return;
                }
            }

            if (nearbyCity == null && currentTile.Type != TerrainType.Ocean)
            {
                var moreFertile = MovementFunctions.GetPossibleMoves(currentTile, unit)
                    .Where(t => t.Fertility > currentTile.Fertility && t.Type != TerrainType.Ocean)
                    .OrderByDescending(t => t.Fertility)
                    .FirstOrDefault();

                if (moreFertile != null)
                {
                    StepAutomatedSettler(unit, currentTile, moreFertile);
                }
                else
                {
                    CityActions.BuildCity(unit, this, CityActions.GetCityName(unit.Owner, this));
                }
                return;
            }

            unit.SkipTurn();
        }

        private bool TryAssignAutomatedSettlerJob(Unit unit, Tile currentTile)
        {
            var preferredImprovements = new[]
            {
                ImprovementTypes.Road,
                ImprovementTypes.Irrigation
            };

            foreach (var improvementId in preferredImprovements)
            {
                if (!CanAutomatedSettlerBuild(unit, currentTile, improvementId, out var improvement))
                {
                    continue;
                }

                unit.WaitOrder = true;
                unit.Build(improvement);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Move an automated settler one square, and hand it back to the player if it
        /// could not go.
        ///
        /// A move can be refused - an enemy zone of control, or a settler with no
        /// attack meeting the unit standing in its way. The order used to be restored
        /// regardless and no movement spent, so the settler chose the same blocked
        /// square every turn for the rest of the game. Automated units are not
        /// awaiting orders, so it was never offered for selection either: it simply
        /// sat there, unmoving and unreachable, which is what being chased by
        /// barbarians into a corner looked like.
        /// </summary>
        /// <summary>The closest city this unit's owner still holds, if any.</summary>
        private static City? NearestOwnCity(Unit unit)
        {
            var from = unit.CurrentLocation;
            return unit.Owner.Cities
                .Where(city => city.Location != null)
                .MinBy(city => DistanceBetween(from, city.Location));
        }

        /// <summary>
        /// Squared straight-line distance, which is all that is needed to compare
        /// two candidates and avoids a square root per tile considered.
        /// </summary>
        private static int DistanceBetween(Tile from, Tile to)
        {
            var dx = from.X - to.X;
            var dy = from.Y - to.Y;
            return dx * dx + dy * dy;
        }

        private void StepAutomatedSettler(Unit unit, Tile from, Tile to)
        {
            MovementFunctions.MoveC2(this, unit, to.X - from.X, to.Y - from.Y);
            if (unit.Dead)
            {
                return;
            }

            if (unit.CurrentLocation != from)
            {
                unit.Order = (int)OrderType.Automate;
                return;
            }

            // It could not move. Give it back rather than automating it into the same
            // wall next turn.
            unit.Order = (int)OrderType.NoOrders;
            unit.WaitOrder = false;
        }

        private Tile? FindAutomatedSettlerWorkTile(Unit unit, Tile currentTile, Tile? nearbyCityTile)
        {
            var cityRadius = nearbyCityTile?.CityRadius().ToHashSet();
            return MovementFunctions.GetPossibleMoves(currentTile, unit)
                .Where(t => t.Type != TerrainType.Ocean && t.CityHere == null)
                .Where(t => cityRadius == null || cityRadius.Contains(t))
                .Where(t => CanAutomatedSettlerImprove(unit, t))
                .OrderByDescending(t => AutomatedSettlerWorkScore(unit, t))
                .FirstOrDefault();
        }

        private bool CanAutomatedSettlerImprove(Unit unit, Tile tile)
        {
            return CanAutomatedSettlerBuild(unit, tile, ImprovementTypes.Road, out _) ||
                   CanAutomatedSettlerBuild(unit, tile, ImprovementTypes.Irrigation, out _);
        }

        private int AutomatedSettlerWorkScore(Unit unit, Tile tile)
        {
            var score = (int)tile.Fertility;
            if (tile.WorkedBy != null)
            {
                score += 200;
            }

            if (CanAutomatedSettlerBuild(unit, tile, ImprovementTypes.Road, out _))
            {
                score += 100;
            }

            if (CanAutomatedSettlerBuild(unit, tile, ImprovementTypes.Irrigation, out _))
            {
                score += 80;
            }

            return score;
        }

        private bool CanAutomatedSettlerBuild(Unit unit, Tile tile, int improvementId, out TerrainImprovement improvement)
        {
            if (!TerrainImprovements.TryGetValue(improvementId, out var foundImprovement))
            {
                improvement = null!;
                return false;
            }

            improvement = foundImprovement;

            var selectedImprovement = improvement;

            if (selectedImprovement.ExclusiveGroup > 0 &&
                tile.Improvements.Any(i => i.Group == selectedImprovement.ExclusiveGroup && i.Improvement != selectedImprovement.Id))
            {
                return false;
            }

            return TerrainImprovementFunctions.CanImprovementBeBuiltHere(tile, selectedImprovement, unit.Owner).Enabled;
        }
    }
}
