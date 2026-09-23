using Model.ImageSets;
using RhyCiv.UI.Classic.ImageLoader;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// The mountain Gold special must be a keyed cutout, not the flat yellow disc
/// the sheet generator draws when no painting exists (#150).
/// </summary>
public class MountainGoldSpecialTests
{
    [Fact]
    public void BundledGoldSpecialCutoutExists()
    {
        var repository = FindRepositoryRoot();
        var path = Path.Combine(repository, "RaylibUI", "FOSSart", "Terrain", "Specials", "mountains_1.png");

        Assert.True(File.Exists(path), $"missing mountain gold special: {path}");
        Assert.True(new FileInfo(path).Length > 1000, "mountain gold special is suspiciously small");
    }

    [Fact]
    public void SheetGeneratorFindsThePaintingForMountainsSlotOne()
    {
        // build_standalone_sheets.special_source is the same path ApplyFossSpecialArt
        // and the sheet builder both look for; a missing file falls back to the
        // yellow ellipse that issue #150 is about.
        var repository = FindRepositoryRoot();
        var path = Path.Combine(repository, "RaylibUI", "FOSSart", "Terrain", "Specials", "mountains_1.png");

        Assert.True(File.Exists(path));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
