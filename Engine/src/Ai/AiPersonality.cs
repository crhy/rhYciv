using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using Model.Core;
using Model.Core.Advances;

namespace RhyCiv.Engine.Ai;

/// <summary>
/// What a particular civilisation wants out of the world, beyond what every
/// civilisation wants.
/// <para>
/// Civ II's computer players have fixed temperaments -- aggressive or rational,
/// militaristic or civilised, expansionist or perfectionist -- and one of them
/// became famous by accident. Gandhi's India, the most peaceable civilisation in
/// the game, would build the bomb at the first opportunity and use it, and the
/// story of nuclear Gandhi outlived the game it came from. It is reproduced here
/// deliberately.
/// </para>
/// </summary>
public static class AiPersonality
{
    /// <summary>
    /// The civilisation that wants the bomb, matched on the tribe's name so that a
    /// ruleset which renames or reorders its tribes still gets the right one.
    /// </summary>
    public static bool WantsTheBomb(Civilization civ) =>
        civ.Adjective.Equals("Indian", StringComparison.OrdinalIgnoreCase) ||
        civ.TribeName.Equals("Indians", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What a nuclear power is aiming at, in order: fission first, because it opens
    /// the Manhattan Project, then rocketry, which is what puts a warhead on top of
    /// a missile.
    /// </summary>
    private static readonly int[] NuclearPath =
        [(int)AdvanceType.NuclearFiss, (int)AdvanceType.Rocketry, (int)AdvanceType.NuclearPwr];

    /// <summary>Whether this civilisation already has everything it needs to build one.</summary>
    public static bool HasTheBomb(Civilization civ) =>
        NuclearPath.Take(2).All(advance => AdvanceFunctions.HasTech(civ, advance));

    /// <summary>
    /// Whether this unit is a nuclear missile. Matched on the ruleset's unit type
    /// rather than on a flag, because what makes it a bomb is what it does on
    /// arrival, and that is decided here.
    /// </summary>
    public static bool IsNuclearMissile(Model.Core.Units.UnitDefinition definition) =>
        definition.Type == (int)UnitType.NuclearMsl;

    /// <summary>
    /// How many warheads are enough. Even an obsession has to leave a city free to
    /// build something else eventually.
    /// </summary>
    public const int WarheadsWanted = 4;

    /// <summary>
    /// Whether this civilisation should put the bomb ahead of whatever else it was
    /// going to build.
    /// </summary>
    public static bool WantsAnotherWarhead(Civilization civ) =>
        WantsTheBomb(civ) &&
        civ.Units.Count(unit => !unit.Dead && IsNuclearMissile(unit.TypeDefinition)) < WarheadsWanted;

    /// <summary>
    /// The research a civilisation should take next, or null when it has no
    /// particular opinion and the ordinary weighing should decide.
    /// </summary>
    public static Advance? PreferredResearch(IGame game, Civilization civ, IList<Advance> options)
    {
        if (options.Count == 0 || !WantsTheBomb(civ) || HasTheBomb(civ))
        {
            return null;
        }

        foreach (var goal in NuclearPath)
        {
            if (AdvanceFunctions.HasTech(civ, goal))
            {
                continue;
            }

            var step = options.FirstOrDefault(option => option.Index == goal)
                       ?? AdvanceFunctions.StepsToward(game, civ, goal, options).FirstOrDefault();

            if (step != null)
            {
                return step;
            }
        }

        return null;
    }
}
