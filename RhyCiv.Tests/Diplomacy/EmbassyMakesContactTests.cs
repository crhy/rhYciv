using RhyCiv.Engine;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Constants;
using Model.Core.Units;

namespace RhyCiv.Tests.Diplomacy;

/// <summary>
/// Sending a Diplomat to open an embassy counts as meeting the civilisation.
/// <para>
/// The embassy set both Contact flags directly and never went through
/// <see cref="DiplomacyFunctions.MakeContact"/>, so the two were at peace with an
/// embassy between them and neither player had been told they met: no herald, no
/// first-contact negotiation, nothing in the record (#149, #140).
/// </para>
/// </summary>
public class EmbassyMakesContactTests
{
    [Fact]
    public void OpeningAnEmbassy_TellsBothSidesTheyHaveMet()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var mine = game.GetPlayerCiv;
        var player = new MockPlayer(mine);
        game.ConnectPlayer(player);

        var theirs = game.AllCivilizations.First(civ => civ != mine && civ.Id != 0);
        var city = CityActions.BuildCity(theirs.Units.First(u => !u.Dead), game, "Host");
        foreach (var unit in city.Location.UnitsHere.ToList())
        {
            if (unit.Owner == theirs)
            {
                unit.Dead = true;
            }
        }

        var diplomat = new Unit
        {
            Owner = mine,
            TypeDefinition = new UnitDefinition
            {
                Name = "Diplomat", AIrole = AiRoleType.Diplomacy, Attack = 0,
                Flags = Enumerable.Repeat(false, 13).ToArray()
            }
        };

        Assert.False(DiplomacyFunctions.HaveMet(mine, theirs));

        Assert.True(DiplomatActions.EstablishEmbassy(game, diplomat, city),
            "a Diplomat at an unmet enemy city should be able to open an embassy");

        Assert.True(DiplomacyFunctions.HaveMet(mine, theirs));
        Assert.True(DiplomacyFunctions.HaveMet(theirs, mine));
        Assert.Contains(theirs, player.Met);
    }
}
