using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Terrains;
using RhyCiv.Engine.Units;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// Terrain defensive-bonus parity (finding C2, the defensive half of tracker TR4,
/// issue #28): a unit defending in Forest, Jungle or Swamp must keep the Civ II
/// half-step -- x1.5 -- and not collapse to x1. The engine divides the rules.txt
/// terrain defence value by 2; as an <c>int</c> that flattened Forest's 3 to 1.
/// These check the multiplier a fight actually reads (<see cref="Tile.Defense"/>
/// and the defender's <c>DefenseFactor</c>) against <see cref="TerrainDefenseParity"/>,
/// and tie that helper back to the shipped <c>@TERRAIN</c> rows, all without a
/// display.
/// </summary>
public class TerrainDefenseParityTests
{
    [Theory]
    [InlineData(TerrainType.Forest, "1.5")]   // forest defence must not flatten to x1
    [InlineData(TerrainType.Jungle, "1.5")]
    [InlineData(TerrainType.Swamp, "1.5")]
    [InlineData(TerrainType.Hills, "2")]      // documented multipliers kept as-is
    [InlineData(TerrainType.Mountains, "3")]
    [InlineData(TerrainType.Plains, "1")]
    [InlineData(TerrainType.Desert, "1")]
    [InlineData(TerrainType.Grassland, "1")]
    public void DefendingOnTerrain_KeepsItsCiv2Bonus(TerrainType terrain, string expectedMultiplier)
    {
        var expected = decimal.Parse(expectedMultiplier, CultureInfo.InvariantCulture);

        // The expected Civ II figure.
        Assert.Equal(expected, TerrainDefenseParity.Multiplier(terrain));

        // The multiplier a fight actually reads: a land defender with no walls,
        // fortification or veteran bonus defends on its terrain alone, so its
        // DefenseFactor is exactly base x terrain. Base 10 keeps an x1 collapse
        // visible as 10 where Civ II gives 15 in a forest.
        var tile = TerrainTile(terrain);
        Assert.Equal(expected, tile.Defense);
        Assert.Equal(10 * (double)expected, Defender().DefenseFactor(GroundAttacker(), tile, 0));
    }

    [Fact]
    public void ForestJungleSwamp_AreNotFlattenedToX1()
    {
        // The specific regression: an integer division turned these x1.5 into x1.
        foreach (var terrain in new[] { TerrainType.Forest, TerrainType.Jungle, TerrainType.Swamp })
        {
            Assert.NotEqual(1m, TerrainDefenseParity.Multiplier(terrain));
            Assert.Equal(15, Defender().DefenseFactor(GroundAttacker(), TerrainTile(terrain), 0));
        }
    }

    [Fact]
    public void MountainAndOpenTerrain_KeepTheirDocumentedMultipliers()
    {
        // Everything the forest change must leave untouched.
        Assert.Equal(3m, TerrainDefenseParity.Multiplier(TerrainType.Mountains));
        Assert.Equal(1m, TerrainDefenseParity.Multiplier(TerrainType.Plains));
        Assert.Equal(1m, TerrainDefenseParity.Multiplier(TerrainType.Desert));
        Assert.Equal(30, Defender().DefenseFactor(GroundAttacker(), TerrainTile(TerrainType.Mountains), 0));
        Assert.Equal(10, Defender().DefenseFactor(GroundAttacker(), TerrainTile(TerrainType.Plains), 0));
        Assert.Equal(10, Defender().DefenseFactor(GroundAttacker(), TerrainTile(TerrainType.Desert), 0));
    }

    [Fact]
    public void ShippedRules_TerrainRowsCarryTheHalfSteps()
    {
        // The helper is only an expectation until the shipped data backs it up: the
        // @TERRAIN rows the engine loads must carry the defence values it claims.
        var path = System.IO.Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Standalone", "RULES.txt");
        var shipped = new Dictionary<TerrainType, int>();
        var inTerrain = false;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line == "@TERRAIN")
            {
                inTerrain = true;
                continue;
            }
            if (!inTerrain) continue;
            if (!line.Contains(',')) break;

            var fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (!Enum.TryParse(fields[0], out TerrainType terrain)) break;

            // @TERRAIN: name, move, defence, ... -- the defence value counts half-steps.
            shipped[terrain] = int.Parse(fields[2], CultureInfo.InvariantCulture);
        }

        foreach (var terrain in new[]
        {
            TerrainType.Desert, TerrainType.Plains, TerrainType.Grassland, TerrainType.Forest,
            TerrainType.Hills, TerrainType.Mountains, TerrainType.Tundra, TerrainType.Glacier,
            TerrainType.Swamp, TerrainType.Jungle,
        })
        {
            Assert.True(shipped.TryGetValue(terrain, out var value), $"@TERRAIN row missing for {terrain}");
            Assert.Equal(TerrainDefenseParity.RulesValue(terrain), value);
        }
    }

    private static Tile TerrainTile(TerrainType terrain) =>
        new(0, 0,
            new Terrain { Type = terrain, Defense = TerrainDefenseParity.RulesValue(terrain), Specials = [] },
            0, new Map(true, 0), 0, new bool[2]);

    private static Unit Defender() => new()
    {
        TypeDefinition = new UnitDefinition { Defense = 10, Domain = UnitGas.Ground, Flags = new bool[15] }
    };

    private static Unit GroundAttacker() => new()
    {
        TypeDefinition = new UnitDefinition
        {
            Domain = UnitGas.Ground, Flags = new bool[15],
            AttackPerTurn = 1, Firepwr = 1, Hitp = 10
        }
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
