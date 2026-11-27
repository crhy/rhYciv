using System.IO.Compression;

namespace RhyCiv.Tests.Mapping;

/// <summary>
/// Pins the map cursor (crhy/rhYciv#44, "Cursor looks retarded"). The active-unit
/// marker used to sit inside the ICONS sheet on an opaque background and was drawn
/// at the raw art resolution, so it came out as an opaque grey box roughly five
/// map tiles across. Fixed in e8a555b4: VIEWPIECE.png is a full map-tile-diamond
/// (300x150) with real transparency, and WaitingView gives the marker a
/// renderScale derived from MapImage.TileRec so it lands on exactly one logical
/// tile at every zoom.
/// </summary>
public class ViewPieceCursorTests
{
    // RaylibUI/RunGame/GameControls/Mapping/MapImage.cs:30 declares the classic
    // logical map tile that the whole map is composed against:
    //     public static Rectangle TileRec = new (0, 0, 64, 32);
    // and WaitingView reads it back as the marker's logical size. RhyCiv.Tests
    // does not reference the RaylibUI project - it is an Exe and would drag the
    // 165 MB FOSSart tree transitively into the test output - so the footprint
    // is pinned here beside the marker art, which is read from the real file.
    // The tile footprint and the art resolution below are the contract the
    // marker maths rests on; change one and this test fails by design.
    private const int TileWidth = 64;
    private const int TileHeight = 32;

    [Fact]
    public void ViewPieceArtIsOneFullTileAtMapResolution()
    {
        var (width, height, _) = ReadMarker();

        // scripts/build_standalone_sheets.py, build_view_piece_cursor(): the
        // marker is a full map tile now, not the classic 64x32 sprite cell - it
        // is drawn at four times that footprint.
        Assert.Equal(300, width);
        Assert.Equal(150, height);

        // WaitingView derives renderScale from the width alone, so the art has
        // to carry the tile's 2:1 diamond aspect or the marker height skews.
        Assert.True(width * TileHeight == height * TileWidth,
            $"marker art is {width}x{height}, not the tile's 2:1 diamond aspect");
    }

    [Fact]
    public void RenderScaleDerivedFromTileRecDrawsExactlyOneTile()
    {
        var (markerWidth, markerHeight, _) = ReadMarker();

        // WaitingView.cs:28-29, verbatim:
        //     var logicalSize = new Vector2(MapImage.TileRec.Width, MapImage.TileRec.Height);
        //     var renderScale = marker.Width > 0 ? logicalSize.X / marker.Width : 1f;
        float renderScale = markerWidth > 0 ? (float)TileWidth / markerWidth : 1f;

        // The scale is derived from the tile, never from the art's own size:
        // the 300x150 VIEWPIECE art lands on exactly one 64x32 logical tile at
        // map resolution. Drawn raw at zoom 1 it measured ~4.7 tiles across -
        // the "about five times oversized" band issue #44 complained about.
        Assert.Equal(0.2133333f, renderScale, 4);
        Assert.Equal(TileWidth, markerWidth * renderScale, 3);
        Assert.Equal(TileHeight, markerHeight * renderScale, 3);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(4f)]
    public void MarkerStillFillsExactlyOneTileAtEveryZoom(float zoom)
    {
        var (markerWidth, markerHeight, _) = ReadMarker();
        float renderScale = (float)TileWidth / markerWidth;

        // TextureElement.Draw (TextureElement.cs:60-66): the map zoom multiplies
        // RenderScale, then the maxDrawSize clamp - WaitingView passes the same
        // logical tile - keeps the marker from ever overshooting its footprint.
        float drawScale = zoom * renderScale;
        float maxWidth = Math.Max(1f, TileWidth * zoom);
        float maxHeight = Math.Max(1f, TileHeight * zoom);
        drawScale = Math.Min(drawScale, Math.Min(maxWidth / markerWidth, maxHeight / markerHeight));

        Assert.Equal(TileWidth * zoom, markerWidth * drawScale, 2);
        Assert.Equal(TileHeight * zoom, markerHeight * drawScale, 2);
    }

    [Fact]
    public void MarkerCornersAreFullyTransparent()
    {
        var (width, height, rgba) = ReadMarker();

        // The grey box of #44: the marker was cut straight out of the ICONS
        // sheet and took its background with it. The generated VIEWPIECE.png
        // keeps its alpha instead - nothing is drawn at the corners of its
        // 300x150 canvas, (299, 149) included - so only the diamond outline
        // ever covers the terrain.
        foreach (var (x, y) in new[]
        {
            (0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)
        })
        {
            Assert.True(rgba[(y * width + x) * 4 + 3] == 0,
                $"marker corner ({x}, {y}) is not transparent: alpha " +
                $"{rgba[(y * width + x) * 4 + 3]}");
        }
    }

    [Fact]
    public void DiamondOutlineIsOpaqueAtEveryVertex()
    {
        var (width, height, rgba) = ReadMarker();

        // build_view_piece_cursor draws a white line over a dark halo along the
        // diamond; blank art would read as "transparent, good" above while
        // drawing nothing at all. Every vertex of the diamond (left, top, right,
        // bottom in art coordinates) has at least one fully opaque pixel.
        foreach (var (vx, vy) in new[]
        {
            (0, height / 2), (width / 2, 0), (width - 1, height / 2), (width / 2, height - 1)
        })
        {
            var opaque = 0;
            for (var x = Math.Max(0, vx - 5); x < Math.Min(width, vx + 6); x++)
            {
                for (var y = Math.Max(0, vy - 5); y < Math.Min(height, vy + 6); y++)
                {
                    if (rgba[(y * width + x) * 4 + 3] == 255)
                    {
                        opaque++;
                    }
                }
            }

            Assert.True(opaque > 0, $"no opaque marker pixel near diamond vertex ({vx}, {vy})");
        }
    }

    private static (int Width, int Height, byte[] Rgba) ReadMarker()
    {
        var path = Path.Combine(FindRepositoryRoot(), "RaylibUI", "FOSSart", "Standalone", "VIEWPIECE.png");
        Assert.True(File.Exists(path), $"missing view-piece marker art: {path}");
        return DecodeRgba8Png(path);
    }

    /// <summary>
    /// Un-filtered RGBA8 pixels of a non-interlaced PNG, top row first, four
    /// bytes per pixel (R, G, B, A). A hand-decoded PNG keeps the alpha checks
    /// above free of native (raylib) and third-party dependencies in the test
    /// project; it asserts the very properties the tests are about.
    /// </summary>
    private static (int Width, int Height, byte[] Rgba) DecodeRgba8Png(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            bytes.Take(8).ToArray());

        static int BeInt(byte[] data, int at) =>
            (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

        var idat = new MemoryStream();
        int width = 0, height = 0;
        var pos = 8;
        while (pos + 8 <= bytes.Length)
        {
            var length = BeInt(bytes, pos);
            var type = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
            if (type == "IHDR")
            {
                width = BeInt(bytes, pos + 8);
                height = BeInt(bytes, pos + 12);
                // Colour and layout are the other half of what the tests pin.
                Assert.Equal(8, bytes[pos + 16]);            // bit depth
                Assert.Equal(6, bytes[pos + 17]);            // colour type 6 = RGBA
                Assert.Equal(0, bytes[pos + 18]);            // deflate compression
                Assert.Equal(0, bytes[pos + 19]);            // adaptive filtering
                Assert.Equal(0, bytes[pos + 20]);            // non-interlaced
            }
            else if (type == "IDAT")
            {
                idat.Write(bytes, pos + 8, length);
            }

            pos += 12 + length; // data, then a CRC
            if (type == "IEND")
            {
                break;
            }
        }

        Assert.True(width > 0 && height > 0, $"{Path.GetFileName(path)} has no IHDR");

        // zlib: two header bytes, then the deflate stream (the trailing
        // adler32 is simply past the end of the stream DeflateStream reads).
        var idatBytes = idat.ToArray();
        var raw = new byte[(width * 4 + 1) * height];
        using (var compressed = new MemoryStream(idatBytes, 2, idatBytes.Length - 2))
        using (var deflate = new DeflateStream(compressed, CompressionMode.Decompress))
        {
            var filled = 0;
            while (filled < raw.Length)
            {
                var read = deflate.Read(raw, filled, raw.Length - filled);
                if (read == 0)
                {
                    break;
                }

                filled += read;
            }

            Assert.Equal(raw.Length, filled);
        }

        // Undo the per-scanline filter, the way a decoder must.
        static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        var stride = width * 4;
        var rgba = new byte[stride * height];
        var at = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = raw[at++];
            for (var x = 0; x < stride; x++)
            {
                var cur = raw[at++];
                byte left = x >= 4 ? rgba[y * stride + x - 4] : (byte)0;
                byte up = y > 0 ? rgba[(y - 1) * stride + x] : (byte)0;
                byte upLeft = x >= 4 && y > 0 ? rgba[(y - 1) * stride + x - 4] : (byte)0;
                rgba[y * stride + x] = filter switch
                {
                    0 => cur,
                    1 => (byte)(cur + left),
                    2 => (byte)(cur + up),
                    3 => (byte)(cur + (left + up) / 2),
                    4 => (byte)(cur + Paeth(left, up, upLeft)),
                    _ => throw new InvalidDataException($"{Path.GetFileName(path)} uses filter {filter}")
                };
            }
        }

        return (width, height, rgba);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RaylibUI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
