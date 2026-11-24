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

            // And failing both, being dug in. Settlers and Engineers never are:
            // Civ II's combat guide names them as "the only units incapable of
            // fortifying", and a saved game or an older build may still carry the
            // order on one, so the bonus is refused here as well as at the order.
            if (positionFactor == 1m && defendingUnit.Order == (int)OrderType.Fortified &&
                defendingUnit.AiRole != AiRoleType.Settle)
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

        // The city's own defences: fighters scrambling to meet an air attack, SAM
        // batteries, SDI against a warhead, the coastal guns against a ship. The
        // bonus is named as well as paid, so the player can be told which of them
        // saved the stack (see CityDefenseBonus).
        df *= CityDefenseBonusOf(defendingUnit, attackingUnit, tile).Multiplier;

        // Effect of terrain
        df *= tile.Defense;

        return (double)df;
    }

    /// <summary>
    /// Which of Civ II's city defences applies to this attack, and how much it
    /// adds.
    /// <para>
    /// Every one of these was already paid -- the numbers are pinned by tests and
    /// unchanged here. What was missing was the name: a stack held by a SAM battery
    /// or a coastal fortress looked, from the map, exactly like a stack that had
    /// simply happened to survive, which is a poor way to learn a rule the Civilopedia
    /// states. The multiplier is returned rather than applied so <see cref="DefenseFactor"/>
    /// can keep its own order of multiplications, and so the caller that only wants
    /// the name does not have to redo the arithmetic.
    /// </para>
    /// <para>
    /// A city carrying both SAM batteries and SDI pays both against an attacker
    /// that SDI can hurt -- the batteries answer the conventional air attack, SDI
    /// the nuclear one -- and the stronger of the two is the one named.
    /// </para>
    /// </summary>
    private static (decimal Multiplier, DefenseBonusKind Kind) CityDefenseBonusOf(Unit defendingUnit, Unit attackingUnit, Tile tile)
    {
        if (tile.CityHere == null)
        {
            return (1m, DefenseBonusKind.None);
        }

        if (attackingUnit.Domain == UnitGas.Air)
        {
            // A fighter based in the city is launched to meet the attacker: x4
            // against anything that is not another fighter, x2 against one. Nothing
            // else the city carries applies on top of the scramble.
            if (defendingUnit is { Domain: UnitGas.Air, FuelRange: 1 })
            {
                return (attackingUnit.FuelRange == 1 ? 2m : 4m, DefenseBonusKind.FighterScramble);
            }

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

            decimal multiplier = 1m;
            var kind = DefenseBonusKind.None;
            decimal samMultiplier = 1m;

            // Effect of SAM batteries (only when attacked from air)
            if (samBonus > 0)
            {
                samMultiplier = 1m + samBonus / 100m;
                multiplier *= samMultiplier;
                kind = DefenseBonusKind.SamBattery;
            }

            if (sdiBonus > 0 &&
                attackingUnit.TypeDefinition.Effects.TryGetValue(UnitEffect.SDIVulnerable,
                    out var sdimulti) && sdimulti > 0)
            {
                var sdiMultiplier = 1m + sdiBonus * sdimulti / 100m;
                multiplier *= sdiMultiplier;

                if (samBonus == 0 || sdiMultiplier > samMultiplier)
                {
                    kind = DefenseBonusKind.MissileDefence;
                }
            }

            return (multiplier, kind);
        }

        if (attackingUnit.Domain == UnitGas.Sea)
        {
            var seaDefence =
                tile.CityHere.Improvements.Sum(i => i.Effects.GetValueOrDefault(Effects.SeaDefence, 0));
            if (seaDefence > 0)
            {
                return (1m + seaDefence / 100m, DefenseBonusKind.CoastalFortress);
            }
        }

        return (1m, DefenseBonusKind.None);
    }

    /// <summary>
    /// The name of the city defence standing between this attacker and this defender.
    /// <para>
    /// Asked once for the defender the combat resolver has already chosen, and
    /// carried on the combat event so the player can be told what held the attack
    /// back. <see cref="DefenseFactor"/> answers how hard the defender is to shift;
    /// this answers why.
    /// </para>
    /// </summary>
    public static DefenseBonusKind CityDefenseBonus(Unit defendingUnit, Unit attackingUnit, Tile tile) =>
        CityDefenseBonusOf(defendingUnit, attackingUnit, tile).Kind;

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