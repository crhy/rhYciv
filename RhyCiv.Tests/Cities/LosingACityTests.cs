using RhyCiv.Engine;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// What a civilisation's map shows after it loses a city.
/// <para>
/// A player sees their remembered record of a square, not the square, and that
/// record is only refreshed for civilisations that can currently see it. A city
/// stops being visible to its owner at the very moment they stop owning it --
/// the garrison is gone and the city belongs to somebody else, so nothing of
/// theirs is in sight of it any more. So the one civilisation guaranteed to have
/// the square on its map was the one guaranteed never to be told it had changed,
/// and the city went on being drawn in their own colours for the rest of the
/// game. Reported as barbarians taking a city and the city not turning red.
/// </para>
/// </summary>
public class LosingACityTests
{
    [Fact]
    public void ACityTakenFromYou_IsRedrawnInItsNewOwnersColours()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var mine = game.GetPlayerCiv;

        var city = CityActions.BuildCity(mine.Units.First(unit => !unit.Dead), game, "Doomed");
        var tile = city.Location;
        tile.UpdatePlayer(mine.Id);

        Assert.Equal(mine.Id, tile.PlayerKnowledge![mine.Id]!.CityHere!.OwnerId);

        // Taken. This is what the capture path does to the city and its square.
        var taker = game.AllCivilizations.First(civ => civ != mine && civ.Id != 0);
        mine.Cities.Remove(city);
        city.Owner = taker;
        taker.Cities.Add(city);
        game.UpdateTilesFor([tile], mine.Id);

        Assert.Equal(taker.Id, tile.PlayerKnowledge![mine.Id]!.CityHere!.OwnerId);
    }

    [Fact]
    public void TheRefresh_ReachesACivilisationThatCannotSeeTheSquare()
    {
        // The whole point: it must not be conditional on visibility, because the
        // civilisation this is for cannot see the square by definition.
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var mine = game.GetPlayerCiv;

        // Somewhere with nothing of ours anywhere near it.
        var map = game.Maps[0];
        var far = map.Tile[map.XDim / 2, map.YDim - 2];
        Assert.False(map.IsCurrentlyVisible(far, mine.Id));

        game.UpdateTilesFor([far], mine.Id);

        Assert.NotNull(far.PlayerKnowledge);
        Assert.NotNull(far.PlayerKnowledge![mine.Id]);
    }
}
