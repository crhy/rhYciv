using RhyCiv.Engine.SaveLoad;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;

namespace RhyCiv.Tests.SaveLoad;

/// <summary>
/// The name the Save Game dialog offers, and whether a name is already taken.
/// <para>
/// The name is built from the leader's initials and the year. It used to be built
/// with Substring(0, 2), which throws on a leader whose name is shorter than two
/// characters — so a one-letter leader, or a custom civilisation saved with the
/// name left blank, took the game down on opening the dialog, before there was
/// anything on screen to say why.
/// </para>
/// <para>
/// The dialog also used to write over an existing save on the first Enter, with
/// nothing to stop it. <see cref="SaveFileNames.Occupied"/> is what the dialog asks
/// with, so these cover the three cases the prompt turns on: nothing there, an
/// empty file, and a save that has actually been written.
/// </para>
/// </summary>
public class SaveFileNameTests : IDisposable
{
    // A folder of its own, so nothing here can touch the player's save folder.
    private readonly string _directory =
        Directory.CreateTempSubdirectory("rhyciv-save-names-").FullName;

    // TempPath rather than Path: the name tests below call Path.GetInvalidFileNameChars.
    private string TempPath(string name) => System.IO.Path.Combine(_directory, name);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("Caesar")]
    [InlineData("Xi")]
    [InlineData("R")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Ö")]
    [InlineData("!!!")]
    public void AnyLeaderName_ProducesAUsableFileName(string leaderName)
    {
        var name = Suggest(leaderName);

        Assert.EndsWith(".sav", name);
        Assert.NotEmpty(Path.GetFileNameWithoutExtension(name));
        Assert.DoesNotContain(name, character => Path.GetInvalidFileNameChars().Contains(character));
        Assert.Equal(name, Path.GetFileName(name));
    }

    [Fact]
    public void TheLeadersInitials_AreUsedWhenThereAreSome()
    {
        Assert.StartsWith("ca", Suggest("Caesar"));
    }

    [Fact]
    public void ANameWithNothingUsable_FallsBackRatherThanFailing()
    {
        var name = Suggest("!!!");

        Assert.StartsWith("rhy", name);
    }

    [Fact]
    public void TheNameCarriesNoSpaces()
    {
        // The year reads like "4000 BC", and a space in a suggested file name is an
        // avoidable nuisance on every platform.
        Assert.DoesNotContain(' ', Suggest("Caesar"));
    }

    [Fact]
    public void Occupied_IsFalse_WhenNothingIsThere()
    {
        Assert.False(SaveFileNames.Occupied(TempPath("not-written.sav")));
    }

    [Fact]
    public void Occupied_IsFalse_WhenTheFileHoldsNothing()
    {
        // A file created and never written to has no save to lose, so it does not
        // earn an interruption on the way to the one the player actually wants.
        var path = TempPath("empty.sav");
        File.Create(path).Dispose();

        Assert.True(File.Exists(path));
        Assert.False(SaveFileNames.Occupied(path));
    }

    [Fact]
    public void Occupied_IsTrue_OnceASaveHasBeenWrittenThere()
    {
        var (game, ruleset, _) = CleanRoomGameFactory.CreateGame();
        var path = TempPath("taken.sav");

        var serializer = new GameSerializer();
        AtomicFile.Write(path,
            stream => serializer.Write(stream, game, ruleset, new Dictionary<string, string> { ["Zoom"] = "0" }));

        Assert.True(SaveFileNames.Occupied(path));
    }

    [Fact]
    public void Occupied_Survives_AFailedSave()
    {
        // The point of the two together: AtomicFile keeps the old save whole when
        // a new one fails, and the prompt still sees that something is there.
        var path = TempPath("precious.sav");
        File.WriteAllText(path, "a whole game");

        Assert.Throws<InvalidOperationException>(() =>
            AtomicFile.Write(path, stream =>
            {
                stream.Write(System.Text.Encoding.UTF8.GetBytes("partial"));
                throw new InvalidOperationException("serialiser failed");
            }));

        Assert.True(SaveFileNames.Occupied(path));
        Assert.Equal("a whole game", File.ReadAllText(path));
    }

    private static string Suggest(string leaderName)
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        game.ActivePlayer.Civilization.LeaderName = leaderName;
        return SaveFileNames.Suggest(game);
    }
}
