using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Constants;
using Model.Core.Cities;
using Model.Core.GameRules;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// What BuildCity does when the capital improvement's prerequisite advance is
/// missing.
/// <para>
/// The rules parser records Prerequisite = -1 when an improvement names a
/// prerequisite advance that is not in the advance list (RulesParser.cs:511).
/// BuildCity used to feed that straight into Advances[i.Prerequisite], which
/// indexed Advances[-1] and threw System.IndexOutOfRangeException -- the crash
/// recorded in the two managed-exception logs. The guard added to the search
/// treats an out-of-range prerequisite as allowed, matching GetAdvanceGroupAccess's
/// own out-of-range fallback, so the capital improvement is still raised.
/// </para>
/// </summary>
public class CapitalImprovementPrerequisiteTests
{
    [Fact]
    public void ACapitalImprovementWithNoPrerequisiteAdvance_IsStillBuilt()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var settler = game.GetPlayerCiv.Units.First(unit => !unit.Dead);

        // The value the parser writes when a prerequisite advance name is missing.
        var palace = rules.Improvements.First(i => i.Effects.ContainsKey(Effects.Capital));
        palace.Prerequisite = -1;

        var city = CityActions.BuildCity(settler, game, "CrashTest");

        // The search path runs (the Palace is locked behind an advance the player
        // does not yet have, so FindByEffect returns null). With the guard the
        // improvement is treated as allowed and added rather than throwing.
        Assert.Contains(city.Improvements, i => i.Effects.ContainsKey(Effects.Capital));
    }
}
