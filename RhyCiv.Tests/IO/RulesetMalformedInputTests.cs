using RhyCiv.Engine.IO;
using Model.Core.GameRules;

namespace RhyCiv.Tests.IO;

/// <summary>
/// <c>RULES.txt</c> is user-editable: a mod author truncates it, reorders it, mistypes
/// a digit, drops a comma. Each test writes one such file into a temporary ruleset
/// directory and asks for the error a player should see — the file, the section and
/// the line — instead of the <c>IndexOutOfRangeException</c> or <c>FormatException</c>
/// the parser used to throw, sometimes only at turn 40 and with no mention of the
/// ruleset that caused it.
/// </summary>
public class RulesetMalformedInputTests : IDisposable
{
    private readonly List<string> _tempDirectories = [];

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Writes <paramref name="rulesText"/> as RULES.txt in a fresh ruleset directory.
    /// </summary>
    private Ruleset RulesetWritten(string rulesText)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rhyciv-malformed-rules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        File.WriteAllText(Path.Combine(directory, "RULES.txt"), rulesText);
        return new Ruleset("x", [], directory);
    }

    /// <summary>
    /// Convenience overload: one argument per line of the file.
    /// </summary>
    private Ruleset RulesetContaining(params string[] rulesLines) =>
        RulesetWritten(string.Join(Environment.NewLine, rulesLines));

    [Fact]
    public void ALeadersSectionTruncatedToTenLinesNamesTheSectionAndWhereItStarts()
    {
        List<string> lines = ["@LEADERS"];
        lines.AddRange(Enumerable.Range(1, 10).Select(i =>
            $"Leader {i}, Lady {i}, 0, 1, 0, Plurals, Adjective, 1, 1, 1"));
        var ruleset = RulesetWritten(string.Join(Environment.NewLine, lines));

        var error = Assert.Throws<RulesetParseError>(() => RulesParser.ParseRules(ruleset));

        Assert.Equal("LEADERS", error.Section);
        // '@LEADERS' is line 1, so its content starts on line 2.
        Assert.Equal(2, error.Line);
        Assert.Contains("RULES.txt", error.Message);
        Assert.Contains("LEADERS", error.Message);
        Assert.Contains("line 2", error.Message);
        Assert.Contains("expected at least 21 leader lines, found 10", error.Message);
        Assert.DoesNotContain("IndexOutOfRange", error.Message);
    }

    [Fact]
    public void AnOrdersLineMissingItsKeyNamesThatLine()
    {
        var ruleset = RulesetContaining("@ORDERS", "Fortify,F", "Halt");

        var error = Assert.Throws<RulesetParseError>(() => RulesParser.ParseRules(ruleset));

        Assert.Equal("ORDERS", error.Section);
        Assert.Equal(3, error.Line);
        Assert.Contains("ORDERS", error.Message);
        Assert.Contains("line 3", error.Message);
        Assert.Contains("expected at least 2 comma-separated fields, found 1", error.Message);
        Assert.Contains("\"Halt\"", error.Message);
    }

    [Fact]
    public void ASecondaryMapsLineWithFourNumbersNamesThatLine()
    {
        var ruleset = RulesetContaining("@SECONDARY_MAPS", "6,10,20,3");

        var error = Assert.Throws<RulesetParseError>(() => RulesParser.ParseRules(ruleset));

        Assert.Equal("SECONDARY_MAPS", error.Section);
        Assert.Equal(2, error.Line);
        Assert.Contains("SECONDARY_MAPS", error.Message);
        Assert.Contains("line 2", error.Message);
        Assert.Contains("expected at least 9 comma-separated fields, found 4", error.Message);
    }

    [Fact]
    public void ANonDigitInTheCivilize2ColumnNamesThatLine()
    {
        var ruleset = RulesetContaining("@CIVILIZE2", "0", "1", "x2");

        var error = Assert.Throws<RulesetParseError>(() => RulesParser.ParseRules(ruleset));

        Assert.Equal("CIVILIZE2", error.Section);
        Assert.Equal(4, error.Line);
        Assert.Contains("CIVILIZE2", error.Message);
        Assert.Contains("line 4", error.Message);
        Assert.Contains("expected every comma-separated field to be a number", error.Message);
        Assert.Contains("\"x2\"", error.Message);
    }

    [Fact]
    public void ALeaders2LineWithNoTabOrSpaceNamesThatLine()
    {
        var ruleset = RulesetContaining("@LEADERS2", "0012");

        var error = Assert.Throws<RulesetParseError>(() => RulesParser.ParseRules(ruleset));

        Assert.Equal("LEADERS2", error.Section);
        Assert.Equal(2, error.Line);
        Assert.Contains("LEADERS2", error.Message);
        Assert.Contains("line 2", error.Message);
        Assert.Contains("found no separator", error.Message);
    }

    [Fact]
    public void ASectionWithNoFieldContractIsStillReportedAsARulesetError()
    {
        // TERRAIN is read row by row and has no field contract to check, so the
        // handler still dies on a short file. ProcessSection re-raises that as the
        // same error type, naming the section, rather than swallowing it.
        var ruleset = RulesetContaining("@TERRAIN", "Grassland,1");

        var error = Assert.Throws<RulesetParseError>(() => RulesParser.ParseRules(ruleset));

        Assert.Equal("TERRAIN", error.Section);
        Assert.Contains("TERRAIN", error.Message);
        Assert.Contains("while reading this section", error.Message);
        Assert.True(error.InnerException is IndexOutOfRangeException || error.InnerException is ArgumentOutOfRangeException,
            $"the handler's own index failure should stay as the inner exception, got {error.InnerException}");
    }

    [Fact]
    public void TheBundledStandaloneRulesetStillParses()
    {
        var standalone = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Standalone");

        var rules = RulesParser.ParseRules(new Ruleset("rhYciv Standalone", [], standalone));

        Assert.Equal(88, rules.Advances.Length);
        Assert.Equal(21, rules.Leaders.Length);
        Assert.Equal(13, rules.Orders.Length);
        Assert.Equal(7, rules.Governments.Length);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
