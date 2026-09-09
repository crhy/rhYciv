using System.Collections.Generic;
using System.Linq;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Player;
using Model.Core.Production;
using RhyCiv.Engine.Production;

namespace RhyCiv.Engine;

/// <summary>
/// The world's view of the great works: who has begun one, who is about to
/// finish one, and what happens to everybody else's plans when one is finished.
/// <para>
/// Civ II reports all of this, and none of it was reported here. Worse, nothing
/// stopped two civilisations -- or two cities of the same civilisation -- from
/// each building the Pyramids, which is the one thing a wonder cannot be: there
/// is only ever one of each in the world.
/// </para>
/// </summary>
public static class WonderProgress
{
    /// <summary>
    /// The share of a wonder's cost at which rivals start hearing about it. Civ II
    /// warns when a wonder is close enough that there is still just time to react.
    /// </summary>
    private const int NearlyDonePercent = 75;

    /// <summary>The wonder a city is building, if it is building one.</summary>
    public static Improvement? WonderUnderConstruction(City city) =>
        city.ItemInProduction is BuildingProductionOrder { Improvement.IsWonder: true } order
            ? order.Improvement
            : null;

    /// <summary>The city holding this wonder, anywhere in the world.</summary>
    public static City? WonderBuiltIn(IGame game, int improvementType) =>
        game.AllCities.FirstOrDefault(city => city.ImprovementExists(improvementType));

    /// <summary>
    /// Every wonder in the world with the city that holds it, for the Wonders of
    /// the World report.
    /// </summary>
    public static IEnumerable<(Improvement Wonder, City? City)> AllWonders(IGame game)
    {
        var built = game.AllCities
            .SelectMany(city => city.Improvements.Where(i => i.IsWonder).Select(i => (Improvement: i, City: city)))
            .ToDictionary(entry => entry.Improvement.Type, entry => entry.City);

        return game.Rules.Improvements
            .Where(improvement => improvement.IsWonder)
            .Select(improvement => (improvement, built.GetValueOrDefault(improvement.Type)));
    }

    /// <summary>
    /// Reports a city's progress on a wonder, once the turn's shields have been
    /// added. The tests are crossings -- work that was below a mark and is now past
    /// it -- so each piece of news is delivered exactly once without anything
    /// having to be remembered between turns or written into a save.
    /// </summary>
    public static void ReportProgress(IGame game, City city, int shieldsBefore)
    {
        var wonder = WonderUnderConstruction(city);
        if (wonder == null)
        {
            return;
        }

        if (shieldsBefore <= 0 && city.ShieldsProgress > 0)
        {
            foreach (var player in Rivals(game, city.Owner))
            {
                player.WonderBegun(city, wonder);
            }
        }

        var nearly = wonder.Cost * NearlyDonePercent / 100;
        if (shieldsBefore < nearly && city.ShieldsProgress >= nearly && city.ShieldsProgress < wonder.Cost)
        {
            // Only the civilisations racing for the same wonder are told. Everyone
            // hearing about every rival's nearly-finished wonder would be noise;
            // the civilisation that is about to lose the race needs to know.
            foreach (var rival in game.AllCivilizations.Where(civ => civ != city.Owner))
            {
                if (rival.Cities.Any(other => WonderUnderConstruction(other)?.Type == wonder.Type))
                {
                    game.Players[rival.Id].WonderNearlyComplete(city, wonder);
                }
            }
        }
    }

    /// <summary>
    /// A wonder has been finished. It is news everywhere, it can never be built
    /// again, and anybody who was building it has just lost the race and their
    /// plans with it.
    /// </summary>
    public static void Completed(IGame game, City city, Improvement wonder)
    {
        WithdrawFromProduction(wonder);

        foreach (var rival in game.AllCivilizations.Where(civ => civ != city.Owner && civ.Alive))
        {
            var player = game.Players[rival.Id];

            var abandoned = rival.Cities
                .Where(other => WonderUnderConstruction(other)?.Type == wonder.Type)
                .ToList();

            foreach (var loser in abandoned)
            {
                // The shields stay in the box -- Civ II lets them go towards
                // whatever the city builds instead -- but the plans are gone.
                player.WonderLost(loser, wonder, city);
            }

            if (abandoned.Count == 0)
            {
                player.WonderCompleted(city, wonder);
            }
        }
    }

    /// <summary>
    /// Takes a completed wonder off every civilisation's build list, so no second
    /// copy of it can ever be started. A city already building it is left holding
    /// an order that is no longer valid, which the turn notices and replaces.
    /// </summary>
    private static void WithdrawFromProduction(Improvement wonder)
    {
        ProductionPossibilities.WithdrawImprovement(wonder.Type);
    }

    private static IEnumerable<IPlayer> Rivals(IGame game, Civilization owner) =>
        game.AllCivilizations
            .Where(civ => civ != owner && civ.Alive)
            .Select(civ => game.Players[civ.Id]);
}
