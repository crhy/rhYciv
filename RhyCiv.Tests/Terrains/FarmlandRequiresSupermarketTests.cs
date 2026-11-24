using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Scripting.ScriptObjects;
using RhyCiv.Engine.Terrains;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Units;
using Moq;

namespace RhyCiv.Tests.Terrains;

/// <summary>
/// Farmland is the second level of irrigation. Civ II gates it on Refrigeration
/// AND a Supermarket, so with the tech but no market the build is refused with a
/// named message and with both it is allowed. The narrowest rule the engine can
/// state honestly is that the civilisation owns a Supermarket improvement
/// somewhere; the same assessment also gates the AI worker, so an engineer that
/// reaches no market refuses the square too.
/// </summary>
public class FarmlandRequiresSupermarketTests
{
    private const string NoMarketPopup = "NEEDSUPERMARKET";

    public FarmlandRequiresSupermarketTests()
    {
        // The farmland level and its labels come from the ruleset, which reads
        // the game's label table; size it as the other terrain tests do.
        Labels.Items = new string[1000];
    }

    [Theory]
    // Refrigeration, no market: refused, and told why by name.
    [InlineData(true, false, false, NoMarketPopup)]
    // Refrigeration and a market: allowed.
    [InlineData(true, true, true, NoMarketPopup)]
    // No Refrigeration: the existing tech gate answers, untouched.
    [InlineData(false, false, false, "FARMLAND")]
    public void FarmlandNeedsRefrigerationAndASupermarket(
        bool refrigeration, bool supermarket, bool expectedEnabled, string popupWhenRefused)
    {
        var irrigation = TerrainImprovementFunctions.GetStandardImprovements(new Rules())[ImprovementTypes.Irrigation];
        var tile = TileWithIrrigationBuilt();
        var civ = CivWithRefrigeration(refrigeration);
        if (supermarket)
        {
            civ.Cities.Add(CityWithSupermarket());
        }

        var result = TerrainImprovementFunctions.CanImprovementBeBuiltHere(tile, irrigation, civ);

        Assert.Equal(expectedEnabled, result.Enabled);
        if (!expectedEnabled)
        {
            Assert.Equal(popupWhenRefused, result.ErrorPopup);
        }
    }

    [Fact]
    public void WiringPutsSupermarketOnTheFarmlandLevelOnly()
    {
        var improvements = TerrainImprovementFunctions.GetStandardImprovements(new Rules());

        var farmland = improvements[ImprovementTypes.Irrigation].Levels[1];
        Assert.Equal((int)ImprovementType.Supermarket, farmland.RequiredCityImprovement);
        Assert.Equal(NoMarketPopup, farmland.MissingRequiredImprovementMessage);

        // Every other level is left at the default of none, so it is untouched.
        Assert.Equal(0, improvements[ImprovementTypes.Irrigation].Levels[0].RequiredCityImprovement);
        Assert.Equal(0, improvements[ImprovementTypes.Road].Levels[1].RequiredCityImprovement);
        Assert.Equal(0, improvements[ImprovementTypes.Mining].Levels[0].RequiredCityImprovement);
        Assert.Equal(0, improvements[ImprovementTypes.Pollution].Levels[0].RequiredCityImprovement);
    }

    [Fact]
    public void PlainIrrigationLevelWithoutTheFieldIsUnaffected()
    {
        // A farmland level that does not ask for a city improvement (the default
        // empty field) is built on the tech alone, market or not.
        var irrigation = IrrigationImprovement(requiredCityImprovement: 0);
        var tile = TileWithIrrigationBuilt();
        var civ = CivWithRefrigeration(true);

        var result = TerrainImprovementFunctions.CanImprovementBeBuiltHere(tile, irrigation, civ);

        Assert.True(result.Enabled);
    }

    [Fact]
    public void RailroadLevelWithoutTheFieldIsUnaffected()
    {
        var road = new TerrainImprovement
        {
            Id = ImprovementTypes.Road,
            Levels = new List<ImprovementLevel>
            {
                new() { Name = "Road", RequiredTech = AdvancesConstants.Nil },
                new() { Name = "Railroad", RequiredTech = (int)AdvanceType.Railroad }
            }
        };
        var tile = new Tile(0, 0, new Terrain { Specials = [] }, 0, new Map(true, 0), 0, new bool[2]);
        tile.Improvements.Add(new ConstructedImprovement { Improvement = ImprovementTypes.Road, Level = 0 });
        var civ = new Civilization { Id = 1 };
        civ.Advances = new bool[(int)AdvanceType.Railroad + 1];
        civ.Advances[(int)AdvanceType.Railroad] = true;

        var result = TerrainImprovementFunctions.CanImprovementBeBuiltHere(tile, road, civ);

        Assert.True(result.Enabled);
    }

    [Fact]
    public void MiningMaxLevelAnswerIsUntouched()
    {
        var mining = new TerrainImprovement
        {
            Id = ImprovementTypes.Mining,
            Levels = new List<ImprovementLevel> { new() { Name = "Mining", RequiredTech = AdvancesConstants.Nil } },
            MaxLevelReachedMessage = "ALREADYMINING"
        };
        var tile = new Tile(0, 0, new Terrain { Specials = [] }, 0, new Map(true, 0), 0, new bool[2]);
        tile.Improvements.Add(new ConstructedImprovement { Improvement = ImprovementTypes.Mining, Level = 0 });

        var result = TerrainImprovementFunctions.CanImprovementBeBuiltHere(tile, mining, new Civilization { Id = 1 });

        Assert.False(result.Enabled);
        Assert.Equal("ALREADYMINING", result.ErrorPopup);
    }

    [Fact]
    public void PollutantCleanupIsUntouched()
    {
        var pollution = new TerrainImprovement
        {
            Id = ImprovementTypes.Pollution,
            Negative = true,
            Levels = new List<ImprovementLevel> { new() { Name = "Pollution", RequiredTech = AdvancesConstants.Nil } }
        };
        var tile = new Tile(0, 0, new Terrain { Specials = [] }, 0, new Map(true, 0), 0, new bool[2]);
        tile.Improvements.Add(new ConstructedImprovement { Improvement = ImprovementTypes.Pollution, Level = 0 });

        var result = TerrainImprovementFunctions.CanImprovementBeBuiltHere(tile, pollution, new Civilization { Id = 1 });

        Assert.True(result.Enabled);
    }

    [Fact]
    public void AiEngineerRefusesTheSameSquareWithoutASupermarket()
    {
        var irrigation = TerrainImprovementFunctions.GetStandardImprovements(new Rules())[ImprovementTypes.Irrigation];
        var civ = CivWithRefrigeration(true);
        var tile = TileWithIrrigationBuilt();

        var game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
        game.TerrainImprovements = new Dictionary<int, TerrainImprovement> { [ImprovementTypes.Irrigation] = irrigation };
        var aiPlayer = new AiPlayer(0, civ, null, game, new Mock<AiInterface>(null, null, 0, null).Object);
        var unit = new Unit { Owner = civ };

        var workFor = typeof(AiPlayer).GetMethod("WorkFor", BindingFlags.NonPublic | BindingFlags.Instance);

        // Refrigeration but no market: the engineer walks away from the square.
        Assert.Null(workFor!.Invoke(aiPlayer, new object[] { unit, tile, false }));

        // The market is what the AI was missing; now it works the square.
        civ.Cities.Add(CityWithSupermarket());
        var action = (TerrainImprovement?)workFor.Invoke(aiPlayer, new object[] { unit, tile, false });
        Assert.NotNull(action);
        Assert.Equal(ImprovementTypes.Irrigation, action!.Id);
    }

    private static TerrainImprovement IrrigationImprovement(int requiredCityImprovement) => new()
    {
        Id = ImprovementTypes.Irrigation,
        Levels = new List<ImprovementLevel>
        {
            new() { Name = "Irrigation", RequiredTech = AdvancesConstants.Nil },
            new()
            {
                Name = "Farmland",
                RequiredTech = (int)AdvanceType.Refrigerat,
                RequiredCityImprovement = requiredCityImprovement,
                MissingRequiredTechMessage = "FARMLAND",
                MissingRequiredImprovementMessage = NoMarketPopup
            }
        }
    };

    private static Tile TileWithIrrigationBuilt()
    {
        var tile = new Tile(0, 0, new Terrain { Specials = [] }, 0, new Map(true, 0), 0, new bool[2]);
        tile.Improvements.Add(new ConstructedImprovement
        {
            Improvement = ImprovementTypes.Irrigation,
            Level = 0,
            Group = 2
        });
        return tile;
    }

    private static Civilization CivWithRefrigeration(bool refrigeration)
    {
        var civ = new Civilization { Id = 1 };
        civ.Advances = new bool[(int)AdvanceType.Refrigerat + 1];
        civ.Advances[(int)AdvanceType.Refrigerat] = refrigeration;
        return civ;
    }

    private static City CityWithSupermarket()
    {
        var city = new City();
        city.OrderedImprovements.Add((int)ImprovementType.Supermarket,
            new Improvement { Type = (int)ImprovementType.Supermarket, Name = "Supermarket" });
        return city;
    }
}
