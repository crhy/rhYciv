using System.Text.RegularExpressions;

namespace RhyCiv.Tests.IO;

/// <summary>
/// Every entry a menu draws has to do something when it is clicked.
/// <para>
/// A <c>MenuElement</c> with no command id becomes a menu row with no command
/// behind it: the game draws it, the player clicks it, and nothing at all
/// happens. Forty-five entries were in that state at once -- among them Build
/// Mines, Set Home City, Pillage, Go To and Fortify, all of which had working
/// commands that the menu simply never reached.
/// </para>
/// <para>
/// An entry that has no implementation yet is not a failure, but it has to say so
/// with <c>omitIfNoCommand</c>, which leaves it out of the menu rather than
/// advertising it.
/// </para>
/// </summary>
public class MenuEntriesDoSomethingTests
{
    [Fact]
    public void EveryMenuEntryEitherRunsSomethingOrIsLeftOut()
    {
        var repository = RepositoryRoot();
        string[] interfaces =
        [
            Path.Combine("UI.Compact", "CompactInterface.cs"),
            Path.Combine("UI.CompatAlternate", "CompatAlternateInterface.cs")
        ];

        var inert = new List<string>();
        var examined = 0;

        foreach (var file in interfaces)
        {
            foreach (var entry in Entries(File.ReadAllText(Path.Combine(repository, file))))
            {
                examined++;
                if (entry.Text is "-" || entry.HasCommand || entry.Omitted)
                {
                    continue;
                }

                inert.Add($"{entry.Text} ({file})");
            }
        }

        Assert.True(examined > 100, $"Only {examined} menu entries were found; the scan is not reading the menus.");

        Assert.True(inert.Count == 0,
            "These menu entries have no command behind them and are not marked " +
            "omitIfNoCommand, so they would be drawn and do nothing when clicked:\n  " +
            string.Join("\n  ", inert));
    }

    private static IEnumerable<(string Text, bool HasCommand, bool Omitted)> Entries(string source)
    {
        // Each entry is new("Menu Text", <shortcut>, <hotkey>[, <command id>][, flags]).
        // The shortcut argument can itself be a call, so the body is matched a
        // bracket at a time rather than up to the first closing bracket.
        var element = new Regex(@"new\(""(?<text>[^""]*)"",(?<rest>(?:[^()]|\([^()]*\))*)\)", RegexOptions.Multiline);

        // A menu group's first entry is the group's own title: it names the menu on
        // the bar and never has a command.
        var titles = new HashSet<string> { "&Game", "&Kingdom", "&View", "&Orders", "&World",
            "&Cheat", "&Editor", "&Civilopedia", "&Map" };

        foreach (Match match in element.Matches(source))
        {
            var text = match.Groups["text"].Value;
            if (titles.Contains(text))
            {
                continue;
            }

            var rest = match.Groups["rest"].Value;
            var arguments = Arguments(rest);

            // The command id is the fourth argument, named or not; anything that is
            // not a flag and is not one of the first two arguments is one.
            var hasCommand = arguments.Skip(2).Any(argument =>
                !argument.StartsWith("omitIfNoCommand") && !argument.StartsWith("repeat") &&
                (argument.StartsWith("commandId:") || IsIdentifier(argument)));

            yield return (text, hasCommand, rest.Contains("omitIfNoCommand: true"));
        }
    }

    private static List<string> Arguments(string rest)
    {
        var arguments = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    arguments.Add(rest[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        arguments.Add(rest[start..].Trim());
        return arguments.Where(argument => argument.Length > 0).ToList();
    }

    /// <summary>
    /// A bare command id, as opposed to the Key or Shortcut arguments around it.
    /// Command ids are constants of <c>CommandIds</c>, brought in by a static
    /// using, so they appear on their own without a type in front of them.
    /// </summary>
    private static bool IsIdentifier(string argument) =>
        !argument.StartsWith("Key.") && !argument.StartsWith("Shortcut.") &&
        !argument.StartsWith("new ") && !argument.StartsWith("new(") &&
        argument.All(character => char.IsLetterOrDigit(character) || character == '_') &&
        argument.Length > 0;

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "rhYciv.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
