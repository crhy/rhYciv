using RhyCiv.Engine.Diplomacy;
using RhyCiv.Tests.Mocks;
using Model.Core;
using Model.Core.Player;

namespace RhyCiv.Tests.Diplomacy;

/// <summary>
/// Meeting, talking, and going back on your word.
/// <para>
/// The data for all of this -- contact, cease-fire, peace, alliance, embassy,
/// reputation, attitude -- was carried in every save and read by nothing.
/// Civilisations met by walking into each other and fought until one was gone.
/// </para>
/// </summary>
public class DiplomacyTests
{
    [Fact]
    public void MeetingSomebodyIsToldToBothSidesAndOnlyOnce()
    {
        var (game, players) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        Assert.True(DiplomacyFunctions.MakeContact(game, us, them));
        Assert.False(DiplomacyFunctions.MakeContact(game, us, them));

        Assert.Single(players[0].Met);
        Assert.Single(players[1].Met);
        Assert.True(DiplomacyFunctions.HaveMet(them, us));
    }

    [Fact]
    public void ATreatyReplacesAWar()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        DiplomacyFunctions.DeclareWar(game, us, them);
        Assert.True(DiplomacyFunctions.AtWar(them, us));

        DiplomacyFunctions.AgreeCeaseFire(us, them);
        Assert.False(DiplomacyFunctions.AtWar(them, us));
        Assert.True(DiplomacyFunctions.UnderTreaty(them, us));
    }

    [Fact]
    public void BreakingATreatyCostsTwoBlackMarksAndTheGoodwillOfOnlookers()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        var onlooker = game.AllCivilizations[2];

        DiplomacyFunctions.MakeContact(game, us, them);
        DiplomacyFunctions.MakeContact(game, us, onlooker);
        DiplomacyFunctions.AgreePeace(us, them);

        var opinionBefore = DiplomacyFunctions.Attitude(onlooker, us);
        DiplomacyFunctions.DeclareWar(game, us, them);

        Assert.Equal(DiplomacyFunctions.BlackMarksPerBetrayal, DiplomacyFunctions.BlackMarks(us));
        Assert.True(DiplomacyFunctions.Attitude(onlooker, us) < opinionBefore,
            "A civilisation that watched a treaty broken should think less of the civilisation that broke it.");
    }

    [Fact]
    public void DeclaringWarWhereThereWasNoTreatyIsNotABetrayal()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        DiplomacyFunctions.MakeContact(game, us, them);

        DiplomacyFunctions.DeclareWar(game, us, them);

        Assert.Equal(0, DiplomacyFunctions.BlackMarks(us));
    }

    [Fact]
    public void ACivilisationThatThinksWellOfYouWillNotBreakItsWord()
    {
        var (game, _) = World();
        var them = game.AllCivilizations[1];
        var us = game.AllCivilizations[0];
        DiplomacyFunctions.AgreePeace(us, them);
        DiplomacyFunctions.AdjustAttitude(them, us, 40);

        for (var turn = 0; turn < 50; turn++)
        {
            Assert.False(DiplomacyFunctions.WouldBreakTreaty(game, them, us));
        }
    }

    [Fact]
    public void AHostileNeighbourWithTheUpperHandEventuallyDoes()
    {
        var (game, _) = World();
        var them = game.AllCivilizations[1];
        var us = game.AllCivilizations[0];
        DiplomacyFunctions.AgreePeace(us, them);
        DiplomacyFunctions.AdjustAttitude(them, us, -45);

        // Twice our strength, and no longer fond of us.
        them.PowerRating.Add(200);
        us.PowerRating.Add(100);

        var broke = false;
        for (var turn = 0; turn < 100 && !broke; turn++)
        {
            broke = DiplomacyFunctions.WouldBreakTreaty(game, them, us);
        }

        Assert.True(broke, "A hostile neighbour twice our strength never once considered attacking.");
    }

    [Fact]
    public void ACivilisationLosingBadlyWillTakeACeaseFireItWouldOtherwiseRefuse()
    {
        var (game, _) = World();
        var them = game.AllCivilizations[1];
        var us = game.AllCivilizations[0];
        DiplomacyFunctions.DeclareWar(game, us, them);
        DiplomacyFunctions.AdjustAttitude(them, us, -40);

        them.PowerRating.Add(30);
        us.PowerRating.Add(300);

        Assert.True(DiplomacyFunctions.WouldAccept(game, us, them, DiplomacyFunctions.Proposal.CeaseFire));
        Assert.False(DiplomacyFunctions.WouldAccept(game, us, them, DiplomacyFunctions.Proposal.Alliance));
    }

    [Fact]
    public void AnEmbassyIsOneWay()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        DiplomacyFunctions.EstablishEmbassy(us, them);

        Assert.True(DiplomacyFunctions.HasEmbassyWith(us, them));
        Assert.False(DiplomacyFunctions.HasEmbassyWith(them, us));
        Assert.True(DiplomacyFunctions.HaveMet(us, them));
    }

    [Fact]
    public void ABlackMarkFadesWithTime()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        us.Betrayals = 2;

        game.Turn = 24;
        DiplomacyFunctions.FadeReputations(game);
        Assert.Equal(1, DiplomacyFunctions.BlackMarks(us));

        game.Turn = 25;
        DiplomacyFunctions.FadeReputations(game);
        Assert.Equal(1, DiplomacyFunctions.BlackMarks(us));
    }

    private static (MockGame Game, MockPlayer[] Players) World()
    {
        var civilizations = new List<Civilization>
        {
            new() { Id = 0, TribeName = "Americans", Adjective = "American", Alive = true },
            new() { Id = 1, TribeName = "Romans", Adjective = "Roman", Alive = true },
            new() { Id = 2, TribeName = "Greeks", Adjective = "Greek", Alive = true }
        };

        var players = civilizations.Select(civ => new MockPlayer(civ)).ToArray();
        return (new MockGame
        {
            AllCivilizations = civilizations,
            Players = players.Cast<IPlayer>().ToArray(),
            Difficulty = 1
        }, players);
    }
}
