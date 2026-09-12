using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core.Mapping;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// What a city this game founds gets for free on the square underneath it.
/// <para>
/// Civ II's Civilopedia, Game Concepts / City Squares: the square "automatically
/// contains a road", is "automatically irrigated or mined, depending on the type
/// of terrain", and gains one shield if the terrain produces none. The figures
/// asserted here were measured in Civ II itself rather than reasoned about:
/// Maesteg, a size-one Celtic city on hills, A.D. 1700, shows 2 food and 1 shield
/// on its own square -- so hills, which can take either improvement, are
/// irrigated and not mined, and the shield is the minimum rather than a mine's
/// three.
/// </para>
/// </summary>
public class NewCitySquareTests
{
    [Fact]
    public void ACityOnHills_IsIrrigatedAndNotMined()
    {
        var (centre, _) = FoundOn(TerrainType.Hills);

        // Civ II, Maesteg: 2 food, 1 shield, 0 trade. Mined hills would be three
        // shields, and the Civilopedia's "irrigated or mined" does not say which
        // wins where both are possible. This is the measurement that says.
        Assert.Equal(2, centre.GetFood(false));
        Assert.Equal(1, centre.GetShields(false));
    }

    [Fact]
    public void ACityOnGrassland_IsIrrigated()
    {
        var (centre, _) = FoundOn(TerrainType.Grassland);

        // Grassland is 2 food; irrigation is what makes it 3. A city that has to
        // wait for a worker to irrigate its own square is a city Civ II would have
        // fed from the turn it was founded.
        Assert.Equal(3, centre.GetFood(false));
    }

    [Fact]
    public void ACityOnGrassland_HasARoadWithoutBuildingOne()
    {
        var (centre, _) = FoundOn(TerrainType.Grassland);

        Assert.Contains(centre.Improvements, i => i.Improvement == ImprovementTypes.Road);
    }

    /// <summary>
    /// Puts the player's settler on ground of the given type and founds a city
    /// there, returning the city's own square.
    /// </summary>
    private static (Tile Centre, Model.Core.Cities.City City) FoundOn(TerrainType type)
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));

        var settler = game.GetPlayerCiv.Units.First(unit => !unit.Dead);
        var where = settler.CurrentLocation;
        where.Terrain = rules.Terrains[where.Z].First(terrain => terrain.Type == type);

        var city = CityActions.BuildCity(settler, game, "Measured");
        return (city.Location, city);
    }
}
