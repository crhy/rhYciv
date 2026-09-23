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
    // RULES.txt lists advances alphabetically; Monarchy loads at index 53, not
    // at (int)AdvanceType.Monarchy which is the legacy enum's 54. Board() names
    // advances at the indices the shipped ruleset uses.
    private const int MonarchyAdvance = 53;
    private const int RepublicAdvance = 80;

    [Fact]
    public void DespotismNeedsNothing()
    {
        var (game, civ) = Board();
        Assert.True(GovernmentFunctions.CanForm(civ, GovernmentType.Despotism, game.Rules));
    }

    [Fact]
    public void AGovernmentNeedsItsAdvance()
    {
        var (game, civ) = Board();

        Assert.False(GovernmentFunctions.CanForm(civ, GovernmentType.Monarchy, game.Rules));
        civ.Advances[MonarchyAdvance] = true;
        Assert.True(GovernmentFunctions.CanForm(civ, GovernmentType.Monarchy, game.Rules));
    }

    [Fact]
    public void AnarchyIsNeverSomethingToChooseFor()
    {
        var (game, civ) = Board();

        Assert.False(GovernmentFunctions.CanForm(civ, GovernmentType.Anarchy, game.Rules));
        Assert.DoesNotContain(GovernmentType.Anarchy, GovernmentFunctions.AvailableGovernments(civ, game.Rules));
    }

    [Fact]
    public void TheCurrentGovernmentIsNotOffered()
    {
        var (game, civ) = Board();

        // A revolution that changed nothing would still cost the turns of anarchy.
        Assert.DoesNotContain(GovernmentType.Despotism, GovernmentFunctions.AvailableGovernments(civ, game.Rules));
    }

    [Fact]
    public void ThereIsNothingToRevoltForUntilSomethingIsResearched()
    {
        var (game, civ) = Board();

        Assert.False(GovernmentFunctions.CanRevolt(civ, game.Rules));
        civ.Advances[RepublicAdvance] = true;
        Assert.True(GovernmentFunctions.CanRevolt(civ, game.Rules));
    }

    [Fact]
    public void AdvancesAreMappedToTheGovernmentTheyOpen()
    {
        var (game, _) = Board();

        Assert.Equal(GovernmentType.Monarchy, GovernmentFunctions.GovernmentUnlockedBy(MonarchyAdvance, game.Rules));
        Assert.Equal(GovernmentType.Republic, GovernmentFunctions.GovernmentUnlockedBy(RepublicAdvance, game.Rules));
        Assert.Null(GovernmentFunctions.GovernmentUnlockedBy((int)AdvanceType.Pottery, game.Rules));
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

        // Names the unlock table keys on, placed at the indices the shipped
        // ruleset loads them at (Monarchy 53, The Republic 80, …).
        advances[MonarchyAdvance].Name = "Monarchy";
        advances[RepublicAdvance].Name = "The Republic";
        advances[15].Name = "Communism";
        advances[20].Name = "Democracy";
        advances[82].Name = "Theology";

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
