using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Enums;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.GameRules;

namespace RhyCiv.Tests;

/// <summary>
/// Unlocks are keyed on the advance's name in the loaded ruleset, not on
/// (int)AdvanceType. The enum is a legacy list whose numbering follows a
/// different order: Monarchy is enum 54 but rules index 53, and 54 in the
/// shipped ruleset is Monotheism -- so the old enum-keyed check both failed to
/// fire on researching Monarchy and offered Monarchy when Monotheism was
/// discovered (#169).
/// </summary>
public class GovernmentRulesetTests
{
    private const int MonarchyIndex = 53;
    private const int MonotheismIndex = 54;

    [Fact]
    public void BundledRulesetPlacesMonarchyAndMonotheismAtTheirOwnIndices()
    {
        var rules = ParseBundledRules();

        Assert.Equal("Monarchy", rules.Advances[MonarchyIndex].Name);
        Assert.Equal("Monotheism", rules.Advances[MonotheismIndex].Name);
        Assert.True((int)AdvanceType.Monarchy != MonarchyIndex,
            "the legacy enum and the ruleset order still disagree for Monarchy; "
            + "if they ever agree again this test's premise needs revisiting");
    }

    [Fact]
    public void GovernmentUnlockedBy_FollowsTheRulesetOrderNotTheEnum()
    {
        var rules = ParseBundledRules();

        Assert.Equal(GovernmentType.Monarchy, GovernmentFunctions.GovernmentUnlockedBy(MonarchyIndex, rules));
        Assert.Equal(GovernmentType.Republic, GovernmentFunctions.GovernmentUnlockedBy(80, rules));
        Assert.Equal(GovernmentType.Communism, GovernmentFunctions.GovernmentUnlockedBy(15, rules));
        Assert.Equal(GovernmentType.Democracy, GovernmentFunctions.GovernmentUnlockedBy(20, rules));
        Assert.Equal(GovernmentType.Fundamentalism, GovernmentFunctions.GovernmentUnlockedBy(82, rules));

        // Monotheism sits where the enum puts Monarchy; it opens no government.
        // Pottery (rules index 63) opens nothing either.
        Assert.Null(GovernmentFunctions.GovernmentUnlockedBy(MonotheismIndex, rules));
        Assert.Null(GovernmentFunctions.GovernmentUnlockedBy(63, rules));
    }

    [Fact]
    public void ResearchingMonarchy_OffersRevolutionOnTheSameTurn()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var civ = game.GetPlayerCiv;
        // Clean-room civs are built with Government = 0, which is Anarchy in the
        // enum; a real new game starts under Despotism.
        civ.Government = (int)GovernmentType.Despotism;
        var player = new OffersRecorded(civ);
        game.ConnectPlayer(player);

        Assert.False(GovernmentFunctions.CanRevolt(civ, rules));

        // Index 53 is Monarchy in the shipped ruleset; granting it must both
        // unlock the government and notify the player after the tech bit is set.
        game.GiveAdvance(MonarchyIndex, civ);

        Assert.True(GovernmentFunctions.CanRevolt(civ, rules),
            "knowing Monarchy should be enough to start a revolution");
        Assert.Contains(GovernmentType.Monarchy, GovernmentFunctions.AvailableGovernments(civ, rules));
        Assert.Equal([(int)GovernmentType.Monarchy], player.Offers);
    }

    private static Rules ParseBundledRules()
    {
        var repository = FindRepositoryRoot();
        var standalone = Path.Combine(repository, "RaylibUI", "FOSSart", "Standalone");
        return RulesParser.ParseRules(new Ruleset("rhYciv Standalone", [], standalone));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }

    private sealed class OffersRecorded(Civilization civ) : MockPlayer(civ)
    {
        public List<int> Offers { get; } = [];

        public override void GovernmentAvailable(int government) => Offers.Add(government);
    }
}
