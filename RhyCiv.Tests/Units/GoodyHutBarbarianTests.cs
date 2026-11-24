using RhyCiv.Engine.Enums;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.TestFiles;

namespace RhyCiv.Tests.Units;

/// <summary>
/// The horde a goody hut unleashes is always a pair, and three on Restless
/// Tribes or Raging Hordes. Never the lone warrior it regressed to.
/// <para>
/// Hut barbarians dropped from 2 to 1 on Villages Only and Roving Bands
/// because <c>BarbarianActivity + 1</c> capped at 1 for activity 0 (#161).
/// Civ II hut hordes are at least a pair and scale to 3 with activity -- the
/// same pair-to-three scaling on the Barbarity answer as the uprising
/// raiders use (BarbarianUprisings' PartySize of 2/3/4).
/// </para>
/// <para>
/// This drives the real game's Barbarity answer through the exact helper
/// <c>ApplyBarbarianOutcome</c> takes its spawn squares by, so the lower
/// bound cannot silently regress again.
/// </para>
/// </summary>
public class GoodyHutBarbarianTests
{
    [Theory]
    [InlineData(BarbarianActivityType.VillagesOnly, 2)]
    [InlineData(BarbarianActivityType.RovingBands, 2)]
    [InlineData(BarbarianActivityType.RestlessTribes, 3)]
    [InlineData(BarbarianActivityType.RagingHordes, 3)]
    public void AHutHorde_IsAPairAndThreeOnRestlessOrRaging(BarbarianActivityType activity, int expected)
    {
        // The real game with the Barbarity question answered a particular way;
        // the factory throws if the loaded game does not report the activity
        // it was asked for, so the test cannot silently drift off the setting.
        var (game, _, _) = CleanRoomGameFactory.CreateGame(activity);

        // Not a copy of the rule: the number the hut outcome itself sizes its
        // horde by. Remove the Math.Max(2, ...) floor and the first two cases
        // drop to 1 and this fails.
        var horde = MovementFunctions.BarbarianHordeSize(game);

        Assert.Equal(expected, horde);
        Assert.NotEqual(1, horde);
    }
}
