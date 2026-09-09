using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using Model.Constants;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Units;

namespace RhyCiv.Tests.UnitActions;

/// <summary>
/// Taking an advance out of somebody else's city.
/// <para>
/// A city can only be robbed once, as in Civ II -- otherwise a rival capital is an
/// endless supply of technology to anybody willing to keep building Diplomats.
/// These cover which advances are on offer and the once-only rule; what the theft
/// costs the agent (a Diplomat its life, a Spy a move) needs a complete ruleset to
/// reach, because granting an advance rebuilds every production list, so it is
/// checked in the game rather than here.
/// </para>
/// </summary>
public class TechnologyTheftTests
{
    private const int Alphabet = 0;
    private const int Writing = 1;
    private const int Pottery = 2;

    [Fact]
    public void OnlyWhatTheOwnerKnowsAndTheThiefDoesNotIsOnOffer()
    {
        var (game, thief, city) = Board();

        // The city's owner knows Writing and Pottery; the thief already has Pottery.
        Assert.Equal(new[] { Writing },
            DiplomatActions.StealableAdvances(game, thief, city).Select(a => a.Index));
    }

    [Fact]
    public void ThereIsNothingToStealFromYourOwnCity()
    {
        var (game, thief, _) = Board();
        var ownCity = new City { Name = "Washington", Owner = thief.Owner };

        Assert.Empty(DiplomatActions.StealableAdvances(game, thief, ownCity));
    }

    [Fact]
    public void ACityThatHasBeenRobbedIsClosed()
    {
        var (_, _, city) = Board();

        Assert.True(DiplomatActions.CanStealFrom(city));
        city.TechnologyStolen = true;
        Assert.False(DiplomatActions.CanStealFrom(city));
    }

    [Fact]
    public void ASecondAgentSentToARobbedCityIsNotSpent()
    {
        var (game, thief, city) = Board();
        city.TechnologyStolen = true;

        Assert.False(DiplomatActions.StealTechnology(game, thief, city, Writing));
        Assert.False(thief.Dead);
    }

    [Fact]
    public void AnAdvanceTheThiefAlreadyHasCannotBeTaken()
    {
        var (game, thief, city) = Board();

        Assert.False(DiplomatActions.StealTechnology(game, thief, city, Pottery));
        Assert.False(thief.Dead);
        Assert.False(city.TechnologyStolen);
    }

    [Fact]
    public void AnAdvanceTheOwnerDoesNotHaveCannotBeTaken()
    {
        var (game, thief, city) = Board();

        Assert.False(DiplomatActions.StealTechnology(game, thief, city, Alphabet));
        Assert.False(city.TechnologyStolen);
    }

    [Fact]
    public void ASpyIsRecognisedAsOne()
    {
        var (_, thief, _) = Board();
        var spy = Agent(thief.Owner, spy: true);

        Assert.False(DiplomatActions.IsSpy(thief));
        Assert.True(DiplomatActions.IsSpy(spy));
        Assert.True(DiplomatActions.IsDiplomat(spy));
    }

    private static (MockGame Game, Unit Thief, City City) Board()
    {
        var rules = new Rules
        {
            Advances =
            [
                new Advance { Index = Alphabet, Name = "Alphabet", Prereq1 = -1, Prereq2 = -1 },
                new Advance { Index = Writing, Name = "Writing", Prereq1 = -1, Prereq2 = -1 },
                new Advance { Index = Pottery, Name = "Pottery", Prereq1 = -1, Prereq2 = -1 },
            ],
        };

        var thiefCiv = new Civilization
        {
            Id = 0,
            TribeName = "Americans",
            PlayerType = PlayerType.Local,
            Advances = [false, false, true],
            AllowedAdvanceGroups = [AdvanceGroupAccess.CanResearch],
        };
        var victimCiv = new Civilization
        {
            Id = 1,
            TribeName = "Zulus",
            PlayerType = PlayerType.Ai,
            Advances = [false, true, true],
            AllowedAdvanceGroups = [AdvanceGroupAccess.CanResearch],
        };

        var game = new MockGame
        {
            Rules = rules,
            AllCivilizations = [thiefCiv, victimCiv],
            Players = [new MockPlayer(thiefCiv), new MockPlayer(victimCiv)],
        };

        return (game, Agent(thiefCiv, spy: false), new City { Name = "Ulundi", Owner = victimCiv, Size = 4 });
    }

    private static Unit Agent(Civilization owner, bool spy) => new()
    {
        Owner = owner,
        TypeDefinition = new UnitDefinition
        {
            AIrole = AiRoleType.Diplomacy,
            Type = spy ? (int)RhyCiv.Engine.Enums.UnitType.Spy : (int)RhyCiv.Engine.Enums.UnitType.Diplomat,
            Move = 2,
            Flags = Enumerable.Repeat(false, 13).ToArray(),
        },
    };
}
