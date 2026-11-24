using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using RhyCiv.Engine;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Tests.TestFiles;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// The Supermarket pays Civ II's +50% food from farmland, not a flat bonus.
/// <para>
/// crhy/rhYciv#28 TR1: Supermarket was modelled as "Food +1 on irrigated tiles".
/// Civ II gives +50% food, and only from farmland -- the second level of the
/// irrigation improvement (the level gated on Refrigeration), not plain
/// irrigation. The whole line is one terrain effect in improvements.lua, read in
/// TileResourceExtensions.GetFood: a Multiply effect does food *= Value / 100m,
/// and it matches a tile only when the tile carries the named improvement at the
/// required level (i.Level >= ef.Level). Level 0 is plain irrigation, Level 1 is
/// farmland, so the effect is keyed to Level 1.
/// </para>
/// </summary>
public class SupermarketFarmlandTests
{
    // A grassland tile worth 4 food worked by a city. The table pins the three
    // ways the Supermarket line can meet the tile: farmland with the improvement
    // (1.5x), farmland without it (unchanged), and plain irrigation (not
    // multiplied, because the effect is keyed to the farmland level).
    [Theory]
    [InlineData(true, 1, 6)] // Supermarket + farmland  -> 4 * 1.5 = 6
    [InlineData(false, 1, 4)] // farmland, no Supermarket -> unchanged
    [InlineData(true, 0, 4)] // Supermarket + plain irrigation -> not multiplied
    public void Supermarket_MultipliesFarmlandFoodOnly(bool supermarket, int level, int expected)
    {
        var tile = WorkedFarmTile(supermarket, level);

        Assert.Equal(expected, tile.GetFood(false));
    }

    /// <summary>
    /// The scripted Supermarket still parses and carries the farmland multiplier.
    /// <para>
    /// Loading the clean-room game runs improvements.lua against the rules, so
    /// improvement 24 carries whatever the script now defines. This pins that the
    /// flat +1 is gone and the +50% farmland Multiply is what replaced it.
    /// </para>
    /// </summary>
    [Fact]
    public void Supermarket_IsDefinedAsAFarmlandMultiplierInLua()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();

        var supermarket = game.Rules.Improvements[24];
        Assert.DoesNotContain(supermarket.TerrainEffects,
            e => e.Resource == ImprovementConstants.Food
                 && e.Action == CityTerrainEffect.Add
                 && e.Improvement == ImprovementTypes.Irrigation
                 && e.Level == 1
                 && e.Value == 1);

        var effect = supermarket.TerrainEffects.FirstOrDefault(
            e => e.Resource == ImprovementConstants.Food
                 && e.Action == CityTerrainEffect.Multiply
                 && e.Improvement == ImprovementTypes.Irrigation
                 && e.Level == 1);

        Assert.NotNull(effect);
        Assert.Equal(150, effect!.Value);
    }

    private static Tile WorkedFarmTile(bool supermarket, int level)
    {
        var map = new Map(true, 0) { Tile = new Tile[8, 8], XDim = 8, YDim = 8 };
        var terrain = new Terrain
        {
            Type = TerrainType.Grassland,
            Specials = [],
            Food = 4,
        };
        var tile = new Tile(3, 3, terrain, 1, map, 3, new bool[2]);
        map.Tile[3, 3] = tile;

        var city = new City { Name = "Kells", Owner = new Civilization { Id = 1 } };
        if (supermarket)
        {
            var improvement = new Improvement { Type = 24, Name = "Supermarket" };
            improvement.TerrainEffects.Add(new CityTerrainEffect
            {
                Resource = ImprovementConstants.Food,
                Action = CityTerrainEffect.Multiply,
                Value = 150,
                Improvement = ImprovementTypes.Irrigation,
                Level = 1,
            });
            city.OrderedImprovements.Add(24, improvement);
        }

        tile.WorkedBy = city;
        tile.Improvements.Add(new ConstructedImprovement
        {
            Improvement = ImprovementTypes.Irrigation,
            Level = level,
        });

        return tile;
    }
}
