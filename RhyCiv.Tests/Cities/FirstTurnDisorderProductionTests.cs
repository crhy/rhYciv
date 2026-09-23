using RhyCiv.Engine;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core.Cities;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// A city founded directly into civil disorder still banks the production of its
/// first turn; the riot stops production from the turn after that.
/// <para>
/// The founding turn used to be lost wholesale: the disorder check in
/// <c>GameTurn.CitiesTurn</c> skipped the rest of the city's turn -- shields
/// included -- before anything was banked, so a settlement put down on a tile
/// that was already rioting produced nothing at all on its first pass and never
/// made that turn's shields up (#166).
/// </para>
/// </summary>
public class FirstTurnDisorderProductionTests
{
    [Fact]
    public void ACityFoundedIntoDisorder_BanksItsFirstTurnsShields()
    {
        var (city, game, player) = FoundedCityInDisorder();

        Assert.True(city.CalculateHappiness(game).IsInDisorder,
            "the city under test is not actually in disorder");

        game.CitiesTurn(player);

        Assert.True(city.ShieldsProgress > 0,
            "the founding turn's production was lost to the disorder");
        Assert.True(city.CivilDisorder);
    }

    [Fact]
    public void TheTurnAfterTheFoundingTurn_StopsProducingAsUsual()
    {
        var (city, game, player) = FoundedCityInDisorder();

        game.CitiesTurn(player);
        var afterFoundingTurn = city.ShieldsProgress;
        Assert.True(afterFoundingTurn > 0);

        game.CitiesTurn(player);

        Assert.True(city.CivilDisorder);
        Assert.Equal(afterFoundingTurn, city.ShieldsProgress);
    }

    /// <summary>
    /// Founds a city, then makes it enormous without giving it food to match: a
    /// size well past any born-content threshold riots on the spot, and a full
    /// store keeps the starvation branch from quietly shrinking it back before
    /// the happiness pass runs.
    /// </summary>
    private static (City City, Game Game, MockPlayer Player) FoundedCityInDisorder()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var civ = game.GetPlayerCiv;

        var city = CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Rioting");
        city.Size = 10;
        city.FoodInStorage = 999;
        city.Production = 3;

        return (city, game, player);
    }
}
