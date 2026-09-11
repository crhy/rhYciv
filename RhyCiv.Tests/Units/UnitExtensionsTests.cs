using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Units;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Units;
using Moq;

namespace RhyCiv.Tests.Units;

public class UnitExtensionsTests
{
    [Fact]
    public void AttackFactor_BaseValue()
    {
        var unit = new Unit { TypeDefinition = new UnitDefinition { Attack = 10 } };
        var enemy = new Unit { TypeDefinition = new UnitDefinition { Attack = 5 } };
        
        Assert.Equal(10.0, unit.AttackFactor(enemy));
    }

    [Fact]
    public void AttackFactor_VeteranBonus()
    {
        var unit = new Unit { TypeDefinition = new UnitDefinition { Attack = 10 }, Veteran = true };
        var enemy = new Unit { TypeDefinition = new UnitDefinition { Attack = 5 } };
        
        Assert.Equal(15.0, unit.AttackFactor(enemy));
    }

    [Fact]
    public void DefenseFactor_CarriedUnit_ReturnsZero()
    {
        var unit = new Unit { InShip = new Unit() };
        var attacker = new Unit();
        var map = new Map(true, 0);
        var tile = new Tile(0, 0, new Terrain { Specials = [] }, 0, map, 0, new bool[0]);
        
        Assert.Equal(0, unit.DefenseFactor(attacker, tile, 0));
    }

    [Fact]
    public void DefenseFactor_GroundUnit_BaseValue()
    {
        var unit = new Unit { TypeDefinition = new UnitDefinition { Defense = 10, Domain = UnitGas.Ground, Flags = new bool[15] } };
        var attacker = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = new bool[15] } };
        var map = new Map(true, 0);
        var tile = new Tile(0, 0, new Terrain { Defense = 2, Specials = [] }, 0, map, 0, new bool[2]);
        
        Assert.Equal(10, unit.DefenseFactor(attacker, tile, 0));
    }

    [Fact]
    public void DefenseFactor_VeteranBonus()
    {
        var unit = new Unit { TypeDefinition = new UnitDefinition { Defense = 10, Domain = UnitGas.Ground, Flags = new bool[15] }, Veteran = true };
        var attacker = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = new bool[15] } };
        var map = new Map(true, 0);
        var tile = new Tile(0, 0, new Terrain { Defense = 2, Specials = [] }, 0, map, 0, new bool[2]);
        
        // 10 * 1.5 = 15. Tile defense factor 1. Total 15.
        Assert.Equal(15, unit.DefenseFactor(attacker, tile, 0));
    }

    [Fact]
    public void DefenseFactor_FortifiedBonus()
    {
        var unit = new Unit { TypeDefinition = new UnitDefinition { Defense = 10, Domain = UnitGas.Ground, Flags = new bool[15] }, Order = (int)OrderType.Fortified };
        var attacker = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = new bool[15] } };
        var map = new Map(true, 0);
        var tile = new Tile(0, 0, new Terrain { Defense = 2, Specials = [] }, 0, map, 0, new bool[2]);
        
        // 10 + (10/2) = 15.
        Assert.Equal(15, unit.DefenseFactor(attacker, tile, 0));
    }

    [Fact]
    public void DefenseFactor_FortressBonus()
    {
        var unit = new Unit { TypeDefinition = new UnitDefinition { Defense = 10, Domain = UnitGas.Ground, Flags = new bool[15] } };
        var attacker = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = new bool[15] } };
        var map = new Map(true, 0);
        var tile = new Tile(0, 0, new Terrain { Defense = 2, Specials = [] }, 0, map, 0, new bool[2]);
        
        // 10 + (10 * 50 / 100) = 15.
        Assert.Equal(15, unit.DefenseFactor(attacker, tile, 50));
    }

    private static Unit Defender(int defense = 10, int order = 0, bool[]? flags = null) =>
        new()
        {
            TypeDefinition = new UnitDefinition
                { Defense = defense, Domain = UnitGas.Ground, Flags = flags ?? new bool[15] },
            Order = order
        };

    private static Unit GroundAttacker(int movesPerTurn = 1, int firepower = 1, int hitpoints = 10) =>
        new()
        {
            TypeDefinition = new UnitDefinition
            {
                Domain = UnitGas.Ground, Flags = new bool[15],
                AttackPerTurn = movesPerTurn, Firepwr = firepower, Hitp = hitpoints
            }
        };

    private static Tile TerrainTile(int terrainDefense = 2, bool river = false) =>
        new(0, 0, new Terrain { Defense = terrainDefense, Specials = [] }, 0, new Map(true, 0), 0, new bool[2])
            { River = river };

    private static Tile WalledCityTile(int wallEffect = 200)
    {
        var tile = TerrainTile();
        var city = new City { Owner = new Civilization { Id = 1 } };
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.Walled] = wallEffect } });
        tile.CityHere = city;
        return tile;
    }

    [Fact]
    public void DefenseFactor_CityWalls_MultiplyRatherThanAddTheirEffectValue()
    {
        // The shipped City Walls effect is 200, which Civ II reads as x3. Adding the
        // effect value on its own made walls a flat +2 whatever the garrison was.
        Assert.Equal(30, Defender().DefenseFactor(GroundAttacker(), WalledCityTile(), 0));
    }

    [Fact]
    public void DefenseFactor_CityWalls_SupersedeFortification()
    {
        // Civ II's combat guide: the fortification bonus is "superceded by fortress
        // improvement and city walls". They are not multiplied together -- x3 for
        // the walls, and being dug in adds nothing on top of them.
        Assert.Equal(30, Defender(order: (int)OrderType.Fortified)
            .DefenseFactor(GroundAttacker(), WalledCityTile(), 0));
    }

    [Fact]
    public void DefenseFactor_Fortress_SupersedesFortification()
    {
        // x2 for the fortress, and no x1.5 on top of it.
        Assert.Equal(20, Defender(order: (int)OrderType.Fortified)
            .DefenseFactor(GroundAttacker(), TerrainTile(), 100));
    }

    [Fact]
    public void DefenseFactor_CityWalls_AnswerLandAttacksOnly()
    {
        // "City walls triple the defense value of city defenders against attacks by
        // land units only."
        var fromTheAir = GroundAttacker();
        fromTheAir.TypeDefinition.Domain = UnitGas.Air;

        Assert.Equal(10, Defender().DefenseFactor(fromTheAir, WalledCityTile(), 0));
    }

    [Fact]
    public void DefenseFactor_ACityWithNoWalls_IsNoDefenceInItself()
    {
        // Neither the city nor its size counts for anything on its own in Civ II:
        // what a city gives a defender is City Walls, and without them a garrison
        // defends on the terrain it is standing on like anybody else.
        var tile = TerrainTile();
        tile.CityHere = new City { Owner = new Civilization { Id = 1 } };

        Assert.Equal(10, Defender().DefenseFactor(GroundAttacker(), tile, 0));
    }

    [Fact]
    public void DefenseFactor_CityWalls_IgnoredByHowitzerStyleAttacker()
    {
        var flags = new bool[15];
        flags[6] = true; // NegatesCityWalls
        var howitzer = new Unit
        {
            TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = flags }
        };

        Assert.Equal(10, Defender().DefenseFactor(howitzer, WalledCityTile(), 0));
    }

    [Theory]
    [InlineData(2, 10)]  // Desert, Plains, Grassland: x1
    [InlineData(3, 15)]  // Forest, Jungle, Swamp: x1.5, which integer division flattened to x1
    [InlineData(4, 20)]  // Hills: x2
    [InlineData(6, 30)]  // Mountains: x3
    public void DefenseFactor_TerrainKeepsItsHalfSteps(int terrainDefense, int expected)
    {
        Assert.Equal(expected, Defender().DefenseFactor(GroundAttacker(), TerrainTile(terrainDefense), 0));
    }

    [Fact]
    public void DefenseFactor_RiverAddsHalfAStepToTheTerrain()
    {
        // Grassland x1 and a river's half step: x1.5, not x1.25. Civ II adds the
        // river while the terrain adjustment is worked out rather than multiplying
        // it in afterwards -- "a hill square with a river gives a x2.5 bonus, a
        // (2 + 0.5) multiplier". As a x1.25 the two agreed on hills by coincidence
        // and nowhere else.
        Assert.Equal(15, Defender().DefenseFactor(GroundAttacker(), TerrainTile(river: true), 0));
    }

    [Theory]
    [InlineData(2, 15)]   // Grassland x1 + 0.5
    [InlineData(4, 25)]   // Hills x2 + 0.5 -- the one a x1.25 also got right
    [InlineData(6, 35)]   // Mountains x3 + 0.5, where a x1.25 gave 37.5
    public void DefenseFactor_RiverIsHalfAStepWhateverTheTerrain(int terrainDefense, double expected)
    {
        Assert.Equal(expected,
            Defender().DefenseFactor(GroundAttacker(), TerrainTile(terrainDefense, river: true), 0));
    }

    [Fact]
    public void DefenseFactor_TheWeakestUnitStillGainsByDiggingIn()
    {
        // Warriors defend at 1. Every other test here uses a defence of ten or a
        // hundred, where rounding the result down costs nothing -- and on those
        // values this fault was invisible. On the numbers the game actually uses,
        // fortifying took Warriors to 1.5 and the rounding took them back to 1, so
        // digging in did nothing at all for the unit most often left holding a new
        // city. The attacker's side of the same sum was never rounded.
        var warriors = Defender(defense: 1, order: (int)OrderType.Fortified);

        Assert.Equal(1.5, warriors.DefenseFactor(GroundAttacker(), TerrainTile(), 0));
    }

    [Fact]
    public void DefenseFactor_PikemenDoubleAgainstMountedAttackers()
    {
        var flags = new bool[15];
        flags[10] = true; // X2OnDefenseVersusHorse
        var pikemen = Defender(flags: flags);

        // Knights: two moves, one hit point, one firepower.
        Assert.Equal(20, pikemen.DefenseFactor(GroundAttacker(movesPerTurn: 2), TerrainTile(), 0));
    }

    [Fact]
    public void DefenseFactor_PikemenBonusReachesDragoonsAndCavalry()
    {
        var flags = new bool[15];
        flags[10] = true;
        var pikemen = Defender(flags: flags);

        // Cavalry carry two hit points; requiring one used to exclude them.
        Assert.Equal(20, pikemen.DefenseFactor(GroundAttacker(movesPerTurn: 2, hitpoints: 20), TerrainTile(), 0));
    }

    [Fact]
    public void DefenseFactor_PikemenBonusDoesNotApplyToSiegeArtillery()
    {
        var flags = new bool[15];
        flags[10] = true;
        var pikemen = Defender(flags: flags);

        // A Howitzer also has two moves; its two firepower is what excludes it.
        Assert.Equal(10, pikemen.DefenseFactor(GroundAttacker(movesPerTurn: 2, firepower: 2), TerrainTile(), 0));
    }
}
