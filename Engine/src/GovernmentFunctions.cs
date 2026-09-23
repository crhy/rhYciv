using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using Model.Core;
using Model.Core.GameRules;

namespace RhyCiv.Engine;

/// <summary>
/// Changing how a civilisation is governed.
/// <para>
/// None of this existed. A civilisation was given Despotism when the game began
/// and stayed under it for the rest of the game however far it researched --
/// Monarchy, The Republic and Democracy could all be discovered and none of them
/// could be adopted. The REVOLUTION entry has been in the Kingdom menu the whole
/// time with nothing behind it.
/// </para>
/// </summary>
public static class GovernmentFunctions
{
    /// <summary>
    /// Which advance opens which government, by the advance's name in the rules
    /// file. Despotism is where everyone starts and Anarchy is not chosen, so
    /// neither appears here.
    /// <para>
    /// Keyed on the name rather than on <c>(int)AdvanceType</c>: the enum is a
    /// legacy constant list whose numbering follows a different order from the
    /// loaded RULES.txt. Monarchy is enum 54 but rules index 53, and 54 in the
    /// shipped ruleset is Monotheism -- so checking the enum number both failed to
    /// fire on researching Monarchy and offered Monarchy when Monotheism was
    /// discovered instead (#169). Names are what the ruleset carries, and they
    /// hold whatever order a ruleset chooses to list them in.
    /// </para>
    /// </summary>
    private static readonly (GovernmentType Government, string AdvanceName)[] Unlocks =
    [
        (GovernmentType.Monarchy, "Monarchy"),
        (GovernmentType.Communism, "Communism"),
        (GovernmentType.Fundamentalism, "Theology"),
        (GovernmentType.Republic, "The Republic"),
        (GovernmentType.Democracy, "Democracy"),
    ];

    /// <summary>
    /// Turns a civilisation spends in Anarchy between governments. Civ II makes a
    /// revolution cost something real without being ruinous; two turns of no taxes
    /// and no science is the price of changing your mind.
    /// </summary>
    public const int AnarchyTurns = 2;

    /// <summary>Where the loaded ruleset keeps an advance of this name, if it has one.</summary>
    private static int? AdvanceIndexNamed(Rules rules, string name) =>
        rules.Advances
            .FirstOrDefault(advance => string.Equals(advance.Name, name, StringComparison.OrdinalIgnoreCase))
            is { } advance ? advance.Index : null;

    /// <summary>The government an advance opens, if it opens one.</summary>
    public static GovernmentType? GovernmentUnlockedBy(int advanceIndex, Rules rules) =>
        Unlocks.Where(u => AdvanceIndexNamed(rules, u.AdvanceName) == advanceIndex)
            .Select(u => (GovernmentType?)u.Government)
            .FirstOrDefault();

    /// <summary>Whether this civilisation knows how to form a government.</summary>
    public static bool CanForm(Civilization civ, GovernmentType government, Rules rules)
    {
        if (government == GovernmentType.Anarchy)
        {
            return false;
        }

        if (government == GovernmentType.Despotism)
        {
            return true;
        }

        var unlock = Unlocks.FirstOrDefault(u => u.Government == government);
        return unlock.Government == government &&
               AdvanceIndexNamed(rules, unlock.AdvanceName) is { } advanceIndex &&
               AdvanceFunctions.HasTech(civ, advanceIndex);
    }

    /// <summary>
    /// Everything this civilisation could change to, in the order the rules list
    /// them. Its current government is not offered: a revolution that changed
    /// nothing would still cost the turns of anarchy.
    /// </summary>
    public static List<GovernmentType> AvailableGovernments(Civilization civ, Rules rules) =>
        System.Enum.GetValues<GovernmentType>()
            .Where(government => government != (GovernmentType)civ.Government &&
                                 CanForm(civ, government, rules))
            .ToList();

    /// <summary>Whether there is anything to change to.</summary>
    public static bool CanRevolt(Civilization civ, Rules rules) =>
        civ.AnarchyTurnsRemaining == 0 && AvailableGovernments(civ, rules).Count > 0;

    /// <summary>
    /// Throws the civilisation into Anarchy. What it becomes afterwards is chosen
    /// when the anarchy ends, not now, which is how Civ II does it -- the point of
    /// the interregnum is that you are not governed while it lasts.
    /// </summary>
    public static void BeginRevolution(IGame game, Civilization civ)
    {
        if (!CanRevolt(civ, game.Rules))
        {
            return;
        }

        civ.AnarchyTurnsRemaining = AnarchyTurns;
        AdoptGovernment(game, civ, GovernmentType.Anarchy);
    }

    /// <summary>
    /// Settles the civilisation under a government and brings every city's output
    /// back in line with it: rates, corruption, waste and unit support all follow
    /// the government, and none of them would change on their own.
    /// </summary>
    public static void AdoptGovernment(IGame game, Civilization civ, GovernmentType government)
    {
        civ.Government = (int)government;

        var rules = game.Rules.Governments[(int)government];
        ClampRatesTo(civ, rules);

        foreach (var city in civ.Cities)
        {
            city.SetUnitSupport(rules);
            city.CalculateOutput(civ.Government, game);
        }
    }

    /// <summary>
    /// A government caps how much can be spent on any one thing. Coming out of
    /// Despotism into Democracy the old rates are legal; going the other way they
    /// are not, and an illegal rate would quietly stay in force.
    /// </summary>
    private static void ClampRatesTo(Civilization civ, Model.Core.GameRules.Government government)
    {
        var maxScience = government.MaxRates.GetValueOrDefault("Science", 100);
        var maxTax = government.MaxRates.GetValueOrDefault("Tax", 100);

        civ.ScienceRate = System.Math.Clamp(civ.ScienceRate, 0, maxScience);
        civ.TaxRate = System.Math.Clamp(civ.TaxRate, 0, maxTax);

        // Rates are in tenths and the three have to add to a hundred; luxuries take
        // whatever the other two leave, so trimming them cannot leave a gap.
        if (civ.TaxRate + civ.ScienceRate > 100)
        {
            civ.ScienceRate = System.Math.Max(0, 100 - civ.TaxRate);
        }
    }
}
