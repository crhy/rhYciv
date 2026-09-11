using Model.Constants;
using System;
using System.Linq;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Terrains;
using RhyCiv.Engine.Units;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine.UnitActions
{
    public static class UnitFunctions
    {
        /// <summary>
        /// Whether this unit can dig in where it is standing.
        /// </summary>
        /// <remarks>
        /// Settlers and Engineers cannot, in Civ II or here: the combat guide names
        /// them as "the only units incapable of fortifying". They were being
        /// allowed to, and a fortified Settlers unit took the same half-again
        /// defence as a Phalanx -- on top of the twenty hit points a Settlers unit
        /// already has, which is double every other unit of its age. That is what
        /// was behind "one settler in a city just killed two of my attacking
        /// horsemen".
        ///
        /// The hit points are not a fault and are left alone: a Settlers unit is
        /// genuinely hard to kill with an early attacker in Civ II, and a horseman
        /// losing to one in the open is the game working.
        /// </remarks>
        public static bool CanFortifyHere(Unit unit, Tile tile)
        {
            if (unit.AiRole == AiRoleType.Settle)
            {
                return false;
            }

            return unit.Domain switch
            {
                UnitGas.Ground => tile.Terrain.Type != TerrainType.Ocean,
                UnitGas.Air => tile.CityHere is not null || tile.EffectsList.Any(e => e.Target == ImprovementConstants.Airbase),
                UnitGas.Sea => tile.CityHere is not null,
                UnitGas.Special => true,
                _ => true
            };
        }

        public static bool CanEnter(UnitGas domain, Tile tile)
        {
            return domain switch
            {
                UnitGas.Ground => tile.Terrain.Type != TerrainType.Ocean,
                UnitGas.Air => true,
                UnitGas.Sea => tile.Terrain.Type == TerrainType.Ocean || tile.CityHere is not null,
                UnitGas.Special => true,
            };
        }
    }
}