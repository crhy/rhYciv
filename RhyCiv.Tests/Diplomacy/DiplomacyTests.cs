using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Advances;
using Model.Core.GameRules;
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

        DiplomacyFunctions.AgreeCeaseFire(game, us, them);
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

    [Fact]
    public void ACeaseFireExpiresAfterItsTurnsRunOut()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        game.Turn = 0;
        DiplomacyFunctions.AgreeCeaseFire(game, us, them);

        for (var turn = 1; turn < DiplomacyFunctions.CeaseFireDuration; turn++)
        {
            game.Turn = turn;
            DiplomacyFunctions.ExpireCeaseFires(game);

            Assert.True(DiplomacyFunctions.UnderTreaty(us, them),
                "A cease-fire should still hold before its turns have run out.");
            Assert.True(DiplomacyFunctions.UnderTreaty(them, us),
                "The cease-fire should still hold for both sides before its turns have run out.");
        }

        game.Turn = DiplomacyFunctions.CeaseFireDuration;
        DiplomacyFunctions.ExpireCeaseFires(game);

        Assert.False(DiplomacyFunctions.UnderTreaty(us, them));
        Assert.False(DiplomacyFunctions.UnderTreaty(them, us));
    }

    [Fact]
    public void TributeExtendsAExistingCeaseFire()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        game.Turn = 0;
        DiplomacyFunctions.AgreeCeaseFire(game, us, them);

        var renewed = DiplomacyFunctions.CeaseFireDuration - 1;
        game.Turn = renewed;
        DiplomacyFunctions.RenewCeaseFire(game, us, them);

        game.Turn = DiplomacyFunctions.CeaseFireDuration;
        DiplomacyFunctions.ExpireCeaseFires(game);

        Assert.True(DiplomacyFunctions.UnderTreaty(us, them),
                "Tribute should extend a cease-fire past its original end.");
        Assert.True(DiplomacyFunctions.UnderTreaty(them, us),
                "Tribute should extend the cease-fire for both sides.");

        game.Turn = renewed + DiplomacyFunctions.CeaseFireDuration;
        DiplomacyFunctions.ExpireCeaseFires(game);

        Assert.False(DiplomacyFunctions.UnderTreaty(us, them));
        Assert.False(DiplomacyFunctions.UnderTreaty(them, us));
    }

    [Fact]
    public void ACeaseFireReadBackFromASaveStillExpires()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        // A save keeps that there is a cease-fire but not when it runs out.
        game.Turn = 0;
        DiplomacyFunctions.AgreeCeaseFire(game, us, them);
        us.Relations[them.Id]!.CeaseFireTurn = -1;
        them.Relations[us.Id]!.CeaseFireTurn = -1;

        game.Turn = 40;
        DiplomacyFunctions.ExpireCeaseFires(game);
        Assert.True(DiplomacyFunctions.UnderTreaty(us, them),
            "A loaded cease-fire should start its count, not end at once.");

        game.Turn = 40 + DiplomacyFunctions.CeaseFireDuration;
        DiplomacyFunctions.ExpireCeaseFires(game);
        Assert.False(DiplomacyFunctions.UnderTreaty(us, them));
        Assert.False(DiplomacyFunctions.UnderTreaty(them, us));
    }

    [Fact]
    public void TributeDoesNotCreateACeaseFireWhereThereIsNone()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        DiplomacyFunctions.MakeContact(game, us, them);
        DiplomacyFunctions.RenewCeaseFire(game, us, them);

        Assert.False(DiplomacyFunctions.UnderTreaty(us, them));
        Assert.False(DiplomacyFunctions.UnderTreaty(them, us));
    }

    /// <summary>
    /// An exchange is raised only where both courts have something to give.
    /// <para>
    /// Civ II's parley offers a gift of knowledge and an exchange of knowledge as
    /// different things, and the difference matters: a trade that would move one
    /// way only is a gift, and the gift is already on the menu.
    /// </para>
    /// </summary>
    [Fact]
    public void TheExchangeIsOfferedOnlyWhenEachSideHoldsSomethingTheOtherLacks()
    {
        var (game, _) = World();
        game.Rules = new Rules
        {
            Advances =
            [
                new Advance { Index = 0, Name = "Pottery" },
                new Advance { Index = 1, Name = "Writing" },
                new Advance { Index = 2, Name = "Mathematics" }
            ]
        };

        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        us.Advances = new bool[3];
        them.Advances = new bool[3];

        var offered = () => DiplomacyFunctions.AvailableProposals(game, us, them)
            .Contains(DiplomacyFunctions.Proposal.ExchangeKnowledge);

        // Neither court knows anything: there is nothing to trade.
        Assert.False(offered());

        // One court holds Pottery and the other nothing. That is a gift, and a gift
        // is already on the menu; an exchange here would promise the player something
        // the other side cannot give back.
        us.Advances[0] = true;
        Assert.False(offered());

        // Both hold Pottery: still nothing to move.
        them.Advances[0] = true;
        Assert.False(offered());

        // The other side holds Writing as well, but has nothing of its own to give
        // back for it -- the trade would still run one way.
        them.Advances[1] = true;
        Assert.False(offered());

        // Now each holds something the other has never learned.
        us.Advances[2] = true;
        Assert.True(offered());
    }

    /// <summary>
    /// The trade itself: both courts end up knowing everything either knew, and
    /// an advance they already share is not "given" at all.
    /// </summary>
    [Fact]
    public void AnExchangeLeavesBothSidesHoldingTheUnionOfWhatEitherKnew()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var us = game.GetPlayerCiv;
        var them = game.AllCivilizations.First(civ => civ.PlayerType == PlayerType.Ai);

        // Two advances apiece, one of which they both already hold: the shared
        // one has nothing to do with the trade, and re-giving it would re-run
        // every effect it carries.
        var shared = game.Rules.Advances[0].Index;
        var ours = game.Rules.Advances[1].Index;
        var theirs = game.Rules.Advances[2].Index;

        Knows(game, us, shared);
        Knows(game, us, ours);
        Knows(game, them, shared);
        Knows(game, them, theirs);

        var exchanged = DiplomacyFunctions.ExchangeKnowledge(game, us, them);

        // Only what changed hands, and in ascending advance-index order.
        Assert.Equal(new List<int> { ours, theirs }, exchanged);

        foreach (var index in new List<int> { shared, ours, theirs })
        {
            Assert.True(AdvanceFunctions.HasTech(us, index),
                $"Both courts should hold advance {index} after the exchange.");
            Assert.True(AdvanceFunctions.HasTech(them, index),
                $"Both courts should hold advance {index} after the exchange.");
        }

        // And the trade invented nothing: an advance neither side held is still
        // held by neither.
        foreach (var advance in game.Rules.Advances)
        {
            if (advance.Index == shared || advance.Index == ours || advance.Index == theirs)
            {
                continue;
            }

            Assert.False(AdvanceFunctions.HasTech(us, advance.Index),
                $"The exchange handed {us.TribeName} advance {advance.Index}, which neither side held.");
            Assert.False(AdvanceFunctions.HasTech(them, advance.Index),
                $"The exchange handed {them.TribeName} advance {advance.Index}, which neither side held.");
        }
    }

    /// <summary>
    /// A court weighs an exchange by how it feels about the proposer, and an
    /// exchange asks less of a friendship than a treaty does: it costs the
    /// receiver nothing and leaves them knowing more.
    /// </summary>
    [Fact]
    public void AHostileCourtRefusesAnExchangeThatANeutralOneTakes()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        // Neutral: the middle of Civ II's hundred-point scale, where a court has
        // formed no opinion.
        Assert.True(DiplomacyFunctions.WouldAccept(game, us, them,
            DiplomacyFunctions.Proposal.ExchangeKnowledge));

        // Cold enough to be called Hostile, and the offer is turned down -- Civ
        // II's "we have no interest in exchanging knowledge at this time".
        DiplomacyFunctions.AdjustAttitude(them, us, -30);
        Assert.Equal("Hostile", DiplomacyFunctions.AttitudeName(them, us));
        Assert.False(DiplomacyFunctions.WouldAccept(game, us, them,
            DiplomacyFunctions.Proposal.ExchangeKnowledge));
    }

    /// <summary>
    /// A computer civilisation that accepts an exchange trades both ways: the
    /// receipt is where the trade happens, not merely where it is remembered.
    /// </summary>
    [Fact]
    public void AComputerCivilisationReceivingAnExchangeTradesBothWays()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var us = game.GetPlayerCiv;
        var them = game.AllCivilizations.First(civ => civ.PlayerType == PlayerType.Ai);

        var ours = game.Rules.Advances[0].Index;
        var theirs = game.Rules.Advances[1].Index;
        Knows(game, us, ours);
        Knows(game, them, theirs);

        // The script engine is not reached on this path, so it stays out of the
        // way of the test.
        var receiver = new AiPlayer(them.Id, them, null, game, null);
        receiver.ProposalReceived(us, new DiplomaticProposal
        {
            Kind = DiplomacyProposals.ExchangeKnowledge
        });

        Assert.True(AdvanceFunctions.HasTech(us, theirs),
            "The player should have taken what the computer civilisation knew.");
        Assert.True(AdvanceFunctions.HasTech(them, ours),
            "The computer civilisation should have taken what the player knew.");
    }

    /// <summary>
    /// Puts an advance into a civilisation's own list. A loaded game keeps that
    /// list only as long as the highest advance the civilisation holds -- a civilisation
    /// holding none has an empty array -- so it has to be grown before an index into
    /// it means anything, which is what <c>GiveAdvance</c> does when it grants one.
    /// </summary>
    private static void Knows(IGame game, Civilization civ, int advanceIndex)
    {
        if (civ.Advances.Length < game.Rules.Advances.Length)
        {
            var grown = new bool[game.Rules.Advances.Length];
            Array.Copy(civ.Advances, grown, civ.Advances.Length);
            civ.Advances = grown;
        }

        civ.Advances[advanceIndex] = true;
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
