namespace RhyCiv.Tests.Terrains;

/// <summary>
/// The ocean slot-2 whale special (issue #177) must keep the fit-not-clip
/// behaviour commit 9c4666b9 landed: the loader scales the shipped cutout with
/// FitInsideDiamond so its head and tail survive, then clips only what falls
/// outside the diamond, so a click on the whale hits its own tile (#168).
/// The fit happens on the raylib-backed Image type and no xunit test in this
/// repo constructs one, so this pins (a) the shipped whale art and (b) the
/// fit/clip source contract in the loader, the way the repo already pins art
/// by file presence (see MountainGoldSpecialTests).
/// </summary>
public class WhalesSpecialFitTests
{
    [Fact]
    public void BundledWhaleSpecialPaintingAndSourcePhotoExist()
    {
        // scripts/prepare_custom_textures.py TERRAIN_SPECIAL_NAMES maps the
        // ocean slot-2 cutout "ocean_2_whales.png" to the shipped painting
        // ocean_2.png; the classic path keys the source photo whales.jpg.
        var repository = FindRepositoryRoot();
        var painting = Path.Combine(repository, "RaylibUI", "FOSSart", "Terrain", "Specials", "ocean_2.png");
        var photo = Path.Combine(repository, "RaylibUI", "FOSSart", "Other", "whales.jpg");

        Assert.True(File.Exists(painting), $"missing ocean whale special: {painting}");
        Assert.True(new FileInfo(painting).Length > 1000, "ocean whale special is suspiciously small");
        Assert.True(File.Exists(photo), $"missing whale source photo: {photo}");
        Assert.True(new FileInfo(photo).Length > 1000, "whale source photo is suspiciously small");
    }

    [Fact]
    public void LoaderFitsTheSpecialInsideTheDiamondInsteadOfClippingIt()
    {
        // The bug #177 describes: the whale's head and tail were clipped away.
        // The fix (9c4666b9) asks FitInsideDiamond for the largest scale whose
        // visible pixels stay inside the tile; a future edit that reverts to
        // scaling the sprite to the tile and clipping it fails this test.
        var loader = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "UI.Classic", "ImageLoader", "TerrainLoader.cs"));

        Assert.Contains("FitInsideDiamond", loader);
        Assert.Contains("FitInsideDiamond(art, targetWidth, targetHeight, largest)", loader);
    }

    [Fact]
    public void LoaderStillClipsTheComposedCanvasToTheDiamondForClicks()
    {
        // Fitting is not overhang: what is drawn must be what is clickable, so
        // the composed canvas is cleared outside |dx| + |dy| <= 1 (dx + dy > 1.0f
        // is the clip condition), and the comment names #168 as the reason.
        // Removing the clip sends clicks on the whale to the neighbour behind
        // it, the bug #168 was.
        var loader = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "UI.Classic", "ImageLoader", "TerrainLoader.cs"));

        Assert.Contains("dx + dy > 1.0f", loader);
        Assert.Contains("#168", loader);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
