using System;
using System.Collections.Generic;
using System.IO;
using Model.Core;
using Model.Images;

namespace RhyCiv.Engine.Diplomacy;

/// <summary>
/// The face a civilisation's leader shows across the negotiating table.
/// </summary>
/// <remarks>
/// Civ II holds a parley in a throne room with the other leader's portrait on
/// the wall, and addresses the player in that leader's own voice. This game's
/// parley was a menu of buttons on a stone panel with nobody on the other side
/// of it.
///
/// The art is one file per tribe per gender under
/// <c>FOSSart/Leaders/Portraits</c>, named for the tribe and the gender with no
/// separator -- <c>celtmale.jpg</c>, <c>russianfemale.jpg</c>. Tribes are matched
/// by several spellings of their own name rather than by a table of file names,
/// so a portrait added later is picked up by being dropped in the folder and
/// needs no code: "Celts" finds <c>celt</c>, "Americans" finds <c>american</c>,
/// "Japanese" finds <c>japanese</c>.
/// </remarks>
public static class LeaderPortraits
{
    private const string Folder = "Leaders";
    private const string Subfolder = "Portraits";

    /// <summary>Extensions tried, in order, for a portrait.</summary>
    private static readonly string[] Extensions = [".jpg", ".png"];

    /// <summary>
    /// The portrait for this civilisation's leader, or null when the tribe has
    /// none yet. A missing portrait is not a fault: the set is being drawn, and
    /// a parley without a face is what every parley looked like before.
    /// </summary>
    public static IImageSource? For(Civilization civilization)
    {
        var gender = civilization.LeaderGender == 1 ? "female" : "male";
        foreach (var stem in NamesFor(civilization))
        {
            foreach (var extension in Extensions)
            {
                var relative = Path.Combine(Folder, Subfolder, stem + gender + extension);
                if (Exists(relative))
                {
                    return new BitmapStorage(relative);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The spellings of a tribe's name a portrait might be filed under: the
    /// adjective ("Celtic"), the plural ("Celts"), and the plural with its
    /// trailing s removed ("Celt"), all folded to lower case.
    /// </summary>
    private static IEnumerable<string> NamesFor(Civilization civilization)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in new[] { civilization.Adjective, civilization.TribeName })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var folded = candidate.Trim().ToLowerInvariant().Replace(" ", string.Empty);
            if (seen.Add(folded))
            {
                yield return folded;
            }

            if (folded.EndsWith('s') && seen.Add(folded[..^1]))
            {
                yield return folded[..^1];
            }
        }
    }

    private static bool Exists(string relative)
    {
        foreach (var root in Settings.SearchPaths)
        {
            if (File.Exists(Path.Combine(root, relative)))
            {
                return true;
            }
        }

        return false;
    }
}
