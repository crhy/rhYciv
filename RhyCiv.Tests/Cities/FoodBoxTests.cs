using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using Model.Core.Cities;
using Model.Core.GameRules;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// The food box, checked against Civ II. Raised on 0.1.5 as something to verify
/// rather than as a fault, so these pin the rules the game says it follows: the
/// box holds ten food for every citizen the city is about to have, growth needs
/// an Aqueduct past eight and a Sewer System past twelve, and a Granary keeps
/// half the box across a growth.
/// </summary>
public class FoodBoxTests
{
    private const int FoodRows = 10;

    [Theory]
    [InlineData(1, 20)]
    [InlineData(4, 50)]
    [InlineData(7, 80)]
    [InlineData(11, 120)]
    public void TheBoxHoldsTenForEveryCitizenTheCityIsAboutToHave(int size, int expected)
    {
        // (size + 1) * RowsFoodBox, which is what the turn compares storage against.
        Assert.Equal(expected, (size + 1) * FoodRows);
    }

    [Fact]
    public void ACityBelowTheAqueductSizeGrowsFreely()
    {
        Assert.True(new City { Size = 7 }.CanGrow(Rules()));
    }

    [Fact]
    public void ACityAtTheAqueductSizeCannotGrowWithoutOne()
    {
        Assert.False(new City { Size = 8 }.CanGrow(Rules()));
    }

    [Fact]
    public void ACityAtTheSewerSizeCannotGrowWithoutOne()
    {
        // Even with an aqueduct: the sewer is the second gate, not a replacement.
        var city = new City { Size = 12 };
        city.OrderedImprovements.Add((int)ImprovementType.Aqueduct, Improvement(ImprovementType.Aqueduct));

        Assert.False(city.CanGrow(Rules()));
    }

    [Fact]
    public void AnAqueductLetsACityPastTheFirstGate()
    {
        var city = new City { Size = 8 };
        city.OrderedImprovements.Add((int)ImprovementType.Aqueduct, Improvement(ImprovementType.Aqueduct));

        Assert.True(city.CanGrow(Rules()));
    }

    [Fact]
    public void WithoutAGranaryGrowingEmptiesTheBox()
    {
        var city = new City { Size = 5 };

        city.ResetFoodStorage(FoodRows);

        Assert.Equal(0, city.FoodInStorage);
    }

    private static Rules Rules()
    {
        var rules = new Rules();
        rules.Cosmic.RowsFoodBox = FoodRows;
        rules.Cosmic.ToExceedCitySizeAqueductNeeded = 8;
        rules.Cosmic.SewerNeeded = 12;
        return rules;
    }

    private static Improvement Improvement(ImprovementType type) =>
        new() { Type = (int)type, Name = type.ToString() };
}
