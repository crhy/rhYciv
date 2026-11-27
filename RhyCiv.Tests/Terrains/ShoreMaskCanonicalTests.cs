using System.Reflection;
using RhyCiv.UI.Classic.ImageLoader;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// #180 pinned: the edge-based shore (e9823e4b) stays loaded. LoadShore maps all
/// 256 masks through CanonicalShoreMask and loads shore_/shoreland_ files by that
/// key; a missing file drops the map back to the old 16-picture CoastMarch system,
/// which dammed every channel whose two ends touched land. Every canonical key for
/// every mask must therefore resolve to shipped art.
/// </summary>
public class ShoreMaskCanonicalTests
{
    private static readonly MethodInfo CanonicalShoreMaskMethod =
        typeof(TerrainLoader).GetMethod(
            "CanonicalShoreMask", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("TerrainLoader.CanonicalShoreMask not found");

    private static int CanonicalShoreMask(int mask)
    {
        var result = CanonicalShoreMaskMethod.Invoke(null, [mask])
            ?? throw new InvalidOperationException("TerrainLoader.CanonicalShoreMask returned null");
        return (int)result;
    }

    [Theory]
    // Bits 0-3 = land on the NE, SE, SW, NW edge; bits 4-7 = land touching only
    // the N, E, S, W corner. Corner N sits between edges NW(3) and NE(0), E
    // between NE and SE, S between SE and SW, W between SW and NW.
    [InlineData(0b0001_0001, 0b0000_0001)] // corner N beside a land NE edge is stripped
    [InlineData(0b0001_0000, 0b0001_0000)] // corner N with both adjacent edges sea stays
    [InlineData(0b0001_1111, 0b0000_1111)] // illegal mask 031: four land edges + corner N -> 0b1111
    [InlineData(0b0000_1010, 0b0000_1010)] // plain SE and NW land edges stay
    [InlineData(0b0010_0010, 0b0000_0010)] // corner E beside a land SE edge is stripped
    [InlineData(0b0010_0000, 0b0010_0000)] // corner E with NE and SE sea stays
    [InlineData(0b0101_0000, 0b0101_0000)] // opposite corners N and S with no land edges stay
    [InlineData(0b1111_0001, 0b1100_0001)] // a land NE edge strips N and E but keeps S and W
    public void CanonicalShoreMaskStripsCornerBesideLandEdgeKeepsOtherWise(int mask, int expected) =>
        Assert.Equal(expected, CanonicalShoreMask(mask));

    [Fact]
    public void EveryCanonicalShoreKeyHasShippedFiles()
    {
        // LoadShore returns without setting Shore/Sea if any canonical key is
        // missing art, and the caller falls back to CoastMarch (#180).
        var coast = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Terrain", "Coast");

        for (var mask = 0; mask < 256; mask++)
        {
            var key = CanonicalShoreMask(mask);
            var overlay = Path.Combine(coast, $"shore_{key:000}.png");
            var land = Path.Combine(coast, $"shoreland_{key:000}.png");

            Assert.True(File.Exists(overlay), $"mask {mask} canonicalizes to {key}: missing {overlay}");
            Assert.True(File.Exists(land), $"mask {mask} canonicalizes to {key}: missing {land}");
        }
    }

    [Fact]
    public void CanonicalKeysAreExactlyTheShippedShoreMasks()
    {
        // The shipped set is the 47 masks legal under the corner rule; the
        // canonical keys must cover it exactly, so no file is ever orphaned and
        // no key ever reaches for a file that was never drawn.
        var coast = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Terrain", "Coast");

        var canonicalKeys = Enumerable.Range(0, 256)
            .Select(CanonicalShoreMask)
            .Distinct()
            .ToHashSet();
        var shippedMasks = Directory.EnumerateFiles(coast, "shore_???.png")
            .Select(path => int.Parse(Path.GetFileName(path)["shore_".Length..^4]))
            .ToHashSet();

        if (!canonicalKeys.SetEquals(shippedMasks))
        {
            var missing = canonicalKeys.Except(shippedMasks).Concat(shippedMasks.Except(canonicalKeys))
                .OrderBy(k => k);
            Assert.Fail($"canonical keys and shipped shore masks differ: {string.Join(", ", missing)}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
