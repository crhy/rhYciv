using RhyCiv.Engine;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core.Cities;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// A city put down on ordinary ground grows.
/// <para>
/// It did not, and the reason was two squares removed from anything about food.
/// A citizen is only put to work on a square the civilisation can see, and
/// founding a city revealed nothing: the engine marked no squares at all and the
/// UI command marked only the one the settler was standing on. So a new city
/// worked its own centre and nothing else -- two food produced, two food eaten,
/// no surplus -- and sat at size one until a unit happened to wander across its
/// fields.
/// </para>
/// <para>
/// In a played game that showed up as cities that crawled: a save from AD 1220,
/// turn 162, held thirty-four cities across the whole world and the largest was
/// size four (issue #13). These run a real game forward and watch the city, which
/// is the only way to catch a fault that lives in the join between three systems
/// that are each correct on their own.
/// </para>
/// </summary>
public class CityGrowthTests
{
    [Fact]
    public void ANewCity_CanSeeTheGroundItWorks()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        var civ = game.GetPlayerCiv;

        var city = CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Sighted");

        Assert.All(city.Location.CityRadius(),
            tile => Assert.True(tile.IsVisible(civ.Id),
                $"square {tile.X},{tile.Y} in the city's own radius is not visible to it"));
    }

    [Fact]
    public void ANewCity_PutsItsCitizenToWorkOnASquare()
    {
        // Size one means the centre and one worked square, so a city that comes out
        // of the ground working only its centre has lost a citizen's labour.
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var city = CityActions.BuildCity(game.GetPlayerCiv.Units.First(unit => !unit.Dead), game, "Working");

        Assert.Equal(2, city.WorkedTiles.Count);
        Assert.True(city.SurplusHunger > 0,
            $"a new city on ordinary ground should have a food surplus, not {city.SurplusHunger}");
    }

    [Fact]
    public void ACityLeftAlone_Grows()
    {
        var (city, _, _) = ACityAfter(30);

        Assert.True(city.Size > 1, $"after thirty turns the city is still size {city.Size}");
    }

    [Fact]
    public void AGrowingCity_PutsEachNewCitizenToWork()
    {
        // Growth that does not come with somewhere to work is growth that stops:
        // the next citizen eats without producing, the surplus falls, and the city
        // stalls a size or two above where it started.
        var (city, _, _) = ACityAfter(30);

        var specialists = city.NoOfSpecialistsx4 / 4;
        Assert.Equal(city.Size + 1 - specialists, city.WorkedTiles.Count);
    }

    /// <summary>
    /// Founds a city and runs the city half of the turn for it, the number of
    /// times asked.
    /// <para>
    /// Deliberately not the whole world's turn. Running every civilisation puts
    /// the result at the mercy of what the computer players and the barbarians do
    /// -- on one run in several the city under test was captured or starved for a
    /// settler, and the test failed for a reason that had nothing to do with what
    /// it was checking. This is the code path growth actually lives on, run
    /// directly, and it is the same every time.
    /// </para>
    /// </summary>
    private static (City City, Game Game, Model.Core.Civilization Civ) ACityAfter(int turns)
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var civ = game.GetPlayerCiv;
        var city = CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Growing");

        for (var turn = 0; turn < turns; turn++)
        {
            game.CitiesTurn(player);
        }

        return (city, game, civ);
    }
}
