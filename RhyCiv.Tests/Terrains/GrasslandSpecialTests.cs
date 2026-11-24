using Model.Core.Mapping;
using Model.ImageSets;
using Model.Images;
using RhyCiv.Engine.Enums;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Images;
using RaylibUI.RunGame.GameControls.Mapping;
using RhyCiv.Tests.TestFiles;
using System.Reflection;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// A grazing resource on grassland has to be painted (#178, "Buffalo behind grass").
/// <para>
/// The tile compositor drew the grass shield and the resource special from one
/// <c>if/else if</c>, so a grassland square showed either its shield or its special
/// and never both -- and grassland is exactly where the bison, pheasant and the rest
/// of the grazing specials live, so on the most common terrain in the game the
/// resource marker was simply never drawn. Civ II paints the resource icon over the
/// shield: both layers belong on the tile, shield first.
/// </para>
/// <para>
/// These compose a tile from solid-colour art built in memory and read the pixels
/// back, so the layer order is checked rather than eyeballed.
/// </para>
/// </summary>
public class GrasslandSpecialTests
{
    private const int TileWidth = 64;
    private const int TileHeight = 32;

    /// <summary>TerrainType.Ocean is 10, so every per-terrain array needs 11 cells.</summary>
    private const int TerrainCells = 11;

    private const int GrasslandType = (int)TerrainType.Grassland;

    /// <summary>SpecialType.Buffalo is 1, which is the second special slot the sheets carry.</summary>
    private const int BuffaloSlot = (int)SpecialType.Buffalo;

    private static readonly Color BaseGreen = new(0, 160, 0, 255);
    private static readonly Color ShieldBlue = new(0, 0, 255, 255);
    private static readonly Color SpecialRed = new(255, 0, 0, 255);

    /// <summary>The tile centre: the shield disc and the resource icon both cover it.</summary>
    private const int CentreX = TileWidth / 2;

    private const int CentreY = TileHeight / 2;

    /// <summary>
    /// A grassland tile from the clean-room game, shielded or not as asked, and
    /// otherwise bare: no hut, river, city or improvement, so what is painted comes
    /// from the two layers under test and nothing else.
    /// </summary>
    private static Tile PickGrassland(Map map, bool wantShield)
    {
        Tile? shielded = null;
        Tile? plain = null;

        foreach (var tile in map.Tile)
        {
            if (tile == null || tile.Type != TerrainType.Grassland)
            {
                continue;
            }

            if (tile.HasGoodyHut || tile.River || tile.CityHere != null || tile.Improvements.Count > 0)
            {
                continue;
            }

            if (tile.HasShield)
            {
                shielded ??= tile;
            }
            else
            {
                plain ??= tile;
            }
        }

        var chosen = wantShield ? shielded : plain;
        Assert.NotNull(chosen);
        return chosen!;
    }

    /// <summary>
    /// The special slot the tile reports. <see cref="Tile.Special"/> is fixed in the
    /// constructor from the map's resource seed, so the test sets the backing field
    /// directly -- it only needs the compositor to see a special, and nothing here
    /// reads the tile's yield.
    /// </summary>
    private static void GiveSpecial(Tile tile, int slot)
    {
        typeof(Tile)
            .GetField("<Special>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(tile, slot);
    }

    [Fact]
    public void BuffaloOnUnshieldedGrassland_IsPainted()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var map = game.Maps[0];
        var tile = PickGrassland(map, wantShield: false);
        GiveSpecial(tile, BuffaloSlot);

        var (art, created) = BuildArt($"plain-{GrasslandType}-{BuffaloSlot}");
        try
        {
            var details = MapImage.MakeTileGraphic(tile, map, art, game, game.Players[1].Civilization.Id);
            try
            {
                Assert.True(CountPixels(details.Image, SpecialRed) > 0,
                    "the buffalo special was not painted on an unshielded grassland tile");
                Assert.True(SameColour(details.Image.GetColor(CentreX, CentreY), SpecialRed),
                    $"the centre pixel is {Describe(details.Image.GetColor(CentreX, CentreY))}, " +
                    "expected the special's red where the icon should sit");
            }
            finally
            {
                details.Image.Unload();
            }
        }
        finally
        {
            UnloadAll(created);
        }
    }

    [Fact]
    public void BuffaloOnShieldedGrassland_PaintsTheShieldAndTheSpecial()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var map = game.Maps[0];
        var tile = PickGrassland(map, wantShield: true);
        GiveSpecial(tile, BuffaloSlot);

        var (art, created) = BuildArt($"shielded-{GrasslandType}-{BuffaloSlot}");
        try
        {
            var details = MapImage.MakeTileGraphic(tile, map, art, game, game.Players[1].Civilization.Id);
            try
            {
                Assert.True(CountPixels(details.Image, ShieldBlue) > 0,
                    "the grass shield disappeared from a tile that also carries a special");
                Assert.True(CountPixels(details.Image, SpecialRed) > 0,
                    "the buffalo special was not painted over the grass shield");

                // Both layers cover the centre, so the pixel there says which
                // came last: Civ II puts the resource icon on top of the shield.
                Assert.True(SameColour(details.Image.GetColor(CentreX, CentreY), SpecialRed),
                    $"the centre pixel is {Describe(details.Image.GetColor(CentreX, CentreY))}, " +
                    "expected the special's red drawn over the shield");
            }
            finally
            {
                details.Image.Unload();
            }
        }
        finally
        {
            UnloadAll(created);
        }
    }

    [Fact]
    public void ShieldedGrasslandWithoutASpecial_StillPaintsItsShield()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var map = game.Maps[0];
        var tile = PickGrassland(map, wantShield: true);
        GiveSpecial(tile, -1);

        var (art, created) = BuildArt($"shield-only-{GrasslandType}");
        try
        {
            var details = MapImage.MakeTileGraphic(tile, map, art, game, game.Players[1].Civilization.Id);
            try
            {
                Assert.True(CountPixels(details.Image, ShieldBlue) > 0,
                    "the grass shield marker disappeared from a tile with no special");
                Assert.Equal(0, CountPixels(details.Image, SpecialRed));
            }
            finally
            {
                details.Image.Unload();
            }
        }
        finally
        {
            UnloadAll(created);
        }
    }

    /// <summary>
    /// A TerrainSet of flat colour plates: green base tiles, a blue shield disc, a
    /// red resource icon, and transparent dither, hut and other-special plates so
    /// only the layer under test can leave a colour in the composed tile.
    /// </summary>
    private static (TerrainSet Art, List<Image> Created) BuildArt(string tag)
    {
        var created = new List<Image>();

        Image Plate(Color colour)
        {
            var plate = Image.GenColor(TileWidth, TileHeight, colour);
            created.Add(plate);
            return plate;
        }

        var art = new TerrainSet(TileWidth, TileHeight);

        art.BaseTiles = Enumerable.Range(0, TerrainCells)
            .Select(type => new MemoryStorage(Plate(BaseGreen), $"f081-{tag}-base-{type}"))
            .ToArray();

        // The shield: a disc in the middle of the tile, as the real marker is.
        var shield = Plate(Color.Blank);
        for (var y = 0; y < TileHeight; y++)
        {
            for (var x = 0; x < TileWidth; x++)
            {
                var dx = x - CentreX;
                var dy = (y - CentreY) * 2;
                if (dx * dx + dy * dy <= 14 * 14)
                {
                    shield.DrawPixel(x, y, ShieldBlue);
                }
            }
        }

        art.GrasslandShield = new MemoryStorage(shield, $"f081-{tag}-shield");

        // The specials: slot 0 is a blank plate everywhere, slot 1 carries the
        // resource icon, a block smaller than the shield disc so the disc's rim
        // survives under it.
        art.Specials = Enumerable.Range(0, 2).Select(slot =>
            Enumerable.Range(0, TerrainCells).Select(type =>
            {
                if (slot == BuffaloSlot && type == GrasslandType)
                {
                    return new MemoryStorage(Icon(), $"f081-{tag}-special-{slot}-{type}");
                }

                return new MemoryStorage(Plate(Color.Blank), $"f081-{tag}-special-{slot}-{type}");
            }).ToArray()).ToArray();

        Image Icon()
        {
            var icon = Plate(Color.Blank);
            for (var y = CentreY - 8; y < CentreY + 8; y++)
            {
                for (var x = CentreX - 10; x < CentreX + 10; x++)
                {
                    icon.DrawPixel(x, y, SpecialRed);
                }
            }

            return icon;
        }

        art.Huts = new MemoryStorage(Plate(Color.Blank), $"f081-{tag}-huts");
        art.Blank = new MemoryStorage(Plate(Color.Blank), $"f081-{tag}-blank");

        // The neighbour loop reads one image per terrain type off each of the four
        // dither maps, and the last of them again for the frontier softening; all
        // transparent, so the dithering cannot move a colour the test is looking for.
        art.DitherMaps = Enumerable.Range(0, 4).Select(edge => new DitherMap
        {
            X = 0,
            Y = 0,
            Images = Enumerable.Range(0, TerrainCells).Select(_ => Plate(Color.Blank)).ToArray()
        }).ToArray();

        return (art, created);
    }

    private static void UnloadAll(List<Image> images)
    {
        foreach (var image in images)
        {
            image.Unload();
        }
    }

    /// <summary>Raylib's colour is a plain struct of bytes; compare the channels.</summary>
    private static bool SameColour(Color first, Color second)
    {
        return first.R == second.R && first.G == second.G && first.B == second.B && first.A == second.A;
    }

    private static string Describe(Color colour)
    {
        return $"({colour.R},{colour.G},{colour.B},{colour.A})";
    }

    /// <summary>How many pixels of the composed tile are exactly this colour.</summary>
    private static int CountPixels(Image image, Color expected)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (SameColour(image.GetColor(x, y), expected))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
