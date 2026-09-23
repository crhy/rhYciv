namespace RhyCiv.Tests.Terrains;

/// <summary>
/// The map's goody-hut marker is painted art cut from its generation matte
/// (#77), not the procedural shelter <c>build_standalone_sheets.tile_hut</c>
/// draws into the TERRAIN1 fallback cell. TerrainLoader composes this file
/// onto the tile whenever FOSS terrain art is applied.
/// </summary>
public class GoodyHutArtTests
{
    [Fact]
    public void BundledGoodyHutArtExists()
    {
        var path = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Terrain", "goodyhut.png");

        Assert.True(File.Exists(path), $"missing goody hut art: {path}");
        Assert.True(new FileInfo(path).Length > 10_000, "goody hut art is suspiciously small");
    }

    [Fact]
    public void GoodyHutArtIsLargerThanTheSheetCell()
    {
        // A keyed cutout is well over the 64x32 TERRAIN1 cell; anything at
        // cell scale is the old generated icon, not the painted tipi.
        var path = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Terrain", "goodyhut.png");
        Assert.True(File.Exists(path), $"missing goody hut art: {path}");

        var (width, height) = ReadPngSize(path);
        Assert.True(width > 64 || height > 64,
            $"goody hut art is only {width}x{height}; expected the full painted cutout");
    }

    [Fact]
    public void KilledUnitMarkerArtExists()
    {
        var path = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Other", "deadtroop.png");

        Assert.True(File.Exists(path), $"missing killed-unit marker: {path}");
        Assert.True(new FileInfo(path).Length > 10_000, "killed-unit marker is suspiciously small");
    }

    /// <summary>Width and height from a PNG's IHDR chunk, without loading the image.</summary>
    private static (int Width, int Height) ReadPngSize(string path)
    {
        var header = new byte[24];
        using var stream = File.OpenRead(path);
        var read = stream.Read(header, 0, header.Length);
        Assert.True(read >= 24, $"{Path.GetFileName(path)} is too short to be a PNG");

        // PNG signature then IHDR length/type; width and height are big-endian uint32.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            header.Take(8).ToArray());
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(header, 12, 4));

        var width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        var height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        return (width, height);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
