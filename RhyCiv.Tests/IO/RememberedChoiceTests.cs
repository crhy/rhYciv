using System.Reflection;
using RhyCiv.Engine;

namespace RhyCiv.Tests.IO;

/// <summary>
/// The new-game questions remember what they were answered last time. Starting a
/// game asks a dozen of them and every one used to open on its own default, so a
/// player who always wants the same thing -- raging hordes every game -- had to
/// say so every game.
/// </summary>
public class RememberedChoiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "rhyciv-tests-" + Guid.NewGuid().ToString("N"));

    public RememberedChoiceTests()
    {
        // Never the player's own configuration: remembering an answer writes it out.
        Settings.DataFolder = () => _folder;
        Forget();
    }

    public void Dispose()
    {
        Settings.DataFolder = () => Path.Combine(Path.GetTempPath(), "rhyciv-tests-unset");
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void AQuestionThatHasNotBeenAnsweredHasNothingRemembered()
    {
        Assert.Null(Settings.NewGameChoice("BARBARITY"));
    }

    [Fact]
    public void AnAnswerComesBack()
    {
        Settings.RememberNewGameChoice("BARBARITY", 3);

        Assert.Equal(3, Settings.NewGameChoice("BARBARITY"));
    }

    [Fact]
    public void TheLatestAnswerWins()
    {
        Settings.RememberNewGameChoice("BARBARITY", 3);
        Settings.RememberNewGameChoice("BARBARITY", 1);

        Assert.Equal(1, Settings.NewGameChoice("BARBARITY"));
    }

    [Fact]
    public void QuestionsAreRememberedSeparately()
    {
        Settings.RememberNewGameChoice("BARBARITY", 3);
        Settings.RememberNewGameChoice("DIFFICULTY", 5);

        Assert.Equal(3, Settings.NewGameChoice("BARBARITY"));
        Assert.Equal(5, Settings.NewGameChoice("DIFFICULTY"));
    }

    [Fact]
    public void AShorterSettingsFileDoesNotLeaveTheTailOfTheLongerOneBehind()
    {
        Settings.RememberNewGameChoice("A_VERY_LONG_DIALOG_NAME_INDEED", 12345);
        Forget();
        Settings.RememberNewGameChoice("B", 1);

        // File.OpenWrite does not truncate; File.Create does. Without that the
        // second, shorter write left the end of the first behind and the file no
        // longer parsed.
        var written = File.ReadAllText(Path.Combine(_folder, "appsettings.json"));
        Assert.DoesNotContain("A_VERY_LONG_DIALOG_NAME_INDEED", written);
        using var parsed = System.Text.Json.JsonDocument.Parse(written);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void NonsenseIsIgnoredRatherThanStored()
    {
        Settings.RememberNewGameChoice("BARBARITY", -1);
        Settings.RememberNewGameChoice(" ", 2);

        Assert.Null(Settings.NewGameChoice("BARBARITY"));
        Assert.Null(Settings.NewGameChoice(" "));
    }

    private static void Forget() =>
        ((System.Collections.IDictionary)typeof(Settings)
            .GetField("RememberedChoices", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!).Clear();
}
