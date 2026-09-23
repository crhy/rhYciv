using Model.ImageSets;
using Model.Images;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Images;
using Raylib_CSharp.Transformations;
using RhyCiv.UI.Classic.ImageLoader;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// Loads the painted river set the game itself would load, fills every connection
/// mask, and writes a contact sheet so a human can see what the map will draw.
/// Skipped when rhYcivtextures is not beside the repo.
/// </summary>
public class PaintedRiverArtTests
{
    [Fact]
    public void PaintedRiversFillEveryMask()
    {
        var directory = TerrainLoader.FindPaintedRiverDirectory();
        if (directory == null)
        {
            return;
        }

        var terrain = new TerrainSet(64, 32, 2)
        {
            River = new IImageSource[RiverMask.Count],
            RiverMouth = new IImageSource[4],
        };
        TerrainLoader.ApplyFossRiverArt(terrain);

        for (var mask = 1; mask < RiverMask.Count; mask++)
        {
            Assert.True(terrain.River[mask] != null, $"mask {mask:B4} has no tile");
        }

        ExportContactSheet(terrain);
    }

    /// <summary>
    /// Four-by-four grid of the installed connection tiles on a dark ground,
    /// with a gold band per set bit so the mask reads at a glance. Written to
    /// $RHYCIV_OUT/painted-rivers.png when that variable is set.
    /// </summary>
    private static void ExportContactSheet(TerrainSet terrain)
    {
        var outDir = Environment.GetEnvironmentVariable("RHYCIV_OUT");
        if (string.IsNullOrWhiteSpace(outDir))
        {
            return;
        }

        Directory.CreateDirectory(outDir);
        const int cell = 128;
        const int grid = 4;
        var sheet = Image.GenColor(cell * grid, cell * grid, new Color(30, 30, 40, 255));

        for (var mask = 0; mask < RiverMask.Count; mask++)
        {
            if (terrain.River[mask] is not MemoryStorage storage)
            {
                continue;
            }

            var art = storage.Image.Copy();
            art.Resize(cell, cell / 2);
            var col = mask % grid;
            var row = mask / grid;
            sheet.Draw(art,
                new Rectangle(0, 0, art.Width, art.Height),
                new Rectangle(col * cell, row * cell + cell / 4, cell, cell / 2),
                Color.White);
            art.Unload();

            for (var bit = 0; bit < 4; bit++)
            {
                if ((mask & (1 << bit)) == 0)
                {
                    continue;
                }

                sheet.DrawRectangle(col * cell + 8 + bit * 12, row * cell + 8, 10, 10,
                    new Color(200, 180, 80, 255));
            }
        }

        var path = Path.Combine(outDir, "painted-rivers.png");
        Assert.True(sheet.Export(path), $"could not write {path}");
        sheet.Unload();
    }
}
