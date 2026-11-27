using System.Reflection;
using Model.ImageSets;
using Raylib_CSharp.Images;
using RaylibUtils;
using RhyCiv.UI.Classic.ImageLoader;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// The ocean slot-2 whale special (issue #177) must keep the fit-not-clip
/// behaviour commit 9c4666b9 landed: the loader scales the shipped cutout with
/// FitInsideDiamond so its head and tail survive, then clips only what falls
/// outside the diamond, so a click on the whale hits its own tile (#168).
/// The fit happens on the raylib-backed Image type, which is CPU-side, so
/// FitInsideDiamond is called through reflection on the shipped ocean art and
/// its real scale/lift are checked against the tile diamond (like
/// ShoreMaskCanonicalTests calls CanonicalShoreMask); the shipped art and the
/// fit/clip source contract stay pinned as well, the way the repo pins art by
/// file presence (see MountainGoldSpecialTests).
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

    [Fact]
    public void FitInsideDiamondFitsTheShippedWhaleAtTheLargestScaleInsideTheDiamond()
    {
        // The behaviour #177 asks for, measured on the shipped art instead of
        // on the loader's text: FitInsideDiamond returns the scale and lift at
        // which every visible pixel of the whale lies inside the tile diamond
        // (|dx| + |dy| <= 1, the same diamond the canvas is clipped to for
        // clicks, #168), and it returns the LARGEST such scale. A rename or
        // delete trips the text pins above; a broken scale would not.
        var artPath = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Terrain", "Specials", "ocean_2.png");
        Assert.True(File.Exists(artPath), $"missing ocean whale special: {artPath}");

        // The arguments the loader passes to the fit: the ocean slot-2 cutout
        // composed into a working-size tile, bounded to 0.56 x 0.72 of it
        // (ApplyFossSpecialArt -> ComposeSpecialTile).
        var terrain = new TerrainSet(64, 32, TerrainLoader.DefaultTerrainRenderScale);
        var targetWidth = terrain.TileWidth * terrain.RenderScale;
        var targetHeight = terrain.TileHeight * terrain.RenderScale;

        var art = Images.LoadImageFromFile(artPath).Image;
        Assert.True(art.Width > 1 && art.Height > 1, "ocean_2.png loaded empty");
        var largest = MathF.Min(targetWidth * 0.56f / art.Width, targetHeight * 0.72f / art.Height);

        var fitInsideDiamond = typeof(TerrainLoader).GetMethod(
            "FitInsideDiamond", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TerrainLoader.FitInsideDiamond not found");
        var fitted = (ValueTuple<float, float>)fitInsideDiamond.Invoke(null,
            [art, targetWidth, targetHeight, largest])!;
        var (scale, lift) = fitted;
        Assert.True(scale > 0f, "FitInsideDiamond returned no scale");

        // Every visible pixel of the shipped art, at the visibility threshold
        // FitInsideDiamond itself reads the cutout at.
        var colours = art.LoadColors();
        var opaque = new List<(float X, float Y)>();
        for (var y = 0; y < art.Height; y++)
        {
            for (var x = 0; x < art.Width; x++)
            {
                if (colours[y * art.Width + x].A > 24)
                {
                    opaque.Add((x + 0.5f, y + 0.5f));
                }
            }
        }
        Image.UnloadColors(colours);
        Assert.NotEmpty(opaque);

        // (a) Nothing is clipped and nothing overhangs: drawn at the returned
        // (scale, lift), every visible whale pixel lies inside the diamond.
        var furthestInside = opaque.Max(point =>
            DiamondDistance(point, scale, lift, art.Width, art.Height, targetWidth, targetHeight));
        Assert.True(furthestInside <= 1.0f,
            $"whale sticks out of the tile diamond at scale {scale}, lift {lift}: |dx| + |dy| = {furthestInside:F3}");

        // (a') A broken (too small) scale would pass (a) too, so the fit must
        // be maximal: scaling it up 5% pushes some visible pixel outside.
        var overhangs = opaque.Any(point =>
            DiamondDistance(point, scale * 1.05f, lift, art.Width, art.Height, targetWidth, targetHeight) > 1.0f);
        Assert.True(overhangs,
            $"scale {scale} is not the largest fit: at {scale * 1.05f} every whale pixel still lies inside the diamond");
    }

    /// <summary>
    /// |dx| + |dy|, normalised to the tile diamond, of an art pixel centre
    /// placed the way ComposeSpecialTile places the cutout: centred at
    /// <paramref name="scale"/> and raised by <paramref name="lift"/> times
    /// the tile height.
    /// </summary>
    private static float DiamondDistance((float X, float Y) point, float scale, float lift,
        int artWidth, int artHeight, int targetWidth, int targetHeight)
    {
        var offsetX = (targetWidth - artWidth * scale) / 2f;
        var offsetY = (targetHeight - artHeight * scale) / 2f - targetHeight * lift;
        var cx = targetWidth / 2f;
        var cy = targetHeight / 2f;
        return MathF.Abs(offsetX + point.X * scale - cx) / cx
             + MathF.Abs(offsetY + point.Y * scale - cy) / cy;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
