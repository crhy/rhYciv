using RhyCiv.Engine.MapObjects;
using Model.Core.Mapping;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// What the square underneath a city produces.
/// <para>
/// Civ II's Civilopedia, Game Concepts / City Squares: "if the city is built on
/// Terrain that normally produces no Shields, one Shield is automatically added
/// to the other resources generated in the city square." Without it a city
/// founded on plain grassland produces nothing at all until a citizen is put on
/// a square that does, which is not how the original behaves -- and it showed up
/// as a one-shield disagreement when this game's reading of a Civ II save was
/// put beside the same city in Civ II itself.
/// </para>
/// </summary>
public class CitySquareYieldTests
{
    [Fact]
    public void ACityOnGroundThatMakesNoShields_StillMakesOne()
    {
        var (plain, _) = GrasslandPair();
        Assert.Equal(0, plain.GetShields(false));

        plain.CityHere = new Model.Core.Cities.City { Name = "Kells" };

        Assert.Equal(1, plain.GetShields(false));
    }

    [Fact]
    public void ACityOnGroundThatAlreadyMakesShields_GainsNothing()
    {
        // The minimum is a floor, not a bonus: Civ II adds the shield only where
        // the terrain makes none, which is the whole reason founding on a shielded
        // square is held to be a waste of it.
        var (_, shielded) = GrasslandPair();
        Assert.Equal(1, shielded.GetShields(false));

        shielded.CityHere = new Model.Core.Cities.City { Name = "Iona" };

        Assert.Equal(1, shielded.GetShields(false));
    }

    /// <summary>
    /// A grassland tile without the shield special and one with. Whether a square
    /// carries a shield is fixed by its coordinates, so this walks the map and
    /// takes whichever tiles actually have one.
    /// </summary>
    private static (Tile Plain, Tile Shielded) GrasslandPair()
    {
        var map = new Map(true, 0) { Tile = new Tile[16, 16], XDim = 16, YDim = 16 };
        var grassland = new Terrain
        {
            Type = TerrainType.Grassland,
            Specials = [],
            Shields = 0,
            Food = 2,
        };

        Tile? plain = null, shielded = null;
        for (var y = 0; y < 16 && (plain == null || shielded == null); y++)
        {
            for (var x = 0; x < 16 && (plain == null || shielded == null); x++)
            {
                var tile = new Tile(x, y, grassland, 1, map, x, new bool[2]);
                map.Tile[x, y] = tile;
                if (tile.HasShield) shielded ??= tile;
                else plain ??= tile;
            }
        }

        Assert.NotNull(plain);
        Assert.NotNull(shielded);
        return (plain, shielded);
    }
}
