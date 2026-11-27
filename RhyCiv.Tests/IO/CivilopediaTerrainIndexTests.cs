using Model.Core.GameRules;
using Model.Core.Mapping;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;

namespace RhyCiv.Tests.IO;

/// <summary>
/// #106: Terrain pages pull their prose from the @@TERRAIN_INDEX block of
/// describe.txt, addressed by <see cref="CivilopediaLoader.GetTerrainIndex"/>.
/// The block is ordered by the ruleset: base terrains at their ruleset
/// position, then each terrain's first special (+length), then second
/// (+2*length). The bundled ruleset ships eleven base terrains with two
/// specials each, so the block spans indices 0..32. Pin the arithmetic: the
/// offsets are position contracts with describe.txt, like the rule tables.
/// </summary>
public class CivilopediaTerrainIndexTests
{
    private const int LastTerrainIndex = 32;

    [Fact]
    public void BaseTerrainsAddressTheirOwnRows()
    {
        var rules = BundledRules();
        var terrains = rules.Terrains[0];

        for (var position = 0; position < terrains.Length; position++)
        {
            var index = CivilopediaLoader.GetTerrainIndex(terrains[position], rules);

            Assert.Equal(position, index);
            Assert.InRange(index, 0, LastTerrainIndex);
        }
    }

    [Fact]
    public void SpecialsAddressRowsOffsetByTheBaseBlock()
    {
        var rules = BundledRules();
        var terrains = rules.Terrains[0];
        var length = terrains.Length;

        var addresses = new List<int>();
        foreach (var (host, terrain) in terrains.Select((terrain, position) => (position, terrain)))
        {
            for (var slot = 0; slot < terrain.Specials.Length; slot++)
            {
                var index = CivilopediaLoader.GetTerrainIndex(terrain.Specials[slot], rules);

                Assert.Equal(host + (slot + 1) * length, index);
                Assert.InRange(index, 0, LastTerrainIndex);
                addresses.Add(index);
            }
        }

        Assert.NotEmpty(addresses);
        Assert.Equal(addresses.Count, addresses.Distinct().Count());
        Assert.Equal(LastTerrainIndex, addresses.Max());
    }

    [Fact]
    public void SpecialsAbsentFromTheRulesetAddressNothing()
    {
        var rules = BundledRules();

        Assert.Equal(-1, CivilopediaLoader.GetTerrainIndex(new Special { Name = "Elsewhere Mine" }, rules));
    }

    private static Rules BundledRules()
    {
        var standalone = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Standalone");
        return RulesParser.ParseRules(new Ruleset("rhYciv Standalone", [], standalone));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
