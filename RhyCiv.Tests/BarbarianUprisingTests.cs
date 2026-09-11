using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Constants;
using Model.Core;

namespace RhyCiv.Tests;

/// <summary>
/// Barbarians who were not in a village.
/// <para>
/// Every barbarian in the game came out of a goody hut, so a civilisation that
/// had cleared the huts near it was never troubled again and the Barbarity
/// question in the new-game dialog chose between four levels of something that
/// only happened when you went looking for it. Reported as "barbarians never
/// show up out of the blue from undeveloped lands" (#86).
/// </para>
/// </summary>
public class BarbarianUprisingTests
{
    [Fact]
    public void OnVillagesOnly_NobodyRises()
    {
        var (game, player) = AGameWithACity(BarbarianActivityType.VillagesOnly);
        var before = BarbarianCount(game);

        PlayTurns(game, 200);

        Assert.Equal(before, BarbarianCount(game));
        Assert.Empty(player.Uprisings);
    }

    [Fact]
    public void OnRagingHordes_RaidersAppear()
    {
        var (game, player) = AGameWithACity(BarbarianActivityType.RagingHordes);

        PlayTurns(game, 200);

        Assert.NotEmpty(player.Uprisings);
        Assert.True(BarbarianCount(game) > 0);
    }

    [Fact]
    public void TheEarlyGame_IsLeftAlone()
    {
        // A size-one city with one warrior in it has no answer to a horde, and
        // losing to one before the game has started is a wasted session rather
        // than a difficulty setting.
        var (game, player) = AGameWithACity(BarbarianActivityType.RagingHordes);

        PlayTurns(game, 20);

        Assert.Empty(player.Uprisings);
    }

    [Fact]
    public void RaidersAppearOutsideACity_NotOnTopOfOne()
    {
        var (game, player) = AGameWithACity(BarbarianActivityType.RagingHordes);

        PlayTurns(game, 200);

        Assert.NotEmpty(player.Uprisings);
        foreach (var (where, _) in player.Uprisings)
        {
            Assert.Null(where.CityHere);
        }
    }

    [Fact]
    public void RaidersAreGivenATurnBeforeTheyMove()
    {
        // They have crossed the country to get here. Arriving with their moves
        // already spent is what gives the city a turn to prepare.
        var (game, player) = AGameWithACity(BarbarianActivityType.RagingHordes);

        PlayTurns(game, 200);
        Assert.NotEmpty(player.Uprisings);

        var barbarians = game.AllCivilizations.Single(c => c.PlayerType == PlayerType.Barbarians);
        Assert.NotEmpty(barbarians.Units);
    }

    private static (Game Game, MockPlayer Player) AGameWithACity(BarbarianActivityType activity)
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame(activity);
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        CityActions.BuildCity(game.GetPlayerCiv.Units.First(unit => !unit.Dead), game, "Threatened");
        return (game, player);
    }

    /// <summary>
    /// Runs the world-level part of a turn, which is where an uprising happens.
    /// Deliberately not the whole turn: what the computer players do with the
    /// raiders afterwards is not what these are about, and running them makes the
    /// result depend on it.
    /// </summary>
    private static void PlayTurns(Game game, int turns)
    {
        for (var turn = 0; turn < turns; turn++)
        {
            game.StartNextTurnCore();
        }
    }

    private static int BarbarianCount(Game game) =>
        game.AllCivilizations.Where(c => c.PlayerType == PlayerType.Barbarians)
            .Sum(c => c.Units.Count(u => !u.Dead));
}
