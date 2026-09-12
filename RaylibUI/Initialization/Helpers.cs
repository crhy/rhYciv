using System.Reflection;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using Model;
using Model.Core.GameRules;
using Model.Interface;
using Raylib_CSharp.Fonts;

namespace RaylibUI.Initialization;

public static class Helpers
{
    /// <summary>
    /// Discovers the <see cref="IUserInterface"/> implementations shipped beside the
    /// executable.
    /// <para>
    /// Reflection over the output directory is how third-party interfaces stay
    /// possible, but it fails at runtime, on a user's machine, with no clue as to
    /// why. So every DLL that could not be examined is recorded and reported, and
    /// finding nothing is a named, actionable error rather than an
    /// <see cref="ArgumentOutOfRangeException"/> from a later index. Note that a
    /// trimmed or single-file publish removes the assemblies this walks, and would
    /// break here first.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No implementation was found, listing what was scanned and what was skipped.
    /// </exception>
    public static IList<IUserInterface> LoadInterfaces(IMain main)
    {
        var implementors = new List<IUserInterface>();
        var directory = new DirectoryInfo(Settings.BasePath);
        var userInterfaceType = typeof(IUserInterface);

        var scanned = new List<string>();
        var skipped = new List<string>();

        foreach (var file in directory.GetFiles("*.dll"))
        {
            scanned.Add(file.Name);
            try
            {
                var assembly = Assembly.Load(AssemblyName.GetAssemblyName(file.FullName));
                implementors.AddRange(assembly.GetTypes()
                    .Where(type => type != userInterfaceType
                                   && userInterfaceType.IsAssignableFrom(type)
                                   && !type.IsAbstract)
                    .Select(type => Activator.CreateInstance(type, main))
                    .OfType<IUserInterface>());
            }
            catch (Exception e)
            {
                // A native or unmanaged DLL sitting in the output directory is the
                // ordinary case and is not worth alarming anyone about, but it is
                // recorded so that it appears if the scan ends up finding nothing.
                skipped.Add($"{file.Name}: {e.GetType().Name}: {e.Message}");
            }
        }

        if (implementors.Count != 0)
        {
            return implementors.ToArray();
        }

        throw new InvalidOperationException(
            $"No {nameof(IUserInterface)} implementation was found in '{Settings.BasePath}', so there " +
            "is no interface to render the game with. This normally means the interface assemblies " +
            "were not copied next to the executable, or that a trimmed/single-file publish removed " +
            $"them.{Environment.NewLine}" +
            $"Scanned {scanned.Count} assemblies: {string.Join(", ", scanned)}{Environment.NewLine}" +
            (skipped.Count == 0
                ? "None were skipped."
                : $"Skipped {skipped.Count}:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", skipped)}"));
    }

    /// <summary>
    /// Picks the interface that serves <paramref name="path"/>, falling back to the
    /// first ruleset's interface and then to the first interface discovered.
    /// </summary>
    public static IUserInterface GetInterface(string path, IList<IUserInterface> interfaces, Ruleset[] ruleSets)
    {
        if (interfaces.Count == 0)
        {
            throw new InvalidOperationException(
                $"GetInterface was called with no interfaces. {nameof(LoadInterfaces)} should have " +
                "failed before this point.");
        }

        foreach (var ruleSet in ruleSets)
        {
            if (ruleSet.Paths.Contains(path))
            {
                return interfaces[ruleSet.InterfaceIndex];
            }
        }
        return ruleSets.Length > 0 ? interfaces[ruleSets[0].InterfaceIndex] : interfaces[0];
    }

    /// <summary>
    /// The characters the fonts are built with.
    /// </summary>
    /// <remarks>
    /// Asking raylib for a font without saying which characters gets the default
    /// ninety-five: printable ASCII and nothing else. Everything above it was
    /// drawn as a question mark, so Napoléon Bonaparte appeared in the Foreign
    /// Ministry as "Napol?on" -- and with him every accented name in the leaders
    /// table, the city name lists and the Civilopedia. The text was never wrong;
    /// the font simply had no such letter in it.
    ///
    /// Latin-1 Supplement covers the western European names, and Latin Extended-A
    /// covers the central and northern European ones -- the ł of Łódź, the ő of
    /// Győr -- which city name lists are full of.
    /// </remarks>
    private static int[] TextCodepoints()
    {
        var codepoints = new List<int>();
        for (var codepoint = 32; codepoint <= 126; codepoint++)      // printable ASCII
        {
            codepoints.Add(codepoint);
        }

        for (var codepoint = 0xA0; codepoint <= 0xFF; codepoint++)   // Latin-1 Supplement
        {
            codepoints.Add(codepoint);
        }

        for (var codepoint = 0x100; codepoint <= 0x17F; codepoint++) // Latin Extended-A
        {
            codepoints.Add(codepoint);
        }

        return codepoints.ToArray();
    }

    public static void LoadFonts()
    {
        var codepoints = TextCodepoints();
        var tnr = Utils.GetFilePath(Path.Combine("Fonts", "LiberationSerif-Regular.ttf")) ??
                  throw new FileNotFoundException("Bundled Liberation Serif font was not found.");
        Fonts.SetTnr(Font.LoadEx(tnr, 96, codepoints));
        var bold = Utils.GetFilePath(Path.Combine("Fonts", "LiberationSerif-Bold.ttf")) ??
                   throw new FileNotFoundException("Bundled bold Liberation Serif font was not found.");
        Fonts.SetBold(Font.LoadEx(bold, 112, codepoints));
        var alternative = Utils.GetFilePath(Path.Combine("Fonts", "LiberationSans-Regular.ttf")) ??
                          throw new FileNotFoundException("Bundled Liberation Sans font was not found.");
        Fonts.SetArial(Font.LoadEx(alternative, 96, codepoints));
    }
}
