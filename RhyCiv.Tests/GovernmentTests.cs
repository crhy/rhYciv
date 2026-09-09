using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Tests.Mocks;
using Model.Core;
using Model.Core.Advances;
using Model.Core.GameRules;

namespace RhyCiv.Tests;

/// <summary>
/// Changing how a civilisation is governed. None of this existed: a civilisation
/// was given Despotism when the game began and stayed under it however far it
/// researched, with the REVOLUTION menu entry drawn and doing nothing.
/// </summary>
public class GovernmentTests
{
    private const int MonarchyAdvance = (int)AdvanceType.Monarchy;
    private const int RepublicAdvance = (int)AdvanceType.Republic;

    [Fact]
    public void DespotismNeedsNothing()
    {
        Assert.True(GovernmentFunctions.CanForm(Civ(), GovernmentType.Despotism));
    }

    [Fact]
    public void AGovernmentNeedsItsAdvance()
    {
        var civ = Civ();

        Assert.False(GovernmentFunctions.CanForm(civ, GovernmentType.Monarchy));
        civ.Advances[MonarchyAdvance] = true;
        Assert.True(GovernmentFunctions.CanForm(civ, GovernmentType.Monarchy));
    }

    [Fact]
    public void AnarchyIsNeverSomethingToChooseFor()
    {
        Assert.False(GovernmentFunctions.CanForm(Civ(), GovernmentType.Anarchy));
        Assert.DoesNotContain(GovernmentType.Anarchy, GovernmentFunctions.AvailableGovernments(Civ()));
    }

    [Fact]
    public void TheCurrentGovernmentIsNotOffered()
    {
        var civ = Civ();

        // A revolution that changed nothing would still cost the turns of anarchy.
        Assert.DoesNotContain(GovernmentType.Despotism, GovernmentFunctions.AvailableGovernments(civ));
    }

    [Fact]
    public void ThereIsNothingToRevoltForUntilSomethingIsResearched()
    {
        var civ = Civ();

        Assert.False(GovernmentFunctions.CanRevolt(civ));
        civ.Advances[RepublicAdvance] = true;
        Assert.True(GovernmentFunctions.CanRevolt(civ));
    }

    [Fact]
    public void AdvancesAreMappedToTheGovernmentTheyOpen()
    {
        Assert.Equal(GovernmentType.Monarchy, GovernmentFunctions.GovernmentUnlockedBy(MonarchyAdvance));
        Assert.Equal(GovernmentType.Republic, GovernmentFunctions.GovernmentUnlockedBy(RepublicAdvance));
        Assert.Null(GovernmentFunctions.GovernmentUnlockedBy((int)AdvanceType.Pottery));
    }

    [Fact]
    public void ARevolutionGoesThroughAnarchyRatherThanStraightToTheNewOrder()
    {
        var (game, civ) = Board();
        civ.Advances[MonarchyAdvance] = true;

        GovernmentFunctions.BeginRevolution(game, civ);

        Assert.Equal((int)GovernmentType.Anarchy, civ.Government);
        Assert.Equal(GovernmentFunctions.AnarchyTurns, civ.AnarchyTurnsRemaining);
    }

    [Fact]
    public void ARevolutionIsRefusedWhileAlreadyInOne()
    {
        var (game, civ) = Board();
        civ.Advances[MonarchyAdvance] = true;
        GovernmentFunctions.BeginRevolution(game, civ);

        civ.AnarchyTurnsRemaining = 1;
        GovernmentFunctions.BeginRevolution(game, civ);

        Assert.Equal(1, civ.AnarchyTurnsRemaining);
    }

    [Fact]
    public void AdoptingAGovernmentTrimsARateItDoesNotAllow()
    {
        var (game, civ) = Board();
        civ.ScienceRate = 90;
        civ.TaxRate = 10;

        // Despotism in this board caps science at 60.
        GovernmentFunctions.AdoptGovernment(game, civ, GovernmentType.Despotism);

        Assert.Equal(60, civ.ScienceRate);
    }

    private static Civilization Civ() => Board().Civ;

    private static (MockGame Game, Civilization Civ) Board()
    {
        var governments = new Government[7];
        for (var i = 0; i < governments.Length; i++)
        {
            governments[i] = new Government
            {
                Name = ((GovernmentType)i).ToString(),
                MaxRates = new Dictionary<string, int> { ["Science"] = 60, ["Tax"] = 60 },
            };
        }

        var advances = new Advance[100];
        for (var i = 0; i < advances.Length; i++)
        {
            advances[i] = new Advance { Index = i, Name = $"Advance {i}", Prereq1 = -1, Prereq2 = -1 };
        }

        var rules = new Rules { Advances = advances, Governments = governments };
        var civ = new Civilization
        {
            Id = 0,
            TribeName = "Americans",
            PlayerType = PlayerType.Local,
            Government = (int)GovernmentType.Despotism,
            Advances = new bool[advances.Length],
            AllowedAdvanceGroups = [AdvanceGroupAccess.CanResearch],
        };

        return (new MockGame
        {
            Rules = rules,
            AllCivilizations = [civ],
            Players = [new MockPlayer(civ)],
        }, civ);
    }
}
