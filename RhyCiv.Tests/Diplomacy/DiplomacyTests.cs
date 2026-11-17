using RhyCiv.Engine;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Scripting.ScriptObjects;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Player;
using Moq;

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

    [Fact]
    public void AMapTradeIsOfferedOnlyWhenBothCourtsHoldGroundTheOtherLacks()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        var map = game.Maps[0];

        var offered = () => DiplomacyFunctions.AvailableProposals(game, us, them)
            .Contains(DiplomacyFunctions.Proposal.ShareWorldMaps);

        // Neither side has recorded anything: two blank pictures are one picture.
        Assert.False(offered());

        // One court remembers a city the other has never heard of. There is a gift
        // to be made, but only one way, and Civ II asks to share, not to hand over.
        Remember(map.Tile[0, 0], us.Id, ACityNamed("Doomed"));
        Assert.False(offered(),
            "One side has nothing to give, so there is no trade to offer.");

        Remember(map.Tile[1, 0], them.Id, ACityNamed("Hidden"));
        Assert.True(offered(),
            "Each court holds a square the other has never seen.");
    }

    [Fact]
    public void SharingMapsLeavesBothSidesKnowingWhatEitherSaw()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        var map = game.Maps[0];

        // Out of date -- nothing is really standing on these squares -- but the only
        // picture either court has of that ground, which is what makes it worth
        // trading for.
        Remember(map.Tile[0, 0], us.Id, ACityNamed("Doomed"));
        Remember(map.Tile[1, 0], them.Id, ACityNamed("Hidden"));

        var changed = DiplomacyFunctions.ShareWorldMaps(game, us, them);

        Assert.Equal(2, changed);
        Assert.Equal("Doomed", Recorded(map.Tile[0, 0], them.Id)!.CityHere!.Name);
        Assert.Equal("Hidden", Recorded(map.Tile[1, 0], us.Id)!.CityHere!.Name);

        Assert.False(ReferenceEquals(Recorded(map.Tile[0, 0], us.Id), Recorded(map.Tile[0, 0], them.Id)),
            "Two civilisations sharing one record would go on editing each other's " +
            "memory every time either was told about the ground again.");
    }

    [Fact]
    public void AStaleMemoryNeverOverwritesOneThatIsCurrent()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        var square = game.Maps[0].Tile[0, 0];

        // us has just been shown the square as it is now; theirs remembers a city
        // that is not there.
        square.UpdatePlayer(us.Id);
        Remember(square, them.Id, ACityNamed("Ghost"));

        Assert.Equal(1, DiplomacyFunctions.ShareWorldMaps(game, us, them));

        Assert.True(Recorded(square, us.Id)!.CityHere == null,
            "A civilisation that has just been shown the square must not be handed " +
            "another's stale memory of it.");
        Assert.True(Recorded(square, them.Id)!.CityHere == null,
            "The stale record should be corrected by the current one.");
    }

    [Fact]
    public void AnAccurateRecordOfAnEmptySquareIsNotTradedForABlank()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        var square = game.Maps[0].Tile[0, 1];

        // us has been shown bare ground; theirs has never heard of the place. Both
        // beliefs are the same belief, and counting it as a gift would make every
        // unexplored ocean an offer worth trading for.
        square.UpdatePlayer(us.Id);

        Assert.Equal(0, DiplomacyFunctions.ShareWorldMaps(game, us, them));
        Assert.True(Recorded(square, them.Id) == null,
            "An accurate record of bare ground is not a gift worth trading for a blank.");
    }

    [Fact]
    public void ACityKnownToOneOfThePairBecomesKnownToBothAndNobodyElseMoves()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];
        var bystander = game.AllCivilizations[2];

        var city = new City
        {
            Owner = us, WhoBuiltIt = us, Name = "Capital", Size = 1,
            WhoKnowsAboutIt = [true, false, true]
        };
        us.Cities.Add(city);

        Assert.Equal(1, DiplomacyFunctions.ShareWorldMaps(game, us, them));

        Assert.True(city.WhoKnowsAboutIt[us.Id]);
        Assert.True(city.WhoKnowsAboutIt[them.Id],
            "A city one of the pair knows about should be on the other's map afterwards.");
        Assert.True(city.WhoKnowsAboutIt[bystander.Id],
            "A third civilisation's memory of the place is none of this pair's business.");
    }

    [Fact]
    public void ACityNeitherOfThePairKnowsAboutStaysOffBothMaps()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        var city = new City { Owner = game.AllCivilizations[2], WhoBuiltIt = game.AllCivilizations[2],
            Name = "Hidden", Size = 1, WhoKnowsAboutIt = [false, false, true] };
        game.AllCivilizations[2].Cities.Add(city);

        Assert.Equal(0, DiplomacyFunctions.ShareWorldMaps(game, us, them));
        Assert.False(city.WhoKnowsAboutIt[us.Id]);
        Assert.False(city.WhoKnowsAboutIt[them.Id]);
    }

    [Fact]
    public void AMapTradeIsCheaperThanAnAllianceAndCostsWhatAPeaceTreatyCosts()
    {
        var (game, _) = World();
        var us = game.AllCivilizations[0];
        var them = game.AllCivilizations[1];

        // Neutral is 50 and a map trade asks for what a peace treaty asks for, so
        // a court one notch cooler than neutral turns it down and one exactly there
        // takes it.
        DiplomacyFunctions.AdjustAttitude(them, us, -10);
        Assert.False(DiplomacyFunctions.WouldAccept(game, us, them,
            DiplomacyFunctions.Proposal.ShareWorldMaps),
            "A cold leader should refuse, and say so, rather than hand over their map.");

        DiplomacyFunctions.AdjustAttitude(them, us, 5);
        Assert.True(DiplomacyFunctions.WouldAccept(game, us, them,
            DiplomacyFunctions.Proposal.ShareWorldMaps));
        Assert.False(DiplomacyFunctions.WouldAccept(game, us, them,
            DiplomacyFunctions.Proposal.Alliance),
            "Ground already walked is a cheaper ask than an alliance.");
    }

    [Fact]
    public void AComputerCivilisationTakesTheMapsAndRemembersTheGoodwill()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var us = game.AllCivilizations.First(civ => civ.PlayerType == PlayerType.Local);
        var them = game.AllCivilizations.First(civ => civ != us && civ.Alive &&
                                                civ.PlayerType != PlayerType.Barbarians);
        var ai = new AiPlayer(0, them, null, game, new Mock<AiInterface>(null, them, 0, null).Object);

        // A city us has been shown, and an old memory of the same ground at theirs.
        var map = game.Maps[0];
        var square = map.Tile[2, 2];
        var city = new City
        {
            Owner = us, WhoBuiltIt = us, Name = "Farce", Size = 1,
            Location = square, X = square.X, Y = square.Y, MapIndex = square.Z
        };
        square.CityHere = city;
        us.Cities.Add(city);
        game.AllCities.Add(city);

        square.UpdatePlayer(us.Id);
        Remember(square, them.Id, new PlayerTile());

        var opinionBefore = DiplomacyFunctions.Attitude(them, us);
        ai.ProposalReceived(us, new DiplomaticProposal
        {
            Kind = DiplomacyProposals.ShareWorldMaps
        });

        Assert.True("Farce" == Recorded(square, them.Id)!.CityHere!.Name,
            "The computer civilisation should leave the meeting holding the other " +
            "side's picture of the ground.");
        Assert.True(DiplomacyFunctions.Attitude(them, us) > opinionBefore,
            "A civilisation that has been offered a map trade should think better of the proposer.");
    }

    /// <summary>
    /// Files what one civilisation remembers of a square, growing the board's record
    /// of it when this is the first civilisation to be told about that ground.
    /// </summary>
    private static void Remember(Tile tile, int civilizationId, PlayerTile what)
    {
        var knowledge = tile.PlayerKnowledge;
        if (knowledge == null || knowledge.Length <= civilizationId)
        {
            var grown = new PlayerTile?[civilizationId + 1];
            if (knowledge != null)
            {
                Array.Copy(knowledge, grown, knowledge.Length);
            }

            tile.PlayerKnowledge = grown;
        }

        tile.PlayerKnowledge![civilizationId] = what;
    }

    /// <summary>A civilisation's memory of a city standing on a square.</summary>
    private static PlayerTile ACityNamed(string name) => new()
    {
        CityHere = new CityInfo { Name = name, Size = 1, OwnerId = 0 }
    };

    /// <summary>What one civilisation currently holds about a square, if anything.</summary>
    private static PlayerTile? Recorded(Tile tile, int civilizationId) =>
        tile.PlayerKnowledge != null && civilizationId < tile.PlayerKnowledge.Length
            ? tile.PlayerKnowledge[civilizationId]
            : null;

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
            Difficulty = 1,
            Maps = [Board(civilizations.Count)]
        }, players);
    }

    /// <summary>
    /// A board of empty ground: four squares, enough to hold a map trade without
    /// the test walking a whole world.
    /// </summary>
    private static Map Board(int civs)
    {
        var map = new Map(false, 0) { XDim = 2, YDim = 2 };
        map.Tile = new Tile[2, 2];
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                map.Tile[x, y] = new Tile(x, y,
                    new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>() },
                    0, map, x + y * 2, new bool[civs]);
            }
        }

        return map;
    }
}
