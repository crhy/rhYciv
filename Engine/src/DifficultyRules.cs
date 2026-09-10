using System;
using Model.Constants;
using RhyCiv.Engine.Enums;
using Model.Core;
using Model.Core.Cities;

namespace RhyCiv.Engine;

/// <summary>
/// What choosing Prince over Deity actually changes.
/// <para>
/// The difficulty was asked for at the start of every game, written into the
/// save, and then read in three places: how content a city's people are, whether
/// barbarians land as veterans, and how far the computer players expand. Prince
/// and Deity played very nearly the same game.
/// </para>
/// <para>
/// Civ II's own levers, restored here: what the computer players pay for what
/// they build, how hard the barbarians hit, and the extra squeeze the computer
/// players get at the top two levels.
/// </para>
/// </summary>
public static class DifficultyRules
{
    /// <summary>
    /// What a computer civilisation pays for anything it builds, as a percentage
    /// of the listed cost. On Chieftain it pays over half as much again; on Deity
    /// it pays a fifth less than the player does.
    /// </summary>
    private static readonly int[] AiProductionCost = [160, 140, 120, 100, 90, 80];

    /// <summary>
    /// Barbarian attack strength, as a percentage. A Chieftain barbarian landing
    /// is a nuisance; a Deity barbarian landing is an invasion.
    /// </summary>
    private static readonly int[] BarbarianAttack = [25, 50, 75, 100, 125, 150];

    /// <summary>
    /// The extra shields a computer city gets at the top levels -- Civ II's
    /// "production box squeeze" -- as a percentage of what it produced honestly.
    /// </summary>
    private static readonly int[] AiProductionBonus = [0, 0, 0, 0, 20, 40];

    private static int Level(IGame game) =>
        Math.Clamp(game.DifficultyLevel, (int)DifficultyType.Chieftain, (int)DifficultyType.Deity);

    /// <summary>
    /// What this city has to put in the box before its item is finished. The
    /// player always pays the listed price; a computer civilisation pays what the
    /// difficulty says.
    /// </summary>
    public static int ProductionCost(IGame game, City city, int listedCost)
    {
        if (city.Owner.PlayerType != PlayerType.Ai)
        {
            return listedCost;
        }

        return Math.Max(1, listedCost * AiProductionCost[Level(game)] / 100);
    }

    /// <summary>
    /// The shields a city actually banks this turn, after the help a computer
    /// civilisation is given at the top two levels.
    /// </summary>
    public static int ShieldsBanked(IGame game, City city, int shields)
    {
        if (city.Owner.PlayerType != PlayerType.Ai || shields <= 0)
        {
            return shields;
        }

        return shields + shields * AiProductionBonus[Level(game)] / 100;
    }

    /// <summary>
    /// A barbarian's attack strength at this level. Everybody else fights at
    /// their listed strength.
    /// </summary>
    public static double AttackStrength(IGame game, Civilization attacker, double attackFactor)
    {
        if (attacker.PlayerType != PlayerType.Barbarians)
        {
            return attackFactor;
        }

        return attackFactor * BarbarianAttack[Level(game)] / 100d;
    }

    /// <summary>
    /// Whether barbarians come ashore as veterans, which Civ II starts doing at
    /// King.
    /// </summary>
    public static bool BarbariansAreVeterans(IGame game) => Level(game) >= (int)DifficultyType.King;

    /// <summary>The level's name, for anything that reports it.</summary>
    public static string Name(IGame game) => ((DifficultyType)Level(game)).ToString();
}
