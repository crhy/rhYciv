using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Moq;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// Issue #168: clicking a resource-map square has to do something the player
/// can see -- either move a citizen or say why it cannot. The size-one city is
/// the sharp case: both citizens already on the land, so a free square used to
/// swallow the click with no free specialist and no message.
/// </summary>
public class SizeOnePheasantClickTests
{
    private (Mock<IGame> game, Rules rules, Civilization civ, Map map) SetupGame()
    {
        var rules = new Rules
        {
            Governments = new[]
            {
                new Government { Level = 0, SettlersConsumption = 1, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = -1 },
                new Government { Level = 1, SettlersConsumption = 0, UnitTypesAlwaysFree = Array.Empty<int>(), Distance = -1 }
            }
        };
        rules.Cosmic.FoodEatenPerTurn = 2;
        var civ = new Civilization { Id = 1, Government = 0 };
        var map = new Map(true, 5) { XDim = 40, YDim = 10 };
        map.Tile = new Tile[40, 10];
        for (int y = 0; y < 10; y++)
        {
            for (int x_idx = 0; x_idx < 40; x_idx++)
            {
                int x = x_idx * 2 + y % 2;
                var tile = new Tile(x, y,
                    new Terrain
                    {
                        Name = "Plains",
                        Type = TerrainType.Plains,
                        Specials = Array.Empty<Special>(),
                        Defense = 100
                    },
                    0, map, x_idx + y * 40, new bool[2]);
                tile.SetVisible(civ.Id);
                map.Tile[x_idx, y] = tile;
            }
        }

        var game = new Mock<IGame>();
        game.Setup(g => g.Rules).Returns(rules);
        game.Setup(g => g.MaxDistance).Returns(100.0);
        game.Setup(g => g.AllCivilizations).Returns(new List<Civilization> { civ });
        return (game, rules, civ, map);
    }

    private static City BuildWorkingCity(Map map, Civilization civ, int size)
    {
        var location = map.Tile[10, 4];
        var city = new City { Owner = civ, Location = location, Size = size, Name = "PheasantTest" };
        location.CityHere = city;
        location.WorkedBy = city;
        return city;
    }

    [Fact]
    public void ClickOnFreeSquare_WithNoSpecialist_MovesAWorstWorkerOntoIt()
    {
        var (_, rules, civ, map) = SetupGame();
        var city = BuildWorkingCity(map, civ, 1);
        city.AutoAddDistributionWorkers(rules);

        // Both citizens on the land: centre + one radius square, no specialist.
        Assert.Equal(0, city.NoOfSpecialistsx4);
        Assert.Equal(2, city.WorkedTiles.Count);

        var target = city.Location.CityRadius()
            .Where(t => t != null && t != city.Location && t.WorkedBy == null)
            .Cast<Tile>()
            .First();

        var result = city.TryWorkTile(target, rules);

        Assert.Equal(WorkTileResult.Reassigned, result);
        Assert.True(result.IsSuccess());
        Assert.Equal(city, target.WorkedBy);
        // Still both on the land -- one square swapped, not a specialist round-trip.
        Assert.Equal(0, city.NoOfSpecialistsx4);
        Assert.Equal(2, city.WorkedTiles.Count);
        // The centre is never the square that gives up its worker.
        Assert.Equal(city, city.Location.WorkedBy);
        // Something that was worked is now free (the least productive non-centre).
        Assert.Contains(city.WorkedTiles, t => t != city.Location);
    }

    [Fact]
    public void ClickOnWorkedByThisCity_MakesSpecialist()
    {
        var (_, rules, civ, map) = SetupGame();
        var city = BuildWorkingCity(map, civ, 1);
        city.AutoAddDistributionWorkers(rules);

        var worked = city.WorkedTiles.First(t => t != city.Location);

        var result = city.TryWorkTile(worked, rules);

        Assert.Equal(WorkTileResult.Released, result);
        Assert.True(result.IsSuccess());
        Assert.Null(worked.WorkedBy);
        Assert.Equal(4, city.NoOfSpecialistsx4);
        Assert.Equal(city, city.Location.WorkedBy);
    }

    [Fact]
    public void ClickOnWorkedByAnotherCity_IsRefused()
    {
        var (_, rules, civ, map) = SetupGame();
        var city = BuildWorkingCity(map, civ, 1);
        city.AutoAddDistributionWorkers(rules);

        var other = new City { Owner = civ, Location = map.Tile[12, 4], Size = 1, Name = "Ribe" };
        var foreign = map.Tile[10, 5];
        foreign.WorkedBy = other;
        var before = city.WorkedTiles.Count;

        var result = city.TryWorkTile(foreign, rules);

        Assert.Equal(WorkTileResult.ForeignWorked, result);
        Assert.False(result.IsSuccess());
        Assert.Equal(other, foreign.WorkedBy);
        Assert.Equal(before, city.WorkedTiles.Count);
        Assert.Equal(0, city.NoOfSpecialistsx4);
    }

    [Fact]
    public void ClickOnOwnCentre_ClearsAndReassigns()
    {
        var (_, rules, civ, map) = SetupGame();
        var city = BuildWorkingCity(map, civ, 2);
        city.AutoAddDistributionWorkers(rules);
        var workedBefore = city.WorkedTiles.Count;

        var result = city.TryWorkTile(city.Location, rules);

        Assert.Equal(WorkTileResult.ClearedAndReassigned, result);
        Assert.True(result.IsSuccess());
        Assert.Equal(city, city.Location.WorkedBy);
        // AutoAdd puts workers back; size 2 means centre + two radius squares.
        Assert.Equal(workedBefore, city.WorkedTiles.Count);
        Assert.Equal(0, city.NoOfSpecialistsx4);
    }

    [Fact]
    public void ClickOnForeignCity_IsRefused()
    {
        var (_, rules, civ, map) = SetupGame();
        var city = BuildWorkingCity(map, civ, 1);
        city.AutoAddDistributionWorkers(rules);

        var foreignTile = map.Tile[10, 5];
        var foreignCity = new City { Owner = civ, Location = foreignTile, Size = 1, Name = "Other" };
        foreignTile.CityHere = foreignCity;
        foreignTile.WorkedBy = foreignCity;

        var result = city.TryWorkTile(foreignTile, rules);

        Assert.Equal(WorkTileResult.ForeignCity, result);
        Assert.False(result.IsSuccess());
        Assert.Equal(foreignCity, foreignTile.CityHere);
    }

    [Fact]
    public void ClickOnWorkedSquare_WhenAllAreSpecialists_IsRefused()
    {
        var (_, rules, civ, map) = SetupGame();
        var city = BuildWorkingCity(map, civ, 1);
        city.AutoAddDistributionWorkers(rules);

        // Every citizen a specialist while a non-centre square is still marked
        // worked: the edge the release branch has to refuse (size falling behind
        // the specialist count, or a square re-marked by hand).
        city.NoOfSpecialistsx4 = 4;
        var worked = city.WorkedTiles.First(t => t != city.Location);

        var result = city.TryWorkTile(worked, rules);

        Assert.Equal(WorkTileResult.NoSlotForSpecialist, result);
        Assert.False(result.IsSuccess());
        Assert.Equal(city, worked.WorkedBy);
        Assert.Equal(4, city.NoOfSpecialistsx4);
    }

    [Fact]
    public void ClickOnFreeSquare_WithNothingToReassign_IsRefused()
    {
        var (_, rules, civ, map) = SetupGame();
        var city = BuildWorkingCity(map, civ, 1);
        city.AutoAddDistributionWorkers(rules);

        // Only the centre remains worked: MakeSpecialist will not take it.
        foreach (var t in city.WorkedTiles.Where(t => t != city.Location).ToList())
        {
            t.WorkedBy = null;
        }

        var target = map.Tile[10, 5];
        target.WorkedBy = null;

        var result = city.TryWorkTile(target, rules);

        Assert.Equal(WorkTileResult.NoSpecialistAvailable, result);
        Assert.False(result.IsSuccess());
        Assert.Null(target.WorkedBy);
        Assert.Equal(city, city.Location.WorkedBy);
        Assert.Equal(0, city.NoOfSpecialistsx4);
    }
}
