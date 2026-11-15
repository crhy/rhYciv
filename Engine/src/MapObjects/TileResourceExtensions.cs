using System;
using System.Linq;
using RhyCiv.Engine.Terrains;
using Model.Core;
using Model.Core.Mapping;

namespace RhyCiv.Engine.MapObjects;

public static class TileResourceExtensions
{
    public static int GetFood(this Tile tile, bool lowOrganisation)
        {
            decimal food = tile.EffectiveTerrain.Food;

            var foodEffects = tile.EffectsList.Where(e => e.Target == ImprovementConstants.Food).ToList();

            food += foodEffects.Where(e => e.Action == ImprovementActions.Add).Sum(e => e.Value);

            food = ApplyCityTerrainEffects(food, tile, ImprovementConstants.Food);
            
            if (tile.CityHere != null && food < 2)
            {
                food += 1;
            }
            if (food > 0)
            {
                var multiplier = foodEffects.Where(e => e.Action == ImprovementActions.Multiply)
                    .Sum(e => e.Value);
                if (multiplier != 0)
                {
                    food += food * (multiplier / 100m);
                }
            }

            if (lowOrganisation && food >= 3)
            {
                food -= 1;
            }

            return (int)food;
        }

        public static int GetTrade(this Tile tile,int organizationLevel)
        {
            decimal trade = tile.EffectiveTerrain.Trade;

            if (tile.River)
            {
                trade += tile.Terrain.RoadBonus;
            }

            var tradeEffects = tile.EffectsList.Where(e => e.Target == ImprovementConstants.Trade).ToList();

            trade += tradeEffects.Where(e => e.Action == ImprovementActions.Add).Sum(e => e.Value);

            // A city's own improvements raise the trade of the squares it works, and
            // they have to do it here rather than in the city's own totals: every
            // consumer -- the city's output, the tile map, the auto-start -- reads
            // this one extension, so a rule that lived anywhere else would make the
            // window and the totals disagree. Applied before the organisation rules
            // below so those still act on the raised figure.
            trade = ApplyCityTerrainEffects(trade, tile, ImprovementConstants.Trade);

            if (organizationLevel > 1 && trade > 0)
            {
                trade += 1;
            }

            if (trade > 0)
            {
                var multiplier = tradeEffects.Where(e => e.Action == ImprovementActions.Multiply)
                    .Sum(e => e.Value);
                if (multiplier != 0)
                {
                    trade += trade * (multiplier / 100m);
                }
            }

            if (organizationLevel == 0 && trade >= 3)
            {
                trade -= 1;
            }

            return (int)trade;
        }

        public static int GetShields(this Tile tile, bool lowOrganization)
        {
            // Grassland yields no shields on its own -- the ruleset gives it zero --
            // and the shield special is the exception: in Civ II a shielded grassland
            // square produces one, which is the entire reason the marker is drawn on
            // it. Reading the terrain's own figure for a shielded square therefore
            // returned zero, so the marker promised production the square never gave.
            decimal shields = tile.Type == TerrainType.Grassland
                ? (tile.HasShield ? tile.EffectiveTerrain.Shields + 1 : 0)
                : tile.EffectiveTerrain.Shields;

            var productionEffects = tile.EffectsList.Where(e => e.Target == ImprovementConstants.Shields).ToList();

            if (productionEffects.Count > 0)
            {
                shields += productionEffects.Where(e => e.Action == ImprovementActions.Add).Sum(e => e.Value);
                if (shields > 0)
                {
                    var multiplier = productionEffects.Where(e => e.Action == ImprovementActions.Multiply)
                        .Sum(e => e.Value);
                    if (multiplier != 0)
                    {
                        shields += shields * (multiplier / 100m);
                    }
                }
            }

            if (lowOrganization && shields >= 3)
            {
                shields -= 1;
            }

            // Civ II's Civilopedia, under Game Concepts / City Squares: "if the city
            // is built on Terrain that normally produces no Shields, one Shield is
            // automatically added to the other resources generated in the city
            // square." It is why founding on grassland is not the waste it looks
            // like, and why a city never has a turn where it can build nothing at
            // all. Measured against Civ II itself: Kells, on plain grassland, shows
            // seven shields where this game showed eight -- it counted a sixth
            // worked square Civ II did not -- and one of Civ II's seven is this.
            if (tile.CityHere != null && shields < 1)
            {
                shields = 1;
            }

            return (int)shields;
        }

        /// <summary>
        /// The improvements of the city working this square that ask for a change
        /// to one of its yields, and would apply to it.
        /// <para>
        /// Such an effect is conditioned on the square, not on the city: a Supermarket
        /// wants irrigation, a Harbour wants ocean, Super Highways want a road. The
        /// food and trade paths ask the same question, so they share it -- an effect
        /// must want this resource, must not name a terrain this square is not, and
        /// must not name a tile improvement this square does not carry at the level
        /// the effect asks for.
        /// </para>
        /// </summary>
        private static IEnumerable<CityTerrainEffect> CityTerrainEffects(Tile tile, int resource)
        {
            return tile.WorkedBy?.Improvements.SelectMany(ci =>
                ci.TerrainEffects?.Where(ef => ef.Resource == resource &&
                    (!ef.Terrain.HasValue || ef.Terrain.Value == (int)tile.Terrain.Type) &&
                    (!ef.Improvement.HasValue
                     || tile.Improvements.Any(i => i.Improvement == ef.Improvement && i.Level >= ef.Level))
                ) ?? Array.Empty<CityTerrainEffect>()) ?? Array.Empty<CityTerrainEffect>();
        }

        /// <summary>
        /// Applies those effects in action order: add, add-extra (which Civ II only
        /// gives a square that already produces something), then multiply, which the
        /// rules table writes as a percentage -- 150 is x1.5.
        /// </summary>
        private static decimal ApplyCityTerrainEffects(decimal value, Tile tile, int resource)
        {
            foreach (var effect in CityTerrainEffects(tile, resource).OrderBy(e => e.Action))
            {
                switch (effect.Action)
                {
                    case CityTerrainEffect.Add:
                        value += effect.Value;
                        break;
                    case CityTerrainEffect.AddExtra:
                        if (value > 0)
                        {
                            value += effect.Value;
                        }
                        break;
                    case CityTerrainEffect.Multiply:
                        value *= effect.Value / 100m;
                        break;
                }
            }

            return value;
        }

}