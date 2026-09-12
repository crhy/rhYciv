using RhyCiv.Engine;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Units;
using Model.Constants;

namespace RhyCiv.Tests.UnitActions;

/// <summary>
/// A Diplomat sent to a city that nobody is defending.
/// <para>
/// Whether a move was an attack or a move was decided on the units standing on
/// the destination alone. An undefended city has none, so a Diplomat walking
/// into one took the ordinary move — and taking a city of size one destroys it.
/// The player was never offered the price. Reported as "empty city population of
/// 1, diplomat wiped the city out, did not have option to buy".
/// </para>
/// </summary>
public class DiplomatAtAnEmptyCityTests
{
    [Fact]
    public void ADiplomatHasBusinessAtAnUndefendedEnemyCity()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        var mine = game.GetPlayerCiv;

        var theirs = game.AllCivilizations.First(civ => civ != mine && civ.Id != 0);
        var city = CityActions.BuildCity(theirs.Units.First(u => !u.Dead), game, "Undefended");
        foreach (var unit in city.Location.UnitsHere.ToList())
        {
            unit.Dead = true;
        }
        city.Location.UnitsHere.Clear();

        var diplomat = new Unit
        {
            Owner = mine,
            TypeDefinition = new UnitDefinition
            {
                Name = "Diplomat", AIrole = AiRoleType.Diplomacy, Attack = 0,
                Flags = Enumerable.Repeat(false, 13).ToArray()
            }
        };

        Assert.Empty(city.Location.UnitsHere);
        Assert.True(DiplomatActions.HasTarget(diplomat, city.Location),
            "a Diplomat at an undefended enemy city should have something to offer");
    }

    [Fact]
    public void ASizeOneCityCanStillBeIncited()
    {
        // The price is what the player was never shown. A city of one is the
        // cheapest there is, not one that cannot be bought.
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        var mine = game.GetPlayerCiv;

        var theirs = game.AllCivilizations.First(civ => civ != mine && civ.Id != 0);
        var city = CityActions.BuildCity(theirs.Units.First(u => !u.Dead), game, "Cheap");

        // The first city a civilisation founds is given the Palace, and a capital
        // cannot be incited in Civ II either. This is about an ordinary city.
        foreach (var capital in city.Improvements
                     .Where(i => i.Effects.ContainsKey(Effects.Capital)).ToList())
        {
            city.OrderedImprovements.Remove(capital.Type);
        }

        Assert.Equal(1, city.Size);
        Assert.True(DiplomatActions.CanIncite(city), "a size-one city should be incitable");
        Assert.True(DiplomatActions.InciteCost(game, city) > 0);
    }
}
