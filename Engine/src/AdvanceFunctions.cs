using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Production;
using RhyCiv.Engine.Terrains;
using Model.Constants;
using Model.Core;
using Model.Core.Advances;
using Model.Core.GameRules;

namespace RhyCiv.Engine.Advances
{
    public static class AdvanceFunctions
    {
        private static AdvanceResearch[] _researched = [];

        /// <summary>
        /// Who discovered what first, one entry per advance in the rules.
        /// <para>
        /// This is static and was only ever filled in by <see cref="SetupTech"/>, so
        /// it kept whatever the last game to start had put there. Starting a second
        /// game on a ruleset with a different number of advances left it the wrong
        /// length, and the next advance anybody researched read off the end of it.
        /// It is resized here instead, which also means an advance can be granted
        /// before a game has been fully set up.
        /// </para>
        /// </summary>
        private static AdvanceResearch[] Researched(IGame game)
        {
            if (_researched.Length != game.Rules.Advances.Length)
            {
                _researched = game.Rules.Advances.Select(_ => new AdvanceResearch()).ToArray();
            }

            return _researched;
        }

        private static int _mapSizeAdjustment;
        
        public static void SetupTech(this Game game)
        {
            _researched = game.Rules.Advances.OrderBy(a=>a.Index).Select(a=> new AdvanceResearch()).ToArray();
            
            _mapSizeAdjustment = game.TotalMapArea / 1000;

            foreach (var civilization in game.AllCivilizations)
            {
                SetEpoch(game, civilization);

                for (var advanceIndex = 0; advanceIndex < game.Rules.Advances.Length; advanceIndex++)
                {
                    if (civilization.Advances.Length <= advanceIndex || !civilization.Advances[advanceIndex]) continue;

                    foreach (var effect in game.Rules.Advances[advanceIndex].Effects
                                 .Where(effect => effect.Key != Effects.EpochTech))
                    {
                        civilization.GlobalEffects[effect.Key] = civilization.GlobalEffects.GetValueOrDefault(effect.Key) + effect.Value;
                    }
                }
            }
            
            ProductionPossibilities.InitializeProductionLists(game.AllCivilizations, ProductionOrder.GetAll( game.Rules));
        }
        
        public static bool HasAdvanceBeenDiscovered(this Game game, int advanceIndex, int byCiv = -1)
        {
            return HasAdvanceBeenDiscovered(advanceIndex) &&
                   (byCiv == -1 || HasTech(game.AllCivilizations[byCiv], advanceIndex));
        }
        
        public static bool HasAdvanceBeenDiscovered(int advanceIndex)
        {
            var research = _researched[advanceIndex];
            return research.Discovered;
        }

        public static void RemoveAdvance(this Game game, int advanceIndex, Civilization civilization)
        {
            if (!civilization.Advances[advanceIndex]) return;

            foreach (var effect in game.Rules.Advances[advanceIndex].Effects)
            {
                if (effect.Key == Effects.EpochTech)
                {
                    SetEpoch(game, civilization);
                }
                else
                {
                    civilization.GlobalEffects[effect.Key] = civilization.GlobalEffects.GetValueOrDefault(effect.Key) - effect.Value;
                }
            }

            civilization.Advances[advanceIndex] = false;
            var allOrders = ProductionOrder.GetAll(game.Rules);
            ProductionPossibilities.RemoveItems(civilization.Id,
                allOrders.Where(i => i.RequiredTech == advanceIndex));
            ProductionPossibilities.AddItems(civilization.Id,
                allOrders.Where(o => o.ExpiresTech == advanceIndex && o.CanBuild(civilization)));
        }

        private static void SetEpoch(Game game, Civilization civilization)
        {
            civilization.Epoch = game.Rules.Advances.Where(a => a.Effects.ContainsKey(Effects.EpochTech))
                .GroupBy(a => a.Effects[Effects.EpochTech])
                .Where(techs => techs.All(t => t.Index < civilization.Advances.Length && civilization.Advances[t.Index])).Select(t => t.Key)
                .DefaultIfEmpty(0).Max();
        }

        public static void GiveAdvance(this IGame game, int advanceIndex, Civilization civilization)
        {
            var research = Researched(game)[advanceIndex];
            if (HasTech(civilization, advanceIndex)) return;
            if (GetAdvanceGroupAccess(civilization, game.Rules.Advances[advanceIndex]) == AdvanceGroupAccess.Prohibited) return;

            ApplyCivAdvance(game, advanceIndex, civilization, research, civilization.Id);
        }

        /// <summary>
        /// How many advances the first civilisation to discover Philosophy gets
        /// for nothing.
        /// </summary>
        public const int PhilosophyFreeAdvances = 2;

        /// <summary>
        /// Philosophy, found by name rather than by <c>(int)AdvanceType</c>: the
        /// legacy enum numbers it 60, but the loaded rules keep their own order
        /// and there Philosophy sits at 59 -- the same trap that made Monarchy
        /// (#169) read as Monotheism. Names hold whatever order a ruleset
        /// chooses to list them in.
        /// </summary>
        private const string PhilosophyAdvanceName = "Philosophy";

        private static int? PhilosophyIndex(Rules rules) =>
            rules.Advances
                .FirstOrDefault(advance => string.Equals(advance.Name, PhilosophyAdvanceName,
                    StringComparison.OrdinalIgnoreCase)) is { } advance ? advance.Index : null;

        /// <summary>
        /// Whether an advance index is the loaded rules' Philosophy.
        /// </summary>
        public static bool IsPhilosophy(this IGame game, int advanceIndex) =>
            PhilosophyIndex(game.Rules) == advanceIndex;

        /// <summary>
        /// Whether this civilisation is the one that discovered Philosophy first.
        /// The first discovery is the only one that writes a name into the
        /// record, so everybody who comes after reads the name already there.
        /// </summary>
        public static bool PhilosophyDiscoveredBy(this IGame game, Civilization civ)
        {
            var philosophy = PhilosophyIndex(game.Rules);
            return philosophy is not null && Researched(game)[philosophy.Value].DiscoveredBy == civ.Id;
        }

        /// <summary>
        /// The two advances the first discoverer of Philosophy is owed, granted
        /// on top of Philosophy itself. They are the cheapest two the
        /// civilisation could legally take: research here is paid per
        /// civilisation, not per advance -- every advance costs the same -- so
        /// the rules' own order decides which two are cheapest, and any advance
        /// the civilisation already knows or is barred from is skipped.
        /// <para>
        /// A civilisation that reaches Philosophy after somebody else is told
        /// about the discovery, not paid for it: the name in the record is not
        /// theirs. <see cref="GiveAdvance"/> is the only mutator here, so a free
        /// advance the civilisation is barred from is refused there rather than
        /// sneaking past this list.
        /// </para>
        /// </summary>
        public static void PhilosophyFreeAdvanceGrant(this IGame game, Civilization civ)
        {
            var philosophy = PhilosophyIndex(game.Rules);
            if (philosophy is null || !game.PhilosophyDiscoveredBy(civ))
            {
                return;
            }

            var free = game.Rules.Advances
                .Where(advance => advance.Index != philosophy.Value &&
                                  !HasTech(civ, advance.Index) &&
                                  GetAdvanceGroupAccess(civ, advance) != AdvanceGroupAccess.Prohibited)
                .OrderBy(advance => advance.Index)
                .Take(PhilosophyFreeAdvances)
                .ToList();

            foreach (var advance in free)
            {
                game.GiveAdvance(advance.Index, civ);
            }
        }

        /// <summary>
        /// What a civilisation that was beaten to Philosophy is told about it:
        /// the discovery happened somewhere, and the discoverers are named only
        /// once they have been met. Null while nobody has discovered Philosophy,
        /// for the civilisation that discovered it, and for anybody the record
        /// has no name for.
        /// </summary>
        public static string? PhilosophyDiscoveredNotice(this IGame game, Civilization civ)
        {
            var philosophy = PhilosophyIndex(game.Rules);
            if (philosophy is null)
            {
                return null;
            }

            var discovererId = Researched(game)[philosophy.Value].DiscoveredBy;
            if (discovererId < 0 || discovererId == civ.Id ||
                discovererId >= game.AllCivilizations.Count)
            {
                return null;
            }

            var discoverer = game.AllCivilizations[discovererId];
            return DiplomacyFunctions.HaveMet(civ, discoverer)
                ? $"The {discoverer.TribeName} have discovered Philosophy."
                : "Philosophy has been discovered in a faraway land.";
        }

        private static void ApplyCivAdvance(IGame game, int advanceIndex, Civilization civilization, AdvanceResearch research,
            int targetCiv)
        {
            if (!research.Discovered)
            {
                research.DiscoveredBy = targetCiv;
                game.History.AdvanceDiscovered(advanceIndex, civilization);
            }

            if (civilization.ReseachingAdvance == advanceIndex)
            {
                civilization.ReseachingAdvance = AdvancesConstants.No;
            }

            // Arriving at the goal retires it. Leaving it set would have the
            // research chooser go on offering a route to somewhere already reached.
            if (civilization.ResearchGoal == advanceIndex)
            {
                civilization.ResearchGoal = -1;
            }

            foreach (var effect in game.Rules.Advances[advanceIndex].Effects)
            {
                if (effect.Key == Effects.EpochTech)
                {
                    if (civilization.Epoch < effect.Value)
                    {
                        var hasAllEpochTechs = game.Rules.Advances.Where(a =>
                            a.Effects.ContainsKey(Effects.EpochTech) && a.Effects[Effects.EpochTech] == effect.Value).All(a=>a.Index == advanceIndex || civilization.Advances[a.Index]);
                        if (hasAllEpochTechs)
                        {
                            civilization.Epoch = effect.Value;
                        }
                    } 
                }
                else
                {
                    civilization.GlobalEffects[effect.Key] = civilization.GlobalEffects.GetValueOrDefault(effect.Key) + effect.Value;
                }
            }

            foreach (var improvement in game.TerrainImprovements.Values)
            {
                for (var level = 0; level < improvement.Levels.Count; level++)
                {
                    if (improvement.Levels[level].RequiredTech != advanceIndex) continue;
                    
                    game.Players[civilization.Id].NotifyImprovementEnabled(improvement, level);

                    if (!improvement.AllCitys) continue;
                    var locations = civilization.Cities
                        .Select(c => c.Location)
                        .Select(tile => new
                        {
                            tile,
                            terrain = improvement.AllowedTerrains[tile.Z]
                                .FirstOrDefault(t => t.TerrainType == (int)tile.Type)
                        })
                        .Where(t => t.terrain is not null)
                        .Select(loc =>
                        {
                            loc.tile.AddImprovement(improvement, loc.terrain, level,
                                game.Rules.Terrains[loc.tile.Z], loc.tile.GetCivsVisibleTo(game));
                            return loc.tile;
                        }).ToList();
                    game.UpdateTiles(improvement.HasMultiTile ? locations.Concat(locations.SelectMany(l=> l.Neighbours())).ToList() : locations );
                }
            }

            if (civilization.Advances.Length <= advanceIndex)
            {
                var advances = new bool[game.Rules.Advances.Length];
                Array.Copy(civilization.Advances, advances, civilization.Advances.Length);
                civilization.Advances = advances;
            }
            civilization.Advances[advanceIndex] = true;

            // A form of government has just become possible. Civ II offers the
            // revolution at this point rather than leaving the player to notice.
            // After the grant, not before: the offer's guard asks whether the
            // civilisation can form the new government, which reads the very tech
            // bit that has only just been set -- asking first meant the answer was
            // always no on the turn the advance landed (#169).
            if (GovernmentFunctions.GovernmentUnlockedBy(advanceIndex, game.Rules) is { } government &&
                civilization.Id >= 0 && civilization.Id < game.Players.Length)
            {
                game.Players[civilization.Id].GovernmentAvailable((int)government);
            }

            var orders = ProductionOrder.GetAll(game.Rules);
            ProductionPossibilities.AddItems(targetCiv,
                orders.Where(i => i.RequiredTech == advanceIndex && i.CanBuild(civilization)));
            ProductionPossibilities.RemoveItems(targetCiv, orders.Where(o => o.ExpiresTech == advanceIndex));
        }

        public static int TotalAdvances(this IGame game, int targetCiv)
        {
            return game.AllCivilizations[targetCiv].Advances.Count(a => a);
        }

        /// <summary>
        ///  I'm not sure if this formula is correct I've just grabed if from https://forums.civfanatics.com/threads/tips-tricks-for-new-players.96725/
        /// </summary>
        /// <param name="game"></param>
        /// <param name="civ"></param>
        /// <returns></returns>
        public static int CalculateScienceCost(IGame game, Civilization civ)
        {
            if (civ.ReseachingAdvance < 0) return -1;
            var techParadigm = game.Rules.Cosmic.TechParadigm;
            var ourAdvances = TotalAdvances(game, civ.Id);
            var keyCivAdvances = TotalAdvances(game, civ.PowerRank);
            var techLead = (ourAdvances - keyCivAdvances) / 3;
            var baseCost = techParadigm + techLead;

            if (ourAdvances > 20)
            {
                baseCost += _mapSizeAdjustment;
            }

            return baseCost * (ourAdvances +1);

        }

        public static int CalculateResearchProgressQuarter(int progress, int cost)
        {
            if (cost <= 0)
            {
                return 0;
            }

            return Math.Clamp((int)((long)Math.Max(0, progress) * 4 / cost), 0, 3);
        }

        public static bool HasTech(Civilization civ, int tech)
        {
            if (tech < 0)
            {
                return tech == AdvancesConstants.Nil;
            }

            return tech < civ.Advances.Length && civ.Advances[tech];
        }

        public static List<Advance> CalculateAvailableResearch(IGame game, Civilization activeCiv)
        {
            var allAvailable = game.Rules.Advances.Where(a =>
                GetAdvanceGroupAccess(activeCiv, a) == AdvanceGroupAccess.CanResearch &&
                HasTech(activeCiv, a.Prereq1) && HasTech(activeCiv, a.Prereq2) &&
                (a.Index >= activeCiv.Advances.Length || !activeCiv.Advances[a.Index])).ToList();
            
            //TODO: cull list based on difficulty
            return allAvailable.ToList();
        }
        
        /// <summary>
        /// Every advance this civilisation still has to learn before
        /// <paramref name="goal"/> is within reach, the goal itself included.
        /// <para>
        /// Walks the prerequisite tree from the goal downwards, stopping wherever
        /// the civilisation already knows an advance. An advance it is barred from
        /// by its advance group is still reported, because the caller needs to be
        /// able to tell the player that the goal cannot be reached at all rather
        /// than silently returning a shorter answer.
        /// </para>
        /// </summary>
        public static HashSet<int> AdvancesNeededFor(IGame game, Civilization civ, int goal)
        {
            var needed = new HashSet<int>();
            var advances = game.Rules.Advances;
            if (goal < 0 || goal >= advances.Length || HasTech(civ, goal))
            {
                return needed;
            }

            var pending = new Stack<int>();
            pending.Push(goal);

            while (pending.Count > 0)
            {
                var index = pending.Pop();
                if (index < 0 || index >= advances.Length || HasTech(civ, index) || !needed.Add(index))
                {
                    continue;
                }

                // Nil marks "no prerequisite", and the visited set above is what
                // keeps a ruleset with a circular prerequisite from spinning here.
                pending.Push(advances[index].Prereq1);
                pending.Push(advances[index].Prereq2);
            }

            return needed;
        }

        /// <summary>
        /// The advances out of <paramref name="options"/> that lead towards
        /// <paramref name="goal"/> -- its outstanding prerequisites, and the goal
        /// itself once everything it needs is known.
        /// <para>
        /// An empty answer means nothing that can be started now brings the goal
        /// any nearer, which the caller is expected to say out loud rather than
        /// present as an empty list.
        /// </para>
        /// </summary>
        public static List<Advance> StepsToward(IGame game, Civilization civ, int goal,
            IEnumerable<Advance> options)
        {
            var needed = AdvancesNeededFor(game, civ, goal);
            return needed.Count == 0
                ? new List<Advance>()
                : options.Where(a => needed.Contains(a.Index)).ToList();
        }

        /// <summary>
        /// Everything this civilisation could sensibly aim at: advances it does not
        /// have and is not barred from. Unlike the research list this is not limited
        /// to what can be started now, because the point of a goal is that it is
        /// several advances away.
        /// </summary>
        public static List<Advance> PossibleResearchGoals(IGame game, Civilization civ)
        {
            return game.Rules.Advances
                .Where(a => GetAdvanceGroupAccess(civ, a) == AdvanceGroupAccess.CanResearch &&
                            !HasTech(civ, a.Index))
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Get a list of advances that can be taken by the active civilization from another civilization.
        ///
        ///     This could be used for diplomat/spy steals or gaining tech on city capture
        /// </summary>
        /// <param name="game">The game object</param>
        /// <param name="activeCiv">Civ gaining tech</param>
        /// <param name="fromCiv">Civ that has tech</param>
        /// <returns>List of takable techs</returns>
        public static List<Advance> CalculateResearchTheft(IGame game, Civilization activeCiv, Civilization fromCiv)
        {
            return game.Rules.Advances.Where(a =>
                GetAdvanceGroupAccess(activeCiv, a) == AdvanceGroupAccess.CanResearch &&
                HasTech(fromCiv, a.Index) && !HasTech(activeCiv, a.Index)).ToList();
        }

        private static AdvanceGroupAccess GetAdvanceGroupAccess(Civilization civilization, Advance advance)
        {
            return advance.AdvanceGroup >= 0 && advance.AdvanceGroup < civilization.AllowedAdvanceGroups.Length
                ? civilization.AllowedAdvanceGroups[advance.AdvanceGroup]
                : AdvanceGroupAccess.CanResearch;
        }
    }
}
