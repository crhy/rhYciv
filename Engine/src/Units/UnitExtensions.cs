using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine.Units;

public static class UnitExtensions
{
    /// <summary>
    /// Defence bonus a city wall provides, matching the bundled ruleset. Used when
    /// a wonder stands in for the building itself.
    /// </summary>
    private const int FreeCityWallEffect = 200;

    public static double AttackFactor(this Unit attackUnit, Unit defendingUnit)
    {
        // Base attack factor from RULES
        double af = attackUnit.AttackBase;

        // Bonus for veteran units
        if (attackUnit.Veteran) af *= 1.5;

        // Partisan bonus against non-combat units
        if (attackUnit.TypeDefinition.Effects.TryGetValue(UnitEffect.Partisan, out var effect) && defendingUnit.AttackBase == 0)
        {
            af *= effect;
        }

        // The Great Wall doubles its owner's attack strength against barbarians.
        if (defendingUnit.Owner.PlayerType == PlayerType.Barbarians &&
            WonderFunctions.DoublesAttackAgainstBarbarians(attackUnit.Owner))
        {
            af *= 2;
        }

        return af;
    }


    /// <summary>
    /// How hard a unit is to shift, all its bonuses applied.
    /// </summary>
    /// <remarks>
    /// Returned as it is calculated rather than rounded down to a whole number.
    /// The whole of this is worked out in fractions -- fortifying is half again,
    /// a river a quarter more, forest and jungle half again -- and every one of
    /// them used to be thrown away at the end by a cast to int.
    ///
    /// On the values the tests used, all tens and hundreds, that cost nothing. On
    /// the values the game actually uses it cost the defender the bonus outright:
    /// Warriors defend at 1, so fortifying took them to 1.5 and the cast took them
    /// straight back to 1. Digging in did nothing whatever for the weakest unit in
    /// the game, which is the one most often left holding a new city -- and the
    /// attacker's side of the same sum was never rounded, so the loss was all the
    /// defender's. It is why a barbarian horseman went through fortified warriors
    /// in one city after another (#61, #75).
    /// </remarks>
    public static double DefenseFactor(this Unit defendingUnit, Unit attackingUnit, Tile tile, int groundDefMultiplier)
    {
        //Carried units cannot be the defender
        if (defendingUnit.InShip != null) return 0;

        // Base defense factor from RULES
        decimal df = defendingUnit.DefenseBase;

        // Bonus for veteran units
        if (defendingUnit.Veteran) df *= 1.5m;

        // Pikemen-style bonus: Civ II doubles the defence against every mounted unit.
        if (defendingUnit.X2OnDefenseVersusHorse && IsMountedAttacker(attackingUnit))
        {
            df *= 2m;
        }

        // AEGIS-style bonus: x3 against aircraft, x5 against missiles.
        if (defendingUnit.X2OnDefenseVersusAir && attackingUnit.Domain == UnitGas.Air)
        {
            df *= attackingUnit.DestroyedAfterAttacking ? 5m : 3m;
        }

        // Prepared-position bonuses (land units only)
        if (defendingUnit.Domain == UnitGas.Ground)
        {
            // City walls, a fortress, or being dug in: one of the three, never a
            // combination. Civ II's combat guide puts it as the fortification bonus
            // being "superceded by fortress improvement and city walls", and testing
            // on the original reported at CivFanatics found the game takes the walls
            // or the fortress even where fortifying would have given the better
            // number -- so this is an order of precedence rather than the best of
            // the three. They used to be multiplied together, which made a fortified
            // garrison behind walls x4.5 where Civ II gives x3.
            var positionFactor = 1m;

            // Walls first. They answer land attacks only, and a Howitzer-style
            // attacker ignores them.
            if (tile.CityHere != null && !attackingUnit.NegatesCityWalls &&
                attackingUnit.Domain == UnitGas.Ground)
            {
                var wallEffect =
                    tile.CityHere.Improvements.Sum(i => i.Effects.GetValueOrDefault(Effects.Walled, 0));

                // The Great Wall stands in for city walls wherever a city has none.
                if (wallEffect < FreeCityWallEffect &&
                    WonderFunctions.HasFreeCityWalls(tile.CityHere.Owner))
                {
                    wallEffect = FreeCityWallEffect;
                }

                // Walls multiply the defence they protect. Adding the effect value
                // on its own made City Walls a flat +2 whatever the garrison was.
                if (wallEffect != 0)
                {
                    positionFactor = 1m + wallEffect / 100m;
                }
            }

            // Then a fortress. The unit does not have to have been told to fortify,
            // and the bonus does not apply when the attack comes from the air.
            if (positionFactor == 1m && groundDefMultiplier != 0 && attackingUnit.Domain != UnitGas.Air)
            {
                positionFactor = 1m + groundDefMultiplier / 100m;
            }

            // And failing both, being dug in.
            if (positionFactor == 1m && defendingUnit.Order == (int)OrderType.Fortified)
            {
                positionFactor = 1.5m;
            }

            df *= positionFactor;
        }

        // Helicopters are vulnerable to anti air
        else if (defendingUnit is { Domain: UnitGas.Air, FuelRange: 0 } && attackingUnit.CanAttackAirUnits)
        {
            df /= 2;
        }

        if (tile.CityHere != null)
        {
            if (attackingUnit.Domain == UnitGas.Air)
            {
                if (defendingUnit is { Domain: UnitGas.Air, FuelRange: 1 })
                {
                    // TODO: Message box about fighters scrambling for defence
                    if (attackingUnit.FuelRange != 1)
                    {
                        df *= 4;
                    }
                    else
                    {
                        df *= 2;
                    }
                }
                else
                {
                    int samBonus = 0;
                    int sdiBonus = 0;
                    foreach (var improvement in tile.CityHere.Improvements)
                    {
                        if (improvement.Effects.TryGetValue(Effects.AirDefence, out var sam))
                        {
                            samBonus += sam;
                        }

                        if (improvement.Effects.TryGetValue(Effects.MissileDefence, out var missile))
                        {
                            sdiBonus += missile;
                        }
                    }

                    // Effect of SAM batteries (only when attacked from air)
                    if (samBonus > 0)
                    {
                        //TODO: SAM message?
                        df += df * samBonus / 100m;
                    }

                    if (sdiBonus > 0 &&
                        attackingUnit.TypeDefinition.Effects.TryGetValue(UnitEffect.SDIVulnerable,
                            out var sdimulti) && sdimulti > 0)
                    {
                        df += df * sdiBonus * sdimulti / 100m;
                    }
                }
            }
            else if (attackingUnit.Domain == UnitGas.Sea)
            {
                var seaDefence =
                    tile.CityHere.Improvements.Sum(i => i.Effects.GetValueOrDefault(Effects.SeaDefence, 0));
                if (seaDefence > 0)
                {
                    //TODO: Coastal fortress message
                    df += df * seaDefence / 100m;
                }
            }
        }

        // Effect of terrain
        df *= tile.Defense;

        return (double)df;
    }

    /// <summary>
    /// Civ II has no explicit "is a horse" flag. A mounted attacker is a land unit
    /// with two movement points and a single firepower, which selects exactly the
    /// Horsemen-through-Cavalry line in the standard rules while still working for
    /// custom rulesets. Hit points are deliberately not part of the test: Dragoons
    /// and Cavalry carry two, and requiring one excluded the very units Pikemen
    /// exist to stop. The firepower term is what keeps the Howitzer out.
    /// </summary>
    private static bool IsMountedAttacker(Unit attackingUnit)
    {
        var definition = attackingUnit.TypeDefinition;
        return attackingUnit.Domain == UnitGas.Ground &&
               definition.AttackPerTurn == 2 &&
               definition.Firepwr == 1;
    }
}