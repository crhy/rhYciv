using System.Text.RegularExpressions;

namespace RhyCiv.Tests.IO;

/// <summary>
/// Every dialog the code asks for by name has to exist in the game's text.
/// <para>
/// Asking for one that does not is silent: the lookup fails, nothing is shown, and
/// the handler that would have done the work never runs. That has hidden whole
/// features. The Diplomat shipped in 0.1.4 and did nothing at all for two releases
/// because every one of its six dialogs was missing, and changing production
/// between categories quietly discarded the player's choice because the dialog
/// that would have confirmed it was not there either.
/// </para>
/// <para>
/// This walks the source rather than the running game, so it catches the fault at
/// the moment it is introduced instead of when somebody happens to press the
/// button.
/// </para>
/// </summary>
public class DialogsExistTests
{
    [Fact]
    public void EveryDialogTheCodeAsksForIsInTheGameText()
    {
        var repository = RepositoryRoot();
        var defined = DefinedDialogs(Path.Combine(repository,
            "RaylibUI", "FOSSart", "Standalone", "Game.txt"));

        Assert.NotEmpty(defined);

        var missing = ReferencedDialogs(repository)
            .Where(reference => !defined.Contains(reference.Name))
            .ToList();

        Assert.True(missing.Count == 0,
            "These dialogs are asked for by name and are not in Game.txt, so nothing " +
            "would be shown and the work behind them would never happen:\n  " +
            string.Join("\n  ", missing.Select(m => $"{m.Name} ({m.File})")));
    }

    private static HashSet<string> DefinedDialogs(string gameTextPath)
    {
        // Section headings are @NAME. Everything else beginning with @ is a property
        // of the section it sits in.
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "title", "width", "button", "options", "default", "listbox", "checkbox", "x", "y" };

        var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(gameTextPath))
        {
            if (!line.StartsWith('@'))
            {
                continue;
            }

            var name = line[1..].Split('=')[0].Trim();
            if (name.Length > 0 && !properties.Contains(name))
            {
                defined.Add(name);
            }
        }

        return defined;
    }

    private static IEnumerable<(string Name, string File)> ReferencedDialogs(string repository)
    {
        var asked = new Regex(@"(?:ShowPopup|ShowCityDialog)\(\s*""([A-Z0-9_]+)""");

        // Only the source projects. Walking the whole repository wanders into the
        // Flatpak build tree, which contains a sandbox root the tests cannot read.
        string[] projects = ["Engine", "Model", "RaylibUI", "RaylibUtils",
                             "UI.Classic", "UI.Compact", "UI.CompatAlternate"];

        foreach (var project in projects)
        {
            var directory = Path.Combine(repository, project);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                foreach (Match match in asked.Matches(File.ReadAllText(file)))
                {
                    yield return (match.Groups[1].Value, Path.GetRelativePath(repository, file));
                }
            }
        }
    }

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
