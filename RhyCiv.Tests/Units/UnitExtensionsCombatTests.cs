using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Engine.Units;
using RhyCiv.Tests.Mocks;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Units;
using Moq;

namespace RhyCiv.Tests.Units;

public class UnitExtensionsCombatTests
{
    private (Mock<IGame> game, Rules rules, Map map) SetupGame()
    {
        var rules = new Rules();
        var map = new Map(true, 5) { XDim = 10, YDim = 10 };
        map.Tile = new Tile[10, 10];
        for (int y = 0; y < 10; y++)
        {
            for (int x_idx = 0; x_idx < 10; x_idx++)
            {
                int x = x_idx * 2 + y % 2;
                map.Tile[x_idx, y] = new Tile(x, y, new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>(), Defense = 100 }, 0, map, x_idx + y * 10, new bool[2]);
            }
        }
        var game = new Mock<IGame>();
        game.Setup(g => g.Rules).Returns(rules);
        return (game, rules, map);
    }

    [Fact]
    public void AttackFactor_CalculatesCorrectly()
    {
        var (game, rules, map) = SetupGame();
        var attacker = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Attack = 4,
                Flags = new bool[20]
            },
            Veteran = false
        };
        var defender = new Unit { TypeDefinition = new UnitDefinition { Attack = 1 } };
        
        // Base case: 4
        Assert.Equal(4.0, attacker.AttackFactor(defender));
        
        // Veteran: 4 * 1.5 = 6
        attacker.Veteran = true;
        Assert.Equal(6.0, attacker.AttackFactor(defender));
    }

    [Fact]
    public void DefenseFactor_CalculatesCorrectly()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        tile.Terrain.Defense = 100; // 100% bonus? No, wait.
        // Tile.Defense implementation: (River ? EffectiveTerrain.Defense + 1 : EffectiveTerrain.Defense) / 2
        // EffectiveTerrain.Defense is 100. (100 / 2) = 50.
        // Wait, why / 2? Civ2 terrain defense is usually 1.0, 1.5, etc.
        // 100 probably means 1.0.
        
        var defender = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Defense = 2,
                Flags = new bool[20],
                Domain = UnitGas.Ground
            },
            Veteran = false
        };
        var attacker = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Flags = new bool[20],
                Domain = UnitGas.Ground
            }
        };

        // Base case: df = 2. bestGroundFactor = 0. df = 2.
        // Terrain bonus: df * tile.Defense = 2 * 50 = 100.
        // Wait, 50?
        Assert.Equal(100, defender.DefenseFactor(attacker, tile, 0));
        
        // Veteran: 2 * 1.5 = 3. 3 * 50 = 150.
        defender.Veteran = true;
        Assert.Equal(150, defender.DefenseFactor(attacker, tile, 0));
        
        // Fortified: df = 3. fortifiedFactor = 3 / 2 = 1.5. df += 1.5 = 4.5.
        // 4.5 * 50 = 225.
        defender.Order = (int)OrderType.Fortified;
        Assert.Equal(225, defender.DefenseFactor(attacker, tile, 0));
    }

    [Fact]
    public void DefenseFactor_WithSAMBattery_CalculatesCorrectly()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        var city = new City { Owner = new Civilization { Id = 1 } };
        tile.CityHere = city;
        
        var defender = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Defense = 2,
                Flags = new bool[20],
                Domain = UnitGas.Ground
            },
            CurrentLocation = tile
        };
        var attacker = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Flags = new bool[20],
                Domain = UnitGas.Air
            }
        };

        // Add SAM Battery effect (100 means +100% = 2x total)
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.AirDefence] = 100 } });
        
        // Base df = 2. SAM bonus = 2 * 100 / 100 = 2. df = 4.
        // Terrain bonus: 4 * 50 = 200.
        Assert.Equal(200, defender.DefenseFactor(attacker, tile, 0));
        
        // Ground attacker does not trigger SAM
        var groundAttacker = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = new bool[20] } };
        Assert.Equal(100, defender.DefenseFactor(groundAttacker, tile, 0));
    }

    [Fact]
    public void DefenseFactor_WithCoastalFortress_CalculatesCorrectly()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        var city = new City { Owner = new Civilization { Id = 1 } };
        tile.CityHere = city;
        
        var defender = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Defense = 2,
                Flags = new bool[20],
                Domain = UnitGas.Ground
            },
            CurrentLocation = tile
        };
        var attacker = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Flags = new bool[20],
                Domain = UnitGas.Sea
            }
        };

        // Add Coastal Fortress effect (100 means +100% = 2x total)
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.SeaDefence] = 100 } });
        
        // Base df = 2. Sea defence bonus = 2 * 100 / 100 = 2. df = 4.
        // Terrain bonus: 4 * 50 = 200.
        Assert.Equal(200, defender.DefenseFactor(attacker, tile, 0));
    }

    [Fact]
    public void CityDefenseBonus_NamesTheFightersScramblingFromTheCity()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        var city = new City { Owner = new Civilization { Id = 1 } };
        tile.CityHere = city;

        // A fighter based in the city (fuel range 1) is launched to meet the attacker.
        var defender = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Air, Range = 1, Flags = new bool[20] } };
        var bomber = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Air, Range = 3, Flags = new bool[20] } };
        var otherFighter = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Air, Range = 1, Flags = new bool[20] } };

        Assert.Equal(DefenseBonusKind.FighterScramble, UnitExtensions.CityDefenseBonus(defender, bomber, tile));
        Assert.Equal(DefenseBonusKind.FighterScramble, UnitExtensions.CityDefenseBonus(defender, otherFighter, tile));
    }

    [Fact]
    public void CityDefenseBonus_NamesTheSAMBatteryUnderAnAirAttack()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        var city = new City { Owner = new Civilization { Id = 1 } };
        tile.CityHere = city;
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.AirDefence] = 100 } });

        var defender = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Defense = 2,
                Flags = new bool[20],
                Domain = UnitGas.Ground
            },
            CurrentLocation = tile
        };
        var attacker = new Unit { TypeDefinition = new UnitDefinition { Flags = new bool[20], Domain = UnitGas.Air } };
        var groundAttacker = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = new bool[20] } };

        Assert.Equal(DefenseBonusKind.SamBattery, UnitExtensions.CityDefenseBonus(defender, attacker, tile));

        // The batteries answer the sky, not the man walking up to the gate.
        Assert.Equal(DefenseBonusKind.None, UnitExtensions.CityDefenseBonus(defender, groundAttacker, tile));
    }

    [Fact]
    public void CityDefenseBonus_NamesTheCoastalFortressUnderAnAttackFromTheSea()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        var city = new City { Owner = new Civilization { Id = 1 } };
        tile.CityHere = city;
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.SeaDefence] = 100 } });

        var defender = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Defense = 2,
                Flags = new bool[20],
                Domain = UnitGas.Ground
            },
            CurrentLocation = tile
        };
        var attacker = new Unit { TypeDefinition = new UnitDefinition { Flags = new bool[20], Domain = UnitGas.Sea } };
        var groundAttacker = new Unit { TypeDefinition = new UnitDefinition { Domain = UnitGas.Ground, Flags = new bool[20] } };

        Assert.Equal(DefenseBonusKind.CoastalFortress, UnitExtensions.CityDefenseBonus(defender, attacker, tile));
        Assert.Equal(DefenseBonusKind.None, UnitExtensions.CityDefenseBonus(defender, groundAttacker, tile));
    }

    [Fact]
    public void CityDefenseBonus_NamesTheMissileDefenceUnderAnAttackCarryingAWarhead()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        var city = new City { Owner = new Civilization { Id = 1 } };
        tile.CityHere = city;
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.MissileDefence] = 100 } });

        var defender = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Defense = 2,
                Flags = new bool[20],
                Domain = UnitGas.Ground
            },
            CurrentLocation = tile
        };
        var warhead = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Flags = new bool[20],
                Domain = UnitGas.Air,
                Effects = { [UnitEffect.SDIVulnerable] = 1 }
            }
        };
        var conventional = new Unit { TypeDefinition = new UnitDefinition { Flags = new bool[20], Domain = UnitGas.Air } };

        Assert.Equal(DefenseBonusKind.MissileDefence, UnitExtensions.CityDefenseBonus(defender, warhead, tile));

        // Nothing to shoot down, so nothing to say: the city's SDI stays quiet.
        Assert.Equal(DefenseBonusKind.None, UnitExtensions.CityDefenseBonus(defender, conventional, tile));
    }

    [Fact]
    public void CityDefenseBonus_SaysNothingAboutAnOrdinaryGroundAttack()
    {
        var (game, rules, map) = SetupGame();
        var tile = map.Tile[1, 1];
        var city = new City { Owner = new Civilization { Id = 1 } };
        tile.CityHere = city;

        // Every city defence there is, and a land unit walking up to the gate: none
        // of them is what held it back, and the player should hear nothing.
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.AirDefence] = 100 } });
        city.AddImprovement(new Improvement { Type = 2, Effects = { [Effects.SeaDefence] = 100 } });
        city.AddImprovement(new Improvement { Type = 3, Effects = { [Effects.MissileDefence] = 100 } });

        var defender = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Defense = 2,
                Flags = new bool[20],
                Domain = UnitGas.Ground
            },
            CurrentLocation = tile
        };
        var attacker = new Unit
        {
            TypeDefinition = new UnitDefinition
            {
                Attack = 4,
                Flags = new bool[20],
                Domain = UnitGas.Ground,
                Effects = { [UnitEffect.SDIVulnerable] = 1 }
            }
        };

        Assert.Equal(DefenseBonusKind.None, UnitExtensions.CityDefenseBonus(defender, attacker, tile));
    }

    /// <summary>
    /// The same combat the player sees: an attack driven through the engine's own
    /// move, with the bonus arriving on the event the interface is told about.
    /// <para>
    /// The arithmetic was already pinned by the tests above; what was never checked
    /// is that the combat resolver bothers to say which bonus it used. Without that
    /// the message has nothing to be built from.
    /// </summary>
    /// <remarks>
    /// The defender is deliberately hard to shift and the attacker deliberately
    /// soft, so the fight ends with the attacker spent: the city is never shrunk,
    /// and the test stays about the message rather than about the aftermath.
    /// </remarks>
    [Fact]
    public void TheCombatEventCarriesTheBonusThatHeldTheDefender()
    {
        var (game, map, mine, theirs, player) = CombatWorld();

        var tile = map.Tile[3, 5];
        var city = new City { Owner = theirs, Location = tile, Size = 5 };
        tile.CityHere = city;
        city.AddImprovement(new Improvement { Type = 1, Effects = { [Effects.AirDefence] = 100 } });

        var defender = new Unit
        {
            Owner = theirs,
            TypeDefinition = new UnitDefinition
            {
                Domain = UnitGas.Ground, Defense = 1000, Hitp = 1, Firepwr = 1, Attack = 1,
                Flags = Enumerable.Repeat(false, 13).ToArray()
            }
        };
        defender.CurrentLocation = tile;
        tile.UnitsHere.Add(defender);
        tile.SetVisible(mine.Id);

        var from = map.Tile[3, 4];
        var attacker = new Unit
        {
            Owner = mine,
            TypeDefinition = new UnitDefinition
            {
                Domain = UnitGas.Air, Move = 10, Attack = 1, Defense = 1, Hitp = 1, Firepwr = 1,
                Flags = Enumerable.Repeat(false, 13).ToArray()
            },
            X = from.X,
            Y = from.Y
        };
        attacker.CurrentLocation = from;

        MovementFunctions.MoveC2(game, attacker, tile.X - from.X, tile.Y - from.Y);

        Assert.True(player.CombatCalled, "the player watching the fight should be told about it");
        Assert.Equal(DefenseBonusKind.SamBattery, player.LastCombat!.DefenseBonus);
    }

    /// <summary>
    /// A world small enough to fight one battle in: two civilisations at war, a
    /// city with a SAM battery in it, and an air unit one square away.
    /// </summary>
    private (MockGame game, Map map, Civilization mine, Civilization theirs, MockPlayer player) CombatWorld()
    {
        var map = new Map(true, 5) { XDim = 10, YDim = 10 };
        map.Tile = new Tile[10, 10];
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                map.Tile[x, y] = new Tile(x * 2 + y % 2, y,
                    new Terrain { Name = "Plains", Type = TerrainType.Plains, Specials = Array.Empty<Special>(), Defense = 100 },
                    0, map, x + y * 10, new bool[2]);
            }
        }
        map.MapRevealed = true;

        var mine = new Civilization { Id = 0, TribeName = "Mine" };
        var theirs = new Civilization { Id = 1, TribeName = "Theirs" };

        // Already at war: attacking is how a war starts, and this test is about
        // what the player is told once it has.
        DiplomacyFunctions.Between(mine, theirs).War = true;

        var game = new MockGame
        {
            Rules = new Rules(),
            Maps = [map],
            AllCivilizations = [mine, theirs]
        };
        var player = new MockPlayer(mine);
        game.Players = [player, new MockPlayer(theirs)];
        return (game, map, mine, theirs, player);
    }
}
