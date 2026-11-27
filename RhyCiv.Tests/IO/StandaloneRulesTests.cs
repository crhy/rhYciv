using RhyCiv.Engine.IO;
using RhyCiv.Engine.Production;
using Model.Core.GameRules;

namespace RhyCiv.Tests.IO;

public class StandaloneRulesTests
{
    [Fact]
    public void BundledRulesetParsesTheCompleteClassicRulesShape()
    {
        var repository = FindRepositoryRoot();
        var standalone = Path.Combine(repository, "RaylibUI", "FOSSart", "Standalone");
        var rules = RulesParser.ParseRules(new Ruleset("rhYciv Standalone", [], standalone));

        Assert.Equal(88, rules.Advances.Length);
        Assert.Equal(67, rules.Improvements.Length);
        Assert.Equal(51, rules.UnitTypes.Length);
        Assert.Equal(7, rules.Governments.Length);
        Assert.Equal(21, rules.Leaders.Length);
        Assert.Equal(13, rules.Orders.Length);
        Assert.Equal(11, Assert.Single(rules.Terrains).Length);

        Assert.Equal("Settlers", rules.UnitTypes[0].Name);
        Assert.Equal("Fanatics", rules.UnitTypes[8].Name);
        Assert.Equal("Ocean", rules.Terrains[0][10].Name);
        var production = ProductionOrder.GetAll(rules);
        Assert.DoesNotContain(production, order => order.Title.Contains("Structural", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(production, order => order.Title.Contains("Component", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(production, order => order.Title.Contains("Module", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(production, order => order.Title.Contains("Apollo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BundledCityNamesParseIntoSeparateTribeLists()
    {
        var repository = FindRepositoryRoot();
        var standalone = Path.Combine(repository, "RaylibUI", "FOSSart", "Standalone");
        var cityNames = NameLoader.LoadCityNames([standalone]);

        Assert.True(cityNames.ContainsKey("AMERICANS"));
        Assert.True(cityNames.ContainsKey("ROMANS"));
        Assert.True(cityNames.ContainsKey("EXTRA"));
        Assert.True(cityNames.ContainsKey("BARBARIANS"));

        foreach (var (tribe, list) in cityNames)
        {
            Assert.NotNull(list);
            Assert.NotEmpty(list!);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "rhYciv.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
