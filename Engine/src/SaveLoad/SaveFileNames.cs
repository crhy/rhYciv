using System;
using System.IO;
using System.Linq;
using Model.Core;

namespace RhyCiv.Engine.SaveLoad;

/// <summary>
/// The name offered in the Save Game dialog, and whether a name is already taken.
/// </summary>
public static class SaveFileNames
{
    /// <summary>Used when a leader's name has nothing usable in it.</summary>
    private const string Fallback = "rhy";

    /// <summary>
    /// The leader's initials and the year, as a file name a file system will take.
    /// <para>
    /// This used to take the first two characters of the leader's name with
    /// Substring, which throws if the name is shorter than that. A one-letter
    /// leader, or a custom civilisation saved with the name left blank, took the
    /// game down when the Save dialog was opened -- before it could draw, so there
    /// was nothing on screen to say what had happened.
    /// </para>
    /// </summary>
    public static string Suggest(IGame game)
    {
        var leader = game.ActivePlayer.Civilization.LeaderName ?? string.Empty;
        var initials = new string(leader.Where(char.IsLetterOrDigit).Take(2).ToArray());
        if (initials.Length == 0)
        {
            initials = Fallback;
        }

        var year = game.Date.GameYearString(game.TurnNumber, "").Replace(".", string.Empty);
        return Sanitise($"{initials}_{year}.sav".ToLowerInvariant());
    }

    /// <summary>
    /// Whether a save already sits at <paramref name="path"/>.
    /// <para>
    /// This exists so the player can be asked before a save is replaced. The Save
    /// Game dialog used to write over whatever was there the moment Enter was
    /// pressed, and there is no undo: an evening's work could be traded for the
    /// state of the game at the instant a key went down. The dialog asks first, and
    /// this is what it asks with.
    /// </para>
    /// <para>
    /// A file that exists but holds nothing has no save to lose, so it does not
    /// earn an interruption.
    /// </para>
    /// </summary>
    public static bool Occupied(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return new FileInfo(path).Length > 0;
    }

    /// <summary>
    /// Replaces anything a file system will not accept. A year string carries a
    /// space between the number and the era, and a scenario is free to put whatever
    /// it likes in a leader's name.
    /// </summary>
    private static string Sanitise(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(character =>
            invalid.Contains(character) || character == ' ' ? '_' : character));
    }
}
