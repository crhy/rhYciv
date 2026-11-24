using Model.Core.GameRules;
using Model.Core.Mapping;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Terrains;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// Civ II mines Mountains for +2 shields (CIV2-PARITY-DEEP-AUDIT.md TR4:
/// "Mountains mining is +1 shield (Civ II: +2)"). A worked Mountain must yield
/// that +2 mining bonus, and no other terrain's mining/shield yields may move.
/// </summary>
public class MountainMiningParityTests
{
    private static Terrain[] LoadTerrains()
    {
        var root = FindRepositoryRoot();
        var ruleset = new Ruleset(
            "f217 mountain mining parity",
            new Dictionary<string, string> { ["Source"] = "RaylibUI/FOSSart/Standalone" },
            Path.Combine(root, "RaylibUI", "FOSSart", "Standalone"),
            Path.Combine(root, "Engine", "Scripts"));
        Labels.UpdateLabels(ruleset);
        var rules = RulesParser.ParseRules(ruleset);
        return rules.Terrains[0];
    }

    [Fact]
    public void WorkedMountain_YieldsTheCiv2MiningBonus()
    {
        var mountains = LoadTerrains()[(int)TerrainType.Mountains];

        // The ruleset row carries the Civ II +2, and the helper exposes the
        // worked tile's shield yield (base shields + the mining bonus).
        Assert.Equal(MountainMiningParity.Civ2MiningShieldBonus, mountains.MiningBonus);
        Assert.Equal(mountains.Shields + 2, MountainMiningParity.ExpectedWorkedShieldYield(mountains));
    }

    [Fact]
    public void OtherTerrains_MiningAndShieldYieldsAreUnchanged()
    {
        var terrains = LoadTerrains();

        // Only the Mountains mining bonus was touched; everything else keeps its
        // RULES.txt @TERRAIN shield yield and (non-)mining bonus.
        Assert.Equal(1, terrains[(int)TerrainType.Plains].Shields);
        Assert.Equal(0, terrains[(int)TerrainType.Plains].MiningBonus);
        Assert.Equal(0, terrains[(int)TerrainType.Grassland].Shields);
        Assert.Equal(0, terrains[(int)TerrainType.Grassland].MiningBonus);
        Assert.Equal(0, terrains[(int)TerrainType.Forest].MiningBonus);
        Assert.Equal(1, terrains[(int)TerrainType.Desert].MiningBonus);
        Assert.Equal(3, terrains[(int)TerrainType.Hills].MiningBonus);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
